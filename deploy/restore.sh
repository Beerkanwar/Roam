#!/usr/bin/env bash
# Restores a backup made by backup.sh. This REPLACES the current database contents.
# Usage:  ./restore.sh backups/roam-YYYYMMDD-HHMMSS.sql.gz
set -euo pipefail

cd "$(dirname "$0")"
backup="${1:-}"
if [ -z "$backup" ] || [ ! -f "$backup" ]; then
  echo "Usage: ./restore.sh backups/roam-YYYYMMDD-HHMMSS.sql.gz"
  exit 1
fi

read -r -p "This replaces ALL current Roam data with $backup. Type RESTORE to continue: " confirm
[ "$confirm" = "RESTORE" ] || { echo "Cancelled."; exit 1; }

docker compose stop api web
gunzip -c "$backup" | docker compose exec -T db sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' 
docker compose up -d
echo "Restored $backup"
