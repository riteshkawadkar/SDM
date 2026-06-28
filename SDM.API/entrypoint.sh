#!/bin/bash

# Wait for Postgres to be ready
until pg_isready -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER"; do
  echo "Waiting for Postgres..."
  sleep 1
done

# Run migrations if needed (optional)
# dotnet ef database update --project /src/SDM.Infrastructure/SDM.Infrastructure.csproj --startup-project /src/SDM.API/SDM.API.csproj

exec dotnet SDM.API.dll --urls "http://+:5254"
