# SecureDeviceManager (SDM) - Backend

Phase 1 backend for SDM. This repository contains several projects: API, Application, Domain, Infrastructure.

Quick start (development):

1. Start Postgres and pgAdmin using Docker Compose:

   docker compose up -d

2. Run EF Core migrations (from repo root):

   dotnet ef database update --project "SDM.Infrastructure/SDM.Infrastructure.csproj" --startup-project "SDM.API/SDM.API.csproj"

3. Start the API in Development:

   $Env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run --project "SDM.API/SDM.API.csproj"

4. API endpoints:

   - POST /api/devices/register
   - POST /api/devices/{id}/push-token
   - POST /api/devices/{id}/heartbeat
   - POST /api/devices/{id}/commands

5. Hangfire dashboard (development): http://localhost:{api_port}/hangfire

Notes:
- Configure Firebase ServerKey in SDM.API/appsettings.Development.json or via environment: Firebase:ServerKey
- JWT settings are required for authentication (Jwt section in appsettings).
