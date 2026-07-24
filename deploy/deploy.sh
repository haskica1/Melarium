#!/usr/bin/env bash
# Redeploy Melarium on the VPS: pull latest code, rebuild the backend container,
# rebuild the frontend, and reload nginx. Run from the repo root on the server:
#   ./deploy/deploy.sh
set -euo pipefail

FRONTEND_TARGET=/var/www/melarium/frontend

git pull

docker compose build api
docker compose up -d

cd frontend
npm ci
npm run build
cd ..

sudo rsync -a --delete frontend/dist/ "$FRONTEND_TARGET/"
sudo systemctl reload nginx

echo "Deploy gotov."
