#!/usr/bin/env bash
# Updates Roam to the latest code: backs up the database, pulls, rebuilds and restarts.
set -euo pipefail

cd "$(dirname "$0")"
./backup.sh
git -C .. pull --ff-only
docker compose up -d --build
docker image prune -f >/dev/null
docker compose ps
