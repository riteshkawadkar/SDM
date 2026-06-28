# SecureDeviceManager (SDM) — Developer Guide

This document helps a new developer quickly understand, run, debug, and extend SecureDeviceManager. It covers repository layout, technology choices, the domain model, architecture, all communication flows (enrollment, heartbeat, commands, unenroll), and operational/debugging guidance.

---

## 1. Project Overview

SecureDeviceManager (SDM) is a backend platform for enrolling and remotely managing Android devices. It provides:

- Enrollment token generation (QR code / deep link)
- Device registration using enrollment tokens
- Remote command delivery via Firebase Cloud Messaging (FCM)
- Command status reporting from device back to server
- Periodic heartbeat ingestion (battery, storage, last-seen)
- FCM push token management
- Background command retry processing via Hangfire
- Audit logging for all device lifecycle events

---

## 2. Tech Stack

| Technology | Role |
|---|---|
| .NET 8 / ASP.NET Core | Web API host |
| Entity Framework Core + Npgsql | ORM + PostgreSQL access |
| PostgreSQL | Persistent storage |
| Hangfire | Background job scheduling (command retry) |
| Firebase Cloud Messaging (HTTP v1) | Push commands to Android devices |
| JWT (Microsoft.AspNetCore.Authentication.JwtBearer) | Auth for users and devices |
| QRCoder | Generate enrollment QR codes |
| Docker / Docker Compose | Container runtime (API + Postgres + pgAdmin) |

**Firebase uses the HTTP v1 API** with a service account JSON (`firebase-sa.json`). It falls back to the legacy `Firebase:ServerKey` if the JSON is absent.

---

## 3. Repository Layout

```
src/backend/SDM/
├── SDM.API/                    # Web API entry point
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── DevicesController.cs
│   │   ├── CommandsController.cs
│   │   └── EnrollmentController.cs
│   ├── Program.cs              # DI wiring, middleware, Hangfire setup
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── firebase-sa.json        # NOT committed — place manually (see §7)
│
├── SDM.Application/            # Interfaces, DTOs, settings (no infra deps)
│   ├── DTOs/
│   └── Interfaces/
│
├── SDM.Infrastructure/         # EF Core, service implementations, jobs
│   ├── Data/ApplicationDbContext.cs
│   ├── Migrations/
│   └── Services/
│       ├── DeviceService.cs
│       ├── CommandService.cs
│       ├── PushService.cs
│       └── HangfireJobs.cs
│
├── SDM.Domain/                 # Entities and enums
│   └── Entities/
│
├── samples/android-agent/      # Reference Android MDM agent (gitignored)
└── docs/
    └── DEVELOPER_GUIDE.md      # This file
```

Dependency direction: `Domain ← Application ← Infrastructure ← API`

---

## 4. Domain Model

### Entities

**Device**
`Id`, `DeviceIdentifier` (unique — ANDROID_ID), `SerialNumber`, `Manufacturer`, `Model`, `AndroidVersion`, `Status`, `BatteryLevel`, `LastSeen`, `CreatedOn`, `UpdatedOn`

**EnrollmentToken**
`Id`, `Token`, `ExpiresOn`, `MaxDevices`, `IsActive`
— `MaxDevices` is decremented on each new device enrollment; token deactivates when it hits 0.

**DevicePushToken**
`Id`, `DeviceId` (FK), `Token` (FCM registration token), `IsActive`, `CreatedOn`
— No cascade delete configured; `DeviceService.DeleteAsync` removes these explicitly.

**DeviceCommand**
`Id`, `DeviceId` (FK), `CommandType`, `Payload`, `Status` (`Pending=0, Sent=1, Executed=2, Failed=3`), `RetryCount`, `CreatedOn`, `ExecutedOn`
— Cascade deleted when device is removed.

**DeviceHeartbeat**
`Id`, `DeviceId` (FK), `BatteryLevel`, `FreeStorage`, `Latitude`, `Longitude`, `CreatedOn`
— Cascade deleted when device is removed.

**AuditLog**
`Id`, `Action`, `EntityName`, `EntityId`, `NewValue` (JSON), `Timestamp`

**User / Role**
Minimal user model for JWT generation. Device JWTs use an in-memory pseudo-user (not persisted) with `sub` = device GUID and role `"Device"`.

---

## 5. Architecture

```
┌─────────────────────────────────────────────────┐
│                   SDM.API                        │
│  AuthController  DevicesController               │
│  CommandsController  EnrollmentController        │
└──────────────┬──────────────────────────────────┘
               │ IDeviceService / ICommandService
               │ IPushService / IAuthService
┌──────────────▼──────────────────────────────────┐
│              SDM.Infrastructure                  │
│  DeviceService   CommandService   PushService    │
│  HangfireJobs    ApplicationDbContext            │
└──────┬───────────────────────────────┬──────────┘
       │ EF Core                       │ HTTP v1
┌──────▼──────┐               ┌────────▼────────┐
│  PostgreSQL  │               │  Firebase FCM   │
└─────────────┘               └─────────────────┘
                                       │ FCM push
                               ┌───────▼─────────┐
                               │  Android Agent   │
                               │  (FCMService)    │
                               └─────────────────┘
```

---

## 6. End-to-End Flows

### 6.1 Device Enrollment

```
Admin                    Backend                      Android Agent
  │                         │                               │
  ├─POST /enrollment/tokens─▶│                               │
  │◀── token string ─────────┤                               │
  │                         │                               │
  │  (QR code / deep link)  │                               │
  │─────────────────────────────────────────────────────────▶│
  │                         │                               │
  │                         │◀─POST /devices/register-with-token
  │                         │  {token, deviceIdentifier,    │
  │                         │   manufacturer, model,        │
  │                         │   androidVersion, fcmToken}   │
  │                         │                               │
  │                         │ • validate token              │
  │                         │ • create/update Device row    │
  │                         │ • save FCM token              │
  │                         │ • decrement token.MaxDevices  │
  │                         │ • generate device JWT         │
  │                         │                               │
  │                         ├──{deviceId, deviceJwt}───────▶│
  │                         │                               │
  │                         │          • save JWT + deviceId to EncryptedSharedPrefs
  │                         │          • schedule HeartbeatWorker (15 min)
```

Key points:
- Re-enrolling the **same** device (same `DeviceIdentifier`) updates metadata but does **not** decrement the token slot.
- FCM token is registered via `RegisterPushTokenAsync` which deduplicates — existing token rows are reactivated rather than duplicated.

### 6.2 Heartbeat

The `HeartbeatWorker` (WorkManager, 15-minute interval, requires network) calls:

```
POST /api/devices/{deviceId}/heartbeat
Authorization: Bearer <device-jwt>
{ "battery": 85, "freeStorage": 12345678 }
```

Backend updates `Device.BatteryLevel`, `Device.LastSeen`, and inserts a `DeviceHeartbeat` row.

### 6.3 Command Delivery

```
Admin                    Backend                      Android Agent
  │                         │                               │
  ├─POST /devices/{id}/commands
  │  {commandType:"LockScreen", payload:""}                 │
  │                         │                               │
  │                         │ 1. Save DeviceCommand (Pending)
  │                         │ 2. Query DevicePushTokens     │
  │                         │ 3. POST to FCM HTTP v1 ───────▶ FCM
  │                         │    data: {                    │
  │                         │      "body": "LockScreen",    │
  │                         │      "payload": {"commandId":"...", "payload":""}
  │                         │    }                          │
  │                         │ 4. Status → Sent (or Failed)  │
  │◀── 201 {id, status:1} ──┤                               │
  │                         │          FCM delivers ────────▶│
  │                         │                               │ onMessageReceived
  │                         │                               │ • read data["body"] → "LockScreen"
  │                         │                               │ • extract commandId from data["payload"]
  │                         │                               │ • dpm.lockNow()
  │                         │                               │
  │                         │◀──POST /devices/{id}/commands/{cid}/status
  │                         │   {success: true}             │
  │                         │                               │
  │                         │ Status → Executed             │
```

**FCM data message keys** (important for Android-side parsing):

| Key | Value |
|-----|-------|
| `body` | command type string (e.g. `"LockScreen"`) |
| `payload` | JSON string `{"commandId":"<uuid>","payload":"<value>"}` |

**Command retry:** `HangfireJobs.ProcessPendingCommands` runs every minute and reprocesses `Pending`/`Failed` commands up to `MaxRetries` (default 5).

### 6.4 Supported Commands

| CommandType | Required privilege | Action |
|---|---|---|
| `LockScreen` | Device Admin | `DevicePolicyManager.lockNow()` |
| `DisableApp` | Device Owner | Hide app package |
| `EnableApp` | Device Owner | Show app package |
| `LockApp` | Device Owner | Pin app via `setLockTaskPackages` |
| `InstallApp` | None | Download APK + silent install |
| `WipeData` | Device Owner | Factory reset (disabled by default) |

Device Admin is granted via the UI button in the agent. Device Owner requires ADB provisioning: `adb shell dpm set-device-owner com.example.sdmagent/.AdminReceiver`.

### 6.5 FCM Token Refresh

When Firebase issues a new FCM token (e.g. after app reinstall), `FCMService.onNewToken` calls:

```
POST /api/devices/update-fcm-token
Authorization: Bearer <device-jwt>
{ "fcmToken": "<new-token>" }
```

The backend reads `sub` from the JWT to identify the device and calls `RegisterPushTokenAsync`.

### 6.6 Unenroll

```
Android Agent                Backend
     │                          │
     ├─DELETE /api/devices/{id}─▶│
     │                          │ • remove DevicePushTokens (explicit)
     │                          │ • remove Device (cascades Commands + Heartbeats)
     │                          │ • write AuditLog
     │◀── 204 No Content ───────┤
     │                          │
     • clear EncryptedSharedPrefs
     • cancel HeartbeatWorker (WorkManager)
```

`DELETE /api/devices/{id}` is `[AllowAnonymous]` for development convenience. Add an admin role requirement before production.

---

## 7. Configuration

### appsettings.json / appsettings.Development.json

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

### Firebase Service Account JSON

1. Firebase Console → Project Settings → Service Accounts → **Generate new private key**
2. Download the JSON and place it at `SDM.API/firebase-sa.json`
3. This file is gitignored — never commit it

**Docker volume mount pitfall:** Docker creates a directory at the mount path if the source file doesn't exist at container creation time. If this happens:
```bash
sudo rm -rf ~/SDM/SDM.API/firebase-sa.json   # remove the bad directory
# copy the real file here, then:
docker compose down && docker compose up -d   # full recreate, not just restart
```

---

## 8. Running Locally

```powershell
# Start Postgres + pgAdmin
docker compose up -d

# Apply migrations
dotnet ef database update \
  --project "SDM.Infrastructure/SDM.Infrastructure.csproj" \
  --startup-project "SDM.API/SDM.API.csproj"

# Run the API
$Env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project "SDM.API/SDM.API.csproj"
```

- Swagger: `http://localhost:<port>/swagger`
- Hangfire: `http://localhost:<port>/hangfire`
- pgAdmin: `http://localhost:8081` (admin@local.com / admin)

For testing with a real device, expose the local port via ngrok:
```bash
ngrok http 5254
```
Use the ngrok HTTPS URL as the server URL in the Android agent.

---

## 9. Running on Azure VM (Docker)

```bash
cd ~/SDM
# Place firebase-sa.json at ~/SDM/SDM.API/firebase-sa.json first
docker compose up -d

# Apply migrations
docker exec sdm-api dotnet ef database update \
  --project SDM.Infrastructure/SDM.Infrastructure.csproj \
  --startup-project SDM.API/SDM.API.csproj
```

The NSG must allow inbound TCP on port 5254 from your IP.

---

## 10. Debugging

### Visual Studio

1. Open `SDM.slnx`
2. Set startup project to `SDM.API`
3. Choose the `http` or `https` launch profile
4. F5 — debugger attaches; set breakpoints in `DeviceService.cs`, `CommandService.cs`, `PushService.cs`

### Useful breakpoints

| File | What to investigate |
|------|---------------------|
| `DeviceService.RegisterWithTokenAsync` | Token validation, device creation, FCM token registration |
| `PushService.SendToDeviceAsync` | FCM push logic, token lookup |
| `HangfireJobs.ProcessPendingCommands` | Retry logic |
| `CommandService.ReportCommandStatusAsync` | Status update from device |

---

## 11. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `401` on enrollment endpoint | Missing `[AllowAnonymous]` | `register-with-token` and `register` are already anonymous |
| `"Invalid or expired enrollment token"` | Token expired or `IsActive=false` | Create a new token; check `MaxDevices > 0` and `ExpiresOn` |
| `"Enrollment token has no remaining device slots"` | `MaxDevices` hit 0 | Create a new token with a higher `MaxDevices` |
| `pushTokens: []` in GET /devices | `GetAllAsync` doesn't `.Include(PushTokens)` | Expected — `PushService` queries `DevicePushTokens` directly; the display is cosmetic |
| `"Neither Firebase ServiceAccountPath nor ServerKey configured"` | `firebase-sa.json` missing or mounted as directory | See §7 Docker pitfall |
| `FCM HTTP v1 send failed: 401` | Service account lacks FCM permissions | Grant `Firebase Cloud Messaging API` role in GCP IAM |
| `status: 3` (Failed) immediately after command | FCM push failed | Check `docker logs sdm-api 2>&1 \| grep -A3 PushService` |
| Screen does not lock despite `status: 1` (Sent) | Device Admin not active | Tap "Enable Device Admin" in the agent app |
| `LockScreen` logs `SecurityException` | `dpm.isAdminActive` returns false | Grant Device Admin (see above) |
| FCM token not updating after reinstall | `onNewToken` requires a saved JWT | Re-enroll the device; `onNewToken` skips upload if no JWT is stored |

---

## 12. API Reference

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/auth/login` | None | Get user JWT |
| POST | `/api/enrollment/tokens` | Admin JWT | Create enrollment token |
| POST | `/api/enrollment/tokens/generate-qr` | Admin JWT | Create token + QR image |
| POST | `/api/devices/register-with-token` | None | Enroll device |
| GET | `/api/devices` | None | List all devices |
| GET | `/api/devices/{id}` | JWT | Get device by ID |
| DELETE | `/api/devices/{id}` | None* | Delete/unenroll device |
| POST | `/api/devices/{id}/heartbeat` | Device JWT | Update battery + lastSeen |
| POST | `/api/devices/{id}/push-token` | Device JWT | Register FCM token |
| POST | `/api/devices/update-fcm-token` | Device JWT | Refresh FCM token |
| POST | `/api/devices/{id}/commands` | JWT | Send command to device |
| POST | `/api/devices/{id}/commands/{cid}/status` | Device JWT | Report command result |

\* `[AllowAnonymous]` for dev convenience — restrict before production.

---

## 13. Onboarding Checklist

- [ ] Clone repo, open `SDM.slnx` in Visual Studio
- [ ] Run `docker compose up -d` (Postgres + pgAdmin)
- [ ] Apply EF migrations (`dotnet ef database update ...`)
- [ ] Place `firebase-sa.json` in `SDM.API/`
- [ ] Start API (F5 or `dotnet run`)
- [ ] Create enrollment token via Swagger (`POST /api/enrollment/tokens`)
- [ ] Get QR via `POST /api/enrollment/tokens/generate-qr`
- [ ] Install the Android agent APK (from `samples/android-agent/`)
- [ ] Tap "Enable Device Admin" in the app
- [ ] Scan QR — verify device appears in `GET /api/devices`
- [ ] Send `POST /api/devices/{id}/commands` with `"commandType": "LockScreen"` — screen should lock

---

## 14. Production Hardening Checklist

- [ ] Add admin role requirement to `DELETE /api/devices/{id}`
- [ ] Restrict `GET /api/devices` to authenticated users
- [ ] Enable HTTPS only; remove HTTP redirect exception
- [ ] Rotate JWT signing key and store in Key Vault / environment secret
- [ ] Move Hangfire dashboard behind admin auth
- [ ] Enable device heartbeat timeout detection (mark device offline if `LastSeen > N minutes`)
- [ ] Add RBAC for enrollment token creation
- [ ] Add integration tests using `WebApplicationFactory`
- [ ] Set up log aggregation (e.g. Application Insights, Seq)
