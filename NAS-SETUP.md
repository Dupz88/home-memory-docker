# Home Memory — Synology NAS Docker Setup

Run Home Memory as an always-on MCP server on your Synology NAS, accessible from Claude Desktop on any device on your local network.

This is a fork of [impactjo/home-memory](https://github.com/impactjo/home-memory) with the following changes to support Docker on Linux/NAS:

| File | Change |
|------|--------|
| `HomeMemoryMCP/Program.cs` | Added HTTP transport mode alongside original stdio |
| `HomeMemoryMCP/HomeMemoryMCP.csproj` | Switched to `Microsoft.NET.Sdk.Web`; added `ModelContextProtocol.AspNetCore` |
| `HomeMemoryMCP/SeedData/template.scd` | Upgraded from Firebird 3.0 (ODS 12) to Firebird 4.0 (ODS 13) format |
| `Dockerfile` | New — builds on Linux with Firebird 4.0 compatibility |
| `docker-compose.yaml` | New — volume mounts and environment for NAS deployment |
| `entrypoint.sh` | New — sets Firebird environment variables before dotnet starts |

---

## Prerequisites

- Synology NAS with Docker/Container Manager installed
- SSH access to your NAS
- Node.js installed on your PC (for Claude Desktop connection)

---

## Step 1 — Find your Docker volume path

Synology NAS models use different volume names. SSH in and find yours:

```bash
ls /volume1/ /volume2/ /volume3/ 2>/dev/null
```

Look for your Docker folder — it's typically `/volume1/docker` or `/volume2/docker`. Use that path in the steps below, replacing `YOUR_DOCKER_PATH`.

---

## Step 2 — Extract Firebird 4.0 to the NAS

Home Memory requires Firebird 4.0 Embedded. Run these commands over SSH:

```bash
# Download Firebird 4.0
cd /tmp
wget -q --no-check-certificate \
  "https://github.com/FirebirdSQL/firebird/releases/download/v4.0.5/Firebird-4.0.5.3140-0.amd64.tar.gz" \
  -O fb4.tar.gz

# Extract
mkdir -p fb4outer
tar -xzf fb4.tar.gz -C fb4outer --strip-components=1
tar -xzf fb4outer/buildroot.tar.gz -C /tmp

# Copy to permanent location (replace YOUR_DOCKER_PATH)
mkdir -p YOUR_DOCKER_PATH/home-memory/firebird
cp -r /tmp/opt/firebird/* YOUR_DOCKER_PATH/home-memory/firebird/

# Add RootDirectory to Firebird config
echo "RootDirectory = /opt/firebird" >> YOUR_DOCKER_PATH/home-memory/firebird/firebird.conf

# Create data directory
mkdir -p YOUR_DOCKER_PATH/home-memory/data
```

---

## Step 3 — Configure docker-compose.yaml

Edit `docker-compose.yaml` and update the volume paths and timezone to match your setup:

```yaml
volumes:
  - YOUR_DOCKER_PATH/home-memory/data:/data
  - YOUR_DOCKER_PATH/home-memory/firebird:/opt/firebird

environment:
  - TZ=Your/Timezone   # e.g. Africa/Johannesburg, Europe/London, America/New_York
```

---

## Step 4 — Copy repo to NAS and build

Copy this repo folder to your NAS projects directory via SMB or SCP, then:

```bash
cd YOUR_DOCKER_PATH/projects/home-memory-compose
docker-compose up -d --build
```

The first build takes a few minutes. Verify with:

```bash
docker logs home-memory
```

You should see:
```
[HomeMemory] Home Memory 0.2.0 starting...
[HomeMemory] Listening on http://0.0.0.0:5100/mcp
```

---

## Step 5 — Connect Claude Desktop

Node.js is required on your PC. Edit your Claude Desktop config (`Claude menu → Settings → Developer → Edit Config`):

```json
{
  "mcpServers": {
    "home-memory": {
      "command": "npx",
      "args": [
        "-y",
        "mcp-remote",
        "http://YOUR-NAS-IP:5100/mcp",
        "--transport",
        "http-first",
        "--allow-http"
      ]
    }
  }
}
```

Replace `YOUR-NAS-IP` with your NAS's local IP address. Restart Claude Desktop.

---

## Database Backup

Your database lives at `YOUR_DOCKER_PATH/home-memory/data/homememory.scd`. Back it up with:

```bash
cp YOUR_DOCKER_PATH/home-memory/data/homememory.scd \
   YOUR_DOCKER_PATH/home-memory/data/homememory-backup-$(date +%Y%m%d).scd
```

---

## Updating

```bash
cd YOUR_DOCKER_PATH/projects/home-memory-compose
git pull
docker-compose up -d --build
```

Your database is unaffected by rebuilds.

---

## Troubleshooting

**Container keeps restarting:**
```bash
docker logs home-memory
```

**Port 5100 already in use:** Change the left side of `5100:5100` in `docker-compose.yaml` and update the Claude Desktop config URL to match.

**Firebird not found:** Verify `YOUR_DOCKER_PATH/home-memory/firebird/lib/libfbclient.so.2` exists.

**Claude Desktop shows "Server disconnected":** Ensure Node.js is installed and `--allow-http` is in the args (required for local HTTP connections).
