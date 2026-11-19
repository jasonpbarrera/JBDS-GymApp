#!/usr/bin/env bash
# Turnkey 1-command bootstrap for Oracle Cloud Infrastructure (OCI Always Free) Ubuntu/Debian VM.
# Installs Docker + Compose, clones jasonpbarrera/JBDS-GymApp, generates secure random secrets,
# starts Oracle MySQL 8.4 + .NET 10 ASP.NET Core API (+ optional Cloudflare Tunnel).
#
# Usage on your OCI Always Free VM (ARM64 Ampere A1 or AMD Micro):
#   curl -fsSL https://raw.githubusercontent.com/jasonpbarrera/JBDS-GymApp/main/scripts/setup-oci-backend.sh | bash
# Or with a Cloudflare Tunnel token:
#   CLOUDFLARE_TUNNEL_TOKEN="eyJ..." bash scripts/setup-oci-backend.sh

set -euo pipefail

APP_DIR="${APP_DIR:-$HOME/JBDS-GymApp}"
REPO_URL="${REPO_URL:-https://github.com/jasonpbarrera/JBDS-GymApp.git}"

echo "=== [1/4] Installing Docker & Docker Compose (if needed) ==="
if ! command -v docker >/dev/null 2>&1; then
  curl -fsSL https://get.docker.com | sudo sh
  sudo usermod -aG docker "$USER"
  echo "Docker installed. Note: you may need 'newgrp docker' if running non-root."
fi

echo "=== [2/4] Cloning or updating JBDS-GymApp in ${APP_DIR} ==="
if [ -d "$APP_DIR/.git" ]; then
  git -C "$APP_DIR" fetch origin && git -C "$APP_DIR" pull --ff-only origin main
else
  git clone "$REPO_URL" "$APP_DIR"
fi

cd "$APP_DIR"

echo "=== [3/4] Generating secure .env (if not present) ==="
if [ ! -f ".env" ]; then
  MYSQL_ROOT_PW="$(openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 24)!"
  MYSQL_APP_PW="$(openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 24)!"
  JWT_SECRET="$(openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 64)"

  cat > .env <<EOF
MYSQL_ROOT_PASSWORD=${MYSQL_ROOT_PW}
MYSQL_DATABASE=jbds_platform
MYSQL_USER=jbds_app
MYSQL_PASSWORD=${MYSQL_APP_PW}

ASPNETCORE_ENVIRONMENT=Production
JWT_KEY=${JWT_SECRET}
JWT_ISSUER=JBDS.Platform
JWT_AUDIENCE=JBDS.GymApp

STRIPE_SECRET_KEY=${STRIPE_SECRET_KEY:-}
STRIPE_WEBHOOK_SECRET=${STRIPE_WEBHOOK_SECRET:-}

CORS_ORIGIN_0=https://jbds-gymapp.jbdigitalservices.workers.dev
CORS_ORIGIN_1=https://gymapp.jbdigitalservices.net
CORS_ORIGIN_2=http://localhost:5002

CLOUDFLARE_TUNNEL_TOKEN=${CLOUDFLARE_TUNNEL_TOKEN:-}
EOF
  chmod 600 .env
  echo "Generated .env with random Oracle MySQL & JWT secrets."
fi

if [ -n "${CLOUDFLARE_TUNNEL_TOKEN:-}" ]; then
  echo "=== Enabling Cloudflare Tunnel service in docker-compose.override.yml ==="
  cat > docker-compose.override.yml <<EOF
services:
  cloudflared:
    image: cloudflare/cloudflared:latest
    container_name: jbds-gymapp-tunnel
    restart: unless-stopped
    command: tunnel --no-autoupdate run
    environment:
      TUNNEL_TOKEN: \${CLOUDFLARE_TUNNEL_TOKEN}
    depends_on:
      - api
EOF
fi

echo "=== [4/4] Starting Oracle MySQL 8.4 + .NET 10 API ==="
sudo docker compose up -d --build

echo ""
echo "JBDS-GymApp backend is starting!"
echo "Check health in ~20s: curl -s http://127.0.0.1:8080/api/health"
echo "View logs: sudo docker compose logs -f api"
