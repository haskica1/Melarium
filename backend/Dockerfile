# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so the restore layer is cached
# (only re-runs when a .csproj changes, not on every code edit)
COPY Melarium.Domain/Melarium.Domain.csproj                 Melarium.Domain/
COPY Melarium.Application/Melarium.Application.csproj       Melarium.Application/
COPY Melarium.Entity/Melarium.Entity.csproj                 Melarium.Entity/
COPY Melarium.Infrastructure/Melarium.Infrastructure.csproj Melarium.Infrastructure/
COPY Melarium.API/Melarium.API.csproj                       Melarium.API/

RUN dotnet restore Melarium.API/Melarium.API.csproj

# Copy the rest of the source and publish
COPY . .
RUN dotnet publish Melarium.API/Melarium.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Non-root runtime user. 1654 is the `app` user baked into the .NET base images
# (also exposed there as $APP_UID); pinned explicitly so the build does not depend
# on that variable being present.
ARG APP_UID=1654

WORKDIR /app
COPY --from=build /app/publish .

# Photo storage (Storage__LocalPath) is a mounted volume. Creating it in the image, owned by the
# runtime user, means a *freshly created* named volume inherits that ownership.
# NOTE: an already-existing volume keeps its old root ownership — see docs/deployment.md for the
# one-time chown required when upgrading an existing deployment.
RUN mkdir -p /app/uploads && chown -R ${APP_UID}:${APP_UID} /app/uploads

USER ${APP_UID}

# Port 8080 (not 80) is required: a non-root process cannot bind privileged ports.
# docker-compose maps it to 127.0.0.1:5080 on the host, where nginx proxies to it.
# The shell-form entrypoint still honours $PORT at runtime for hosts that inject one.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet Melarium.API.dll"]
