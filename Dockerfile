# ===========================================
# Dockerfile for InkVault ASP.NET Core
# Optimized for Render Deployment
# ===========================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY ["InkVault.csproj", "./"]
RUN dotnet restore "InkVault.csproj"

COPY . .
RUN dotnet publish "InkVault.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Render assigns PORT dynamically; default to 8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl --fail http://localhost:8080/ || exit 1

ENTRYPOINT ["dotnet", "InkVault.dll"]
