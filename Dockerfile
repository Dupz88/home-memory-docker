# ─── Build stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HomeMemoryMCP/HomeMemoryMCP.csproj HomeMemoryMCP/
RUN dotnet restore HomeMemoryMCP/HomeMemoryMCP.csproj

COPY HomeMemoryMCP/ HomeMemoryMCP/
RUN dotnet publish HomeMemoryMCP/HomeMemoryMCP.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── Runtime stage ───────────────────────────────────────────────────────────
# Firebird 4.0 is mounted as a volume from the NAS host at /opt/firebird
# See NAS-SETUP.md for setup instructions
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# libtommath1 + symlink to .so.0 required by Firebird 4.0 on Ubuntu Noble
RUN apt-get update && apt-get install -y --no-install-recommends \
    libtommath1 \
    && ln -s /usr/lib/x86_64-linux-gnu/libtommath.so.1 /usr/lib/x86_64-linux-gnu/libtommath.so.0 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY entrypoint.sh .
RUN chmod +x entrypoint.sh && mkdir -p /data /opt/firebird

ENV HOME_MEMORY_DB_PATH=/data/homememory.scd
ENV HOME_MEMORY_TRANSPORT=http
ENV HOME_MEMORY_PORT=5100

EXPOSE 5100
ENTRYPOINT ["/app/entrypoint.sh"]
