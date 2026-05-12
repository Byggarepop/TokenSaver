# Deploying TokenSaverViewer to a Raspberry Pi

The viewer hosts both the public stats page and the ingest API on a single
port. SQLite stores all reports in a single file next to the binary. The
whole thing fits in ~50 MB and idles under 100 MB of RAM.

## Architecture

```
[Windows / macOS / Linux clients running roslyn-lean CLI or MCP]
        │  TOKENSAVER_API_URL=http://your-pi.local:5100
        │  HTTPS POST /api/reports
        ▼
┌──────────────────── Raspberry Pi ────────────────────┐
│  systemd: tokensaver-viewer.service                  │
│    dotnet TokenSaverViewer.dll  →  port 5100         │
│    SQLite: /var/lib/tokensaver/tokensaver.db         │
│                                                      │
│  Public:   http://pi.local:5100/         (Blazor UI) │
│  Public:   http://pi.local:5100/api/...  (ingest)    │
└──────────────────────────────────────────────────────┘
```

## Step 1 — Publish for the Pi

From the dev box (Windows):

```powershell
# Pi OS 64-bit (recommended — Pi 3/4/5)
dotnet publish TokenSaverViewer/TokenSaverViewer.csproj `
    -c Release -r linux-arm64 --self-contained false `
    -o publish/pi

# Pi OS 32-bit / older Pi
dotnet publish TokenSaverViewer/TokenSaverViewer.csproj `
    -c Release -r linux-arm --self-contained false `
    -o publish/pi
```

`--self-contained false` keeps the bundle small; you'll install the ASP.NET
Core 10 runtime on the Pi once and reuse it across deploys.

## Step 2 — Install the runtime on the Pi

SSH in, then:

```bash
# .NET 10 runtime (use the official Microsoft install script).
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
sudo ./dotnet-install.sh --runtime aspnetcore --channel 10.0 --install-dir /usr/share/dotnet
sudo ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
dotnet --info  # should list "Microsoft.AspNetCore.App 10.x"
```

## Step 3 — Copy and lay out files

```bash
sudo mkdir -p /opt/tokensaver /var/lib/tokensaver
sudo chown -R pi:pi /opt/tokensaver /var/lib/tokensaver
# From your dev box:
scp -r publish/pi/* pi@pi.local:/opt/tokensaver/
```

Add an `appsettings.Production.json` next to `TokenSaverViewer.dll`:

```json
{
  "TokenSaver": {
    "Mode": "server",
    "DbPath": "/var/lib/tokensaver/tokensaver.db"
  },
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://0.0.0.0:5100" }
    }
  }
}
```

`Mode: "server"` makes the Blazor page read from the DB instead of the
local JSON file. `DbPath` puts the database in `/var/lib` so it survives
re-deploys.

## Step 4 — systemd unit

`/etc/systemd/system/tokensaver-viewer.service`:

```ini
[Unit]
Description=TokenSaver Viewer + Ingest API
After=network.target

[Service]
WorkingDirectory=/opt/tokensaver
ExecStart=/usr/local/bin/dotnet /opt/tokensaver/TokenSaverViewer.dll
Restart=always
RestartSec=10
User=pi
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

Then:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now tokensaver-viewer
sudo systemctl status tokensaver-viewer
journalctl -u tokensaver-viewer -f       # live logs
```

## Step 5 — Point clients at it

On any machine running the CLI or MCP server, set:

```
TOKENSAVER_API_URL = http://pi.local:5100
```

(System-wide on Windows: `setx TOKENSAVER_API_URL http://pi.local:5100`,
then restart Claude Code / the shell.)

Verify:

```bash
curl http://pi.local:5100/healthz
curl http://pi.local:5100/api/stats/summary
```

Browse `http://pi.local:5100/` for the live page. It auto-refreshes every
30 seconds.

## Backups

The whole dataset is one file:

```bash
sqlite3 /var/lib/tokensaver/tokensaver.db ".backup '/var/lib/tokensaver/backup-$(date +%F).db'"
```

Stick that line in a daily cron job and you're done.

## Optional: HTTPS

Two common routes:

- **Cloudflare Tunnel** — `cloudflared tunnel` exposes the Pi at a public
  HTTPS hostname without opening ports on your router. Simplest if you
  want this reachable from anywhere.
- **Caddy in front of Kestrel** — `caddy reverse_proxy localhost:5100`
  on the Pi handles Let's Encrypt automatically. Needs port 80/443 open.

For LAN-only use, plain HTTP on port 5100 is fine — the data isn't
sensitive (tokens-saved counts) and rate-limiting is already on by default.
