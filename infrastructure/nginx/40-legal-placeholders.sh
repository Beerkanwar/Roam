#!/bin/sh
# Fills OPERATOR_NAME / CONTACT_EMAIL in the privacy and terms pages from the container environment,
# so they are set in .env instead of by editing HTML. Runs at every container start (idempotent).
set -eu

html=/usr/share/nginx/html

# Keep only characters that are safe in both HTML text and a sed replacement.
clean() { printf '%s' "$1" | tr -d '<>"&\\/|' ; }

name=$(clean "${OPERATOR_NAME:-the Roam team}")
email=$(printf '%s' "${CONTACT_EMAIL:-}" | tr -cd 'A-Za-z0-9@._+-')

for page in "$html/privacy.html" "$html/terms.html"; do
  [ -f "$page" ] || continue
  sed -i "s|OPERATOR_NAME|$name|g; s|CONTACT_EMAIL|$email|g" "$page"
done
