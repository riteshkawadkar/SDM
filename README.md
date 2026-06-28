# SecureDeviceManager (SDM) — Backend

MDM backend for Android devices. .NET 8 ASP.NET Core Web API targeting PostgreSQL, with Firebase Cloud Messaging (FCM) for push notifications and Hangfire for background job processing.

---

## Quick Start (Development)

1. **Start Postgres and pgAdmin via Docker Compose:**
   ```powershell
   docker compose up -d
   ```

2. **Apply EF Core migrations:**
   ```powershell
   dotnet ef database update --project "SDM.Infrastructure/SDM.Infrastructure.csproj" --startup-project "SDM.API/SDM.API.csproj"
   ```

3. **Start the API:**
   ```powershell
   $Env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run --project "SDM.API/SDM.API.csproj"
   ```

4. **Swagger UI:** `http://localhost:<port>/swagger`
5. **Hangfire dashboard:** `http://localhost:<port>/hangfire`
6. **pgAdmin:** `http://localhost:8081` (admin@local.com / admin)

---

## Deploying to Azure VM (Docker)

1. SSH into the VM and clone/pull the repo to `~/SDM`
2. Place your Firebase service account JSON at `~/SDM/SDM.API/firebase-sa.json`
3. Start all services:
   ```bash
   cd ~/SDM
   docker compose up -d
   ```
4. Apply migrations:
   ```bash
   docker exec sdm-api dotnet ef database update \
     --project SDM.Infrastructure/SDM.Infrastructure.csproj \
     --startup-project SDM.API/SDM.API.csproj
   ```

> **Important:** The `firebase-sa.json` file must exist on the host *before* `docker compose up` is run for the first time. If Docker created a directory at that path, remove it (`sudo rm -rf ~/SDM/SDM.API/firebase-sa.json`) and copy the file again, then do a full `docker compose down && docker compose up -d`.

---

## Configuration

Required sections in `appsettings.json` / environment:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=sdm_db;Username=sdm_user;Password=secret"
  },
  "Jwt": {
    "Issuer": "sdm-api",
    "Audience": "sdm-client",
    "Key": "<min 32-char secret>",
    "ExpiryMinutes": 480
  },
  "Firebase": {
    "ServiceAccountPath": "firebase-sa.json"
  },
  "Hangfire": {
    "Enabled": true
  }
}
```

`PushService` uses the Firebase HTTP v1 API with the service account JSON. It falls back to the legacy `Firebase:ServerKey` if the JSON is not present.

---

## End-to-End Flow

### 1. Create an Enrollment Token (admin)

```bash
curl -X POST http://<host>/api/enrollment/tokens \
  -H "Authorization: Bearer <admin-jwt>" \
  -H "Content-Type: application/json" \
  -d '{"maxDevices": 1, "expiresInHours": 24}'
```

Returns a token string (and optionally a QR code via `POST /api/enrollment/tokens/generate-qr`).

### 2. Enroll the Android Device

The Android agent handles the deep link `sdm://enroll?token=<token>`. When the QR code is scanned:

1. The app reads the token and calls `POST /api/devices/register-with-token` with device details (ANDROID_ID, manufacturer, model, OS version) and its current FCM token.
2. The backend validates the token, creates (or updates) the device record, registers the FCM token in `DevicePushTokens`, and decrements the token's `MaxDevices` counter.
3. The response includes a **device JWT** (7-day, role `"Device"`, `sub` = device GUID) stored in `EncryptedSharedPreferences`.
4. The app schedules a WorkManager periodic job (`HeartbeatWorker`) to call `POST /api/devices/{id}/heartbeat` every 15 minutes, keeping `batteryLevel` and `lastSeen` up to date.

### 3. Send a Command (admin)

```bash
curl -X POST http://<host>/api/devices/<deviceId>/commands \
  -H "Content-Type: application/json" \
  -d '{"commandType": "LockScreen", "payload": ""}'
```

Supported command types: `LockScreen`, `DisableApp`, `EnableApp`, `LockApp`, `InstallApp`, `WipeData`.

What happens:
1. A `DeviceCommand` record is saved with status `Pending`.
2. The backend immediately attempts an FCM push via `PushService.SendToDeviceAsync`. On success the status moves to `Sent`; on failure it stays `Failed`.
3. `HangfireJobs.ProcessPendingCommands` runs every minute and retries any `Pending`/`Failed` commands up to 5 times.

### 4. Device Receives and Executes the Command

The Android agent's `FCMService.onMessageReceived` receives the FCM data message:

| FCM data key | Value |
|---|---|
| `body` | command type (e.g. `LockScreen`) |
| `payload` | JSON `{"commandId":"<uuid>","payload":"<value>"}` |

Execution by command type:

- **LockScreen** — calls `DevicePolicyManager.lockNow()` (requires Device Admin)
- **DisableApp / EnableApp** — hides/shows an app package (requires Device Owner)
- **LockApp** — pins an app to the screen via `setLockTaskPackages` (requires Device Owner)
- **InstallApp** — downloads APK from `url` and installs via `PackageInstaller`
- **WipeData** — factory reset via `DevicePolicyManager.wipeData` (disabled by default)

### 5. Device Reports Execution Status

After executing the command, the agent calls:

```
POST /api/devices/{deviceId}/commands/{commandId}/status
{ "success": true }
```

The backend updates the `DeviceCommand` status to `Executed` (success) or `Failed`.

### 6. Unenroll a Device

From the Android app: tap **Unenroll Device**. This calls `DELETE /api/devices/{deviceId}`, clears local prefs, and cancels the heartbeat job.

From curl (admin/dev):
```bash
curl -X DELETE http://<host>/api/devices/<deviceId>
```

This removes the device, its push tokens, commands, and heartbeats from the database.

---

## API Reference

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/enrollment/tokens` | Admin JWT | Create enrollment token |
| POST | `/api/enrollment/tokens/generate-qr` | Admin JWT | Create token + QR image |
| POST | `/api/devices/register-with-token` | None | Enroll device with token |
| GET | `/api/devices` | None | List all devices |
| GET | `/api/devices/{id}` | JWT | Get device by ID |
| DELETE | `/api/devices/{id}` | None* | Delete/unenroll device |
| POST | `/api/devices/{id}/heartbeat` | Device JWT | Update battery/lastSeen |
| POST | `/api/devices/{id}/push-token` | Device JWT | Register FCM token |
| POST | `/api/devices/update-fcm-token` | Device JWT | Refresh FCM token |
| POST | `/api/devices/{id}/commands` | JWT | Send command to device |
| POST | `/api/devices/{id}/commands/{cid}/status` | Device JWT | Report command result |

\* `DELETE /api/devices/{id}` is `[AllowAnonymous]` for development convenience. Restrict with an admin role before production.

---

## Architecture

```
SDM.Domain → SDM.Application → SDM.Infrastructure → SDM.API
```

- **SDM.Domain** — Entities (`Device`, `DeviceCommand`, `DeviceHeartbeat`, `DevicePushToken`, `EnrollmentToken`, `User`, `AuditLog`) and enums.
- **SDM.Application** — Interfaces, DTOs, exceptions, and settings. No infrastructure dependencies.
- **SDM.Infrastructure** — `ApplicationDbContext`, EF Core migrations, service implementations, `HangfireJobs`.
- **SDM.API** — Four controllers and `Program.cs` for DI wiring.

---

## Android Sample Agent

See `samples/android-agent/` for the reference Android implementation.
