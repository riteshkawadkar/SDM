# SecureDeviceManager (SDM) — Developer Understanding Guide

This document is intended to help a new developer quickly understand, run, debug, and extend the SecureDeviceManager backend. It covers repository layout, the technology choices and rationale, the domain model and entity relationships, high-level architecture, agent communication flows (enrollment, heartbeat, push tokens), operational and debugging steps (including using Visual Studio 2026 and ngrok), and recommended next steps for contributors.

---

## 1. Project overview

SecureDeviceManager (SDM) is a backend platform for enrolling and managing mobile devices (agents). It provides:
- Enrollment token generation (QR / deep link)
- Device registration using enrollment tokens
- Heartbeat ingestion
- Push token management (FCM)
- Background processing of commands via Hangfire

The backend is organized as multiple projects (logical layers):
- SDM.API — ASP.NET Minimal / Controller-based Web API (entry point)
- SDM.Infrastructure — Implementations of services, data access (EF Core), Hangfire jobs
- SDM.Application — DTOs, interface definitions and cross-cutting application services
- SDM.Domain — domain entities (Device, EnrollmentToken, User, Role, AuditLog, etc.)

This layering follows clean architecture principles: controllers depend on application interfaces, implementations live in Infrastructure, and domain models are centralized in Domain.

## 2. Tech stack and why it was chosen

- .NET 8 (C#): Modern, high-performance, supported LTS for building scalable web APIs and background services.
- ASP.NET Web API (Controllers): Straightforward mapping of REST endpoints and MVC-style controllers for HTML rendering during development (enroll form).
- Entity Framework Core + Npgsql (PostgreSQL): Familiar ORM, migrations, transactional DB access, and PostgreSQL as a robust production database.
- Hangfire: Background job scheduling and execution for recurring or deferred tasks (processing pending commands, etc.). Works with Postgres storage.
- JWT (System.IdentityModel.Tokens / Microsoft.AspNetCore.Authentication.JwtBearer): Lightweight token-based authentication for device/user tokens.
- QRCoder: Generate QR codes for enrollment payloads.
- FCM (Firebase Cloud Messaging) — implied via push token management: used to deliver push notifications to devices.
- ngrok (dev): Expose local server to device over the internet for testing without deploying.

Rationale: each component is industry-standard for its role (ORM, background jobs, authentication) and integrates well with .NET.

## 3. Code structure and key files

- SDM.API/
  - Program.cs — application bootstrapping (services, authentication, Hangfire, MapControllers)
  - Controllers/EnrollmentController.cs — developer-facing enrollment pages and token QR endpoints
  - Properties/launchSettings.json — profiles for local debugging (http, https, IIS Express)

- SDM.Infrastructure/
  - Services/DeviceService.cs — core device registration, heartbeat, push token logic
  - Services/PushService.cs, CommandService.cs, HangfireJobs.cs — background and push related logic (services referenced in Program.cs)
  - Data/ApplicationDbContext.cs — EF Core DbContext and DbSets (entities)

- SDM.Application/
  - DTOs/* — request/response DTOs (DeviceRegisterWithTokenRequest, Enrollment DTOs)
  - Interfaces/* — service interfaces (IDeviceService, IAuthService, IJwtTokenGenerator)

- SDM.Domain/
  - Entities/* — domain entities (Device, EnrollmentToken, DevicePushToken, DeviceHeartbeat, AuditLog, User, Role)

If you need start points: set breakpoints in Program.cs, EnrollmentController.EnrollForm (dev HTML), and DeviceService.RegisterWithTokenAsync.

## 4. Domain model (ERD / relationships)

This is a textual ERD that describes principal entities and relations (implementations live in SDM.Domain.Entities):

- Device
  - Id (PK), DeviceIdentifier (unique), SerialNumber, Manufacturer, Model, AndroidVersion, Status, CreatedOn, UpdatedOn, LastSeen, BatteryLevel
  - Relationships:
	- 1 Device -> * DevicePushToken
	- 1 Device -> * DeviceHeartbeat

- EnrollmentToken
  - Id (PK), Token (string), ExpiresOn (UTC), MaxDevices (int), IsActive (bool)
  - Used to gate enrollment operations. Token.MaxDevices is decremented on device register.

- DevicePushToken
  - Id, DeviceId (FK), Token (FCM token), IsActive, CreatedOn

- DeviceHeartbeat
  - Id, DeviceId (FK), BatteryLevel, FreeStorage, Latitude, Longitude, CreatedOn

- AuditLog
  - Id, Action, EntityName, EntityId, NewValue (JSON), Timestamp

- User and Role
  - Minimal user model used to generate JWTs; device-specific pseudo-user objects are created in memory when generating a device JWT (not persisted).

Notes on constraints and integrity:
- EnrollmentToken.MaxDevices controls how many devices can use the token. When it reaches 0, IsActive is set to false.
- DeviceIdentifier should be unique — used to detect existing device registrations and update metadata.

## 5. High-level architecture (HLD)

Components:
- API Layer (SDM.API)
  - Exposes endpoints for: token generation, QR generation, enrollment form (dev), device registration endpoints (via controllers), heartbeats, push token registration and any management operations.

- Services Layer (SDM.Infrastructure)
  - Business logic for registering devices, validating tokens, saving heartbeats, registering push tokens, creating audit logs, and generating JWTs.

- Data Layer (Postgres via EF Core)
  - Persistent storage for devices, tokens, audit logs, jobs, etc.

- Background Jobs (Hangfire)
  - Recurring tasks (e.g., process pending commands, cleanup tasks).

- Agents (Android / iOS)
  - Mobile/agent apps or device management clients that:
	- Initiate enrollment using QR / deep link that includes enrollment token
	- POST registration payloads to API endpoints (register-with-token)
	- Periodically POST heartbeats
	- Register FCM token for push notifications

Flows:
- Enrollment flow (high-level):
  1. Admin creates an enrollment token via POST /api/enrollment/tokens (or uses test QR endpoint).
  2. Token is delivered to device via QR or deep link (payload: sdm://enroll?token=... or a JSON payload for test QR).
  3. Device opens enroll UI and posts DeviceRegisterWithTokenRequest to POST /api/devices/register-with-token.
  4. Server validates token, creates/updates Device record, registers FCM token if supplied, decrements token usage and returns Device JWT.

- Heartbeat and command flows: devices POST heartbeats; server records DeviceHeartbeat rows and may queue commands for agents. Hangfire background jobs may process queued commands and send push notifications via FCM using recorded push tokens.

## 6. Agent communication details

Endpoints of interest:
- POST /api/enrollment/tokens — create tokens (controller: EnrollmentController)
- POST /api/enrollment/tokens/generate-qr/test — dev QR endpoint returns HTML with token and QR (allow anonymous)
- GET /enroll?token=... — quickly renders a small HTML form for manual testing (AllowAnonymous)

Missing or incomplete pieces to validate before manual testing:
- POST /api/devices/register-with-token — The server logic exists (DeviceService.RegisterWithTokenAsync), but verify there is a controller action exposing this route. If missing, implement a DevicesController with an [AllowAnonymous] POST endpoint that accepts DeviceRegisterWithTokenRequest and calls IDeviceService.RegisterWithTokenAsync.

Payload shape for registration (DeviceRegisterWithTokenRequest):
- Token: string
- DeviceIdentifier: string
- SerialNumber: string
- Manufacturer, Model, AndroidVersion: string
- Optional FcmToken: string

Returned payload (DeviceRegisterWithTokenResponse):
- DeviceId: GUID
- DeviceJwt: string
- ExpiresInSeconds: int

Security considerations:
- Enrollment endpoints used during initial registration should allow anonymous access (device has no JWT yet) but must strictly validate enrollment tokens.
- Ensure HTTPS in production; for development ngrok can provide secure tunnels.

## 7. How to run & debug (Visual Studio 2026)

1. Open the solution: D:\Users\rites\source\repos\SecureDeviceManager\src\backend\SDM\SDM.slnx
2. Set the startup project to SDM.API (right-click > Set as Startup Project).
3. Select the launch profile (in the Visual Studio toolbar): choose the "https" profile from SDM.API/Properties/launchSettings.json. That profile exposes https://localhost:7288 and http://localhost:5254.
4. Place breakpoints in relevant files (Program.cs, EnrollmentController.cs, DeviceService.cs).
5. Press F5 (Start Debugging). Visual Studio will build, run the app, and attach the debugger.

Notes for ngrok testing:
- Run ngrok forwarding to the same local port the app is listening on. Example (PowerShell):
  - ngrok http 5254
  - or for HTTPS endpoint: ngrok http 7288 --host-header="localhost:7288"
- Use the ngrok public URL for the QR deep link or open that URL on the device: https://<your-ngrok-id>.ngrok-free.dev/enroll?token=...

If you need to expose on port 80 (your provided command), either change applicationUrl to use port 80 (requires admin) or run a local reverse proxy to map 5254→80.

## 8. Common troubleshooting and investigation steps

- If the device POST to /api/devices/register-with-token returns 404:
  - Verify a controller action is mapped for that route.
  - Check Program.cs that MapControllers() is enabled (it is).
  - Inspect app console logs for routing failures.

- If request returns 401:
  - Ensure the devices endpoint allows anonymous access during enrollment. Add [AllowAnonymous] to the controller action.

- If request returns 500 or exception:
  - Attach debugger and reproduce; the exception stack will reveal root cause (DB connectivity, model binding issues, null refs).
  - Check database rows for EnrollmentTokens (ExpiresOn, IsActive, MaxDevices) to ensure token is valid.

- If token validation fails:
  - Confirm time skew (server UTC vs device). ExpiresOn is compared to DateTime.UtcNow.
  - Confirm token string matches exactly (HTML encoding, double-encoding issues when building deep link).

- If device post reaches server but DeviceService throws "Enrollment token has no remaining device slots":
  - Check token.MaxDevices value in DB. For test flows, create tokens with MaxDevices >= 1.

## 9. Recommended immediate fixes / improvements

1. Verify existence of DevicesController exposing POST /api/devices/register-with-token. If missing, add it and mark endpoint [AllowAnonymous].
2. Add structured logging (ILogger<T>) in controllers and DeviceService to capture enrollment attempts, token values (redact token in logs), and exceptions.
3. Add integration tests for the enrollment flow using WebApplicationFactory (Program partial is exposed) to simulate token creation and device registration.
4. Enable CORS only for development if the enrollment form is hosted on a different origin (ngrok vs localhost).
5. Add database migration and seeding scripts for Roles (Viewer) referenced when generating device pseudo-user claims.

## 10. Onboarding checklist for a new developer (step-by-step)

1. Pull the repository and open solution in Visual Studio 2026.
2. Run the database locally (Postgres) and set connection string in appsettings.Development.json (DefaultConnection).
3. Run EF Core migrations: dotnet ef database update (ensure correct startup project and tools installed).
4. Set SDM.API as startup; pick https profile and start debugging (F5).
5. Create an enrollment token via Swagger or curl and use the test QR endpoint to view token and QR.
6. Start ngrok pointed at 5254 (or 7288) and open the enroll page on your test device using the ngrok URL.
7. Click enroll on the device; observe server logs and breakpoints.

## 11. Long-term / architectural suggestions

- Add a dedicated DevicesController that surfaces all device-related endpoints in one place.
- Add API versioning and a brief OpenAPI documentation for production.
- Move QR and test endpoints behind a development-only feature flag so production does not expose dev utilities.
- Add RBAC for enrollment token creation and management (only admins).
- Harden token generation (token entropy) and optionally allow single-use tokens.

## 12. Useful commands and snippets

- Start dev server (Visual Studio): F5
- Create token (curl):
  curl -k -X POST https://localhost:7288/api/enrollment/tokens -H "Content-Type: application/json" -d '{"ExpiresInMinutes":30,"MaxDevices":1}'
- Start ngrok:
  ngrok http 5254
- adb deep link (open app or open browser):
  adb shell am start -a android.intent.action.VIEW -d "https://<ngrok-domain>/enroll?token=THE_TOKEN"

---

If you want, I can:
- Add a DevicesController skeleton and wire up the POST /api/devices/register-with-token endpoint (AllowAnonymous) so enrollment flow is complete.
- Add logging statements and a small integration test that proves enrollment works end-to-end in the test environment.

Save location: docs/DEVELOPER_GUIDE.md
