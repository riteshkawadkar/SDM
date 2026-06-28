# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy solution files and restore
COPY . ./
RUN dotnet restore "SDM.API/SDM.API.csproj"

# publish
RUN dotnet publish "SDM.API/SDM.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install pg_isready (postgres client) to wait for DB readiness
RUN apt-get update && apt-get install -y postgresql-client && rm -rf /var/lib/apt/lists/*

# copy publish output
COPY --from=build /app/publish ./

# copy entrypoint script
COPY SDM.API/entrypoint.sh ./entrypoint.sh
RUN chmod +x ./entrypoint.sh

ENV ASPNETCORE_URLS="http://+:5254"

EXPOSE 5254
ENTRYPOINT ["/app/entrypoint.sh"]
