#!/usr/bin/env bash
# Saves a compressed copy of the Roam database to deploy/backups/ and keeps the newest 14.
# Run by hand (./backup.sh) or daily from cron (see docs/PUBLISHING.md).
# Restore a backup with:  ./restore.sh backups/roam-YYYYMMDD-HHMMSS.sql.gz
set -euo pipefail

cd "$(dirname "$0")"
mkdir -p backups
file="backups/roam-$(date +%Y%m%d-%H%M%S).sql.gz"

# The database name and user are read inside the container, from its own environment.
docker compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists' \
  | gzip > "$file.partial"
mv "$file.partial" "$file"
chmod 600 "$file"

ls -1t backups/roam-*.sql.gz | tail -n +15 | xargs -r rm -f
echo "Saved $file"
