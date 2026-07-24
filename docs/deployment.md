# Deployment — Self-Hosted VPS (e.g. netcup)

Alternative to the Render/Vercel setup described in the [README](../README.md#deployment):
everything (PostgreSQL, backend, frontend) runs on a single VPS you control, behind nginx,
under one domain.

## Architecture

```
Internet
   │  DNS: A record @domain -> VPS public IP
   ▼
nginx (host, ports 80/443, Let's Encrypt TLS)
   ├── /              -> static files: frontend/dist (built React SPA)
   ├── /api/          -> proxy -> 127.0.0.1:5080 (Docker: api container, port 8080)
   ├── /swagger/, /health -> proxy -> 127.0.0.1:5080
   │
   └── Docker network
         ├── api container (Melarium.API, backend/Dockerfile)
         └── postgres container (data in a named volume)
```

Single domain, `/api` reverse-proxied on the same origin — the frontend calls the relative
`/api` path (its built-in default, see `apiClient.ts`), so no `VITE_API_URL` or CORS
configuration is needed for the production build.

Files that support this (repo root / `deploy/`):

| File | Purpose |
|---|---|
| `docker-compose.yml` | PostgreSQL + backend API containers |
| `.env.example` | Template — copy to `.env` on the server, fill in real secrets |
| `deploy/nginx.melarium.conf.example` | nginx site config (static frontend + `/api` proxy) |
| `deploy/deploy.sh` | Pulls latest code, rebuilds backend + frontend, reloads nginx |

## Prerequisites

- A netcup VPS running **Debian 12**, with root (or sudo) SSH access.
- A domain registered/managed at netcup (or elsewhere), able to edit DNS records.
- The VPS's public IPv4 (and IPv6, if assigned).

## 1. DNS

In the netcup CCP (domain → DNS zone):

- `A` record: `@` → VPS public IPv4.
- `AAAA` record (if the VPS has IPv6): `@` → VPS public IPv6.
- `A`/`CNAME` record: `www` → same IP / same apex domain.

DNS propagation can take from a few minutes up to a few hours. Check with:

```bash
dig +short melarium.app
```

## 2. Initial server setup

SSH in (netcup emails the initial root password), then:

```bash
apt update && apt upgrade -y

# Create a non-root sudo user instead of working as root day-to-day
adduser melarium
usermod -aG sudo melarium

# Firewall: only SSH, HTTP, HTTPS
apt install -y ufw
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable
```

Recommended hardening (not strictly required to get the app running, but do this before
going live): copy your SSH public key to `~melarium/.ssh/authorized_keys`, then disable
password login and root SSH login in `/etc/ssh/sshd_config`
(`PasswordAuthentication no`, `PermitRootLogin no`), and `systemctl restart sshd`.
Consider `apt install fail2ban` for the SSH port.

From here on, run commands as the `melarium` user, not root.

## 3. Install Docker

Follow Docker's official Debian instructions (apt repo, not the distro's own `docker.io`
package, which lags behind):

```bash
sudo apt install -y ca-certificates curl gnupg
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/debian/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin

sudo usermod -aG docker $USER
# log out and back in for the group change to take effect
docker compose version
```

## 4. Get the code and configure secrets

```bash
sudo mkdir -p /opt/melarium && sudo chown $USER:$USER /opt/melarium
git clone https://github.com/haskica1/Melarium.git /opt/melarium
cd /opt/melarium

cp .env.example .env
nano .env   # fill in POSTGRES_PASSWORD, DOMAIN, JWT_SECRET, SMTP_PASSWORD, GROQ_API_KEY,
            # SYSADMIN_EMAIL, SYSADMIN_PASSWORD — see comments in the file
```

Generate a JWT secret:

```bash
openssl rand -base64 48
```

`.env` is git-ignored — it never gets committed (repo rule: no secrets in the repo, see
`docs/CLAUDE.md`).

## 5. Start PostgreSQL + backend

```bash
docker compose build api
docker compose up -d
docker compose ps          # both containers should be "healthy"/"running"
curl http://127.0.0.1:5080/health
```

Migrations run automatically on startup (`Program.cs` calls `db.Database.MigrateAsync()`
unconditionally). In production it also locks the demo accounts and provisions the real
SystemAdmin from `SYSADMIN_EMAIL`/`SYSADMIN_PASSWORD` — no manual DB step needed.

## 6. Build and deploy the frontend

Static build — no Node.js server needed at runtime, nginx just serves the files. Install
Node.js 20+ first (not needed at runtime, only to run this build):

```bash
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt install -y nodejs
node --version   # v20.x
```

```bash
cd /opt/melarium/frontend
npm ci
npm run build              # outputs frontend/dist — do NOT set VITE_API_URL (defaults to /api)

sudo mkdir -p /var/www/melarium/frontend
sudo rsync -a --delete dist/ /var/www/melarium/frontend/
```

## 7. nginx + TLS

```bash
sudo apt install -y nginx
sudo cp /opt/melarium/deploy/nginx.melarium.conf.example /etc/nginx/sites-available/melarium
sudo ln -s /etc/nginx/sites-available/melarium /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl reload nginx
```

Confirm plain HTTP works (`http://melarium.app`), then get a certificate:

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d melarium.app -d www.melarium.app
```

certbot rewrites the nginx config to add the `listen 443 ssl;` block and the HTTP→HTTPS
redirect. Renewal is automatic via the `certbot.timer` systemd unit the Debian package
installs — verify with `systemctl status certbot.timer`.

## 8. Verify

- Open `https://melarium.app` — SPA loads, PWA manifest/icons work.
- Log in, exercise a core flow (create an apiary/beehive, run an inspection).
- `https://melarium.app/health` → `Healthy`.
- `https://melarium.app/swagger` → Swagger UI (remove the `/swagger/` location from the
  nginx config once you don't need it public anymore).

## 9. Backups

At minimum, back up the Postgres data and the uploads volume:

```bash
# Daily pg_dump, keep 14 days — add to `crontab -e`
0 3 * * * docker compose -f /opt/melarium/docker-compose.yml exec -T postgres \
  pg_dump -U melarium MelariumDB | gzip > /opt/backups/melarium-$(date +\%F).sql.gz \
  && find /opt/backups -name '*.sql.gz' -mtime +14 -delete
```

Copy backups off the VPS periodically (e.g. `rclone` to netcup Storage or any object
store) — a backup that only lives on the same disk as the database doesn't protect
against disk failure.

## 10. Redeploying updates

```bash
cd /opt/melarium
chmod +x deploy/deploy.sh   # once
./deploy/deploy.sh
```

This pulls the latest commit, rebuilds and restarts the `api` container, rebuilds the
frontend, syncs it to `/var/www/melarium/frontend`, and reloads nginx.
