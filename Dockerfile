# ── Stage 1: Build ────────────────────────────────────────────────────────────
# Uses the full .NET SDK to restore, build, and publish the Server project.
# Only the three projects that Server depends on are copied — Client/Tests are
# not needed and are intentionally excluded to keep the build context small.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy .csproj files first so Docker layer-caches the restore step.
# A change to source code (not .csproj) will NOT invalidate the restore layer.
COPY BaseLibrary/BaseLibrary.csproj       BaseLibrary/
COPY ServerLibrary/ServerLibrary.csproj   ServerLibrary/
COPY Server/Server.csproj                 Server/

RUN dotnet restore Server/Server.csproj

# Copy the rest of the source after restore to leverage the cache above
COPY BaseLibrary/   BaseLibrary/
COPY ServerLibrary/ ServerLibrary/
COPY Server/        Server/

# Publish to /app/publish (Release, self-contained=false, no-restore)
RUN dotnet publish Server/Server.csproj \
        -c Release \
        -o /app/publish \
        --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
# Uses the leaner ASP.NET runtime image — no SDK, smaller attack surface.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# The API listens on HTTP 8080 inside the container.
# HTTPS is terminated at the host / reverse-proxy level when needed.
EXPOSE 8080

ENTRYPOINT ["dotnet", "Server.dll"]
