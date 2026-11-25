#!/usr/bin/env bash
# Creates deploy/.env for the production stack: asks a few questions and generates every secret.
# Run once from the deploy folder:  ./setup.sh
# It never overwrites an existing .env (that file holds the database password and encryption keys).
set -euo pipefail
# Git Bash on Windows would otherwise rewrite "/CN=..." below into a file path.
export MSYS_NO_PATHCONV=1

cd "$(dirname "$0")"

if [ -f .env ]; then
  echo "deploy/.env already exists, so nothing was changed."
  echo "Edit it with:  nano .env      (or delete it first if you really want to start over)"
  exit 1
fi

if ! command -v openssl >/dev/null 2>&1; then
  echo "openssl is required. On Ubuntu run:  sudo apt-get install -y openssl"
  exit 1
fi

ask() {
  # ask VAR "Question" "default"
  local answer
  if [ -n "${3:-}" ]; then
    read -r -p "$2 [$3]: " answer
    answer="${answer:-$3}"
  else
    read -r -p "$2: " answer
  fi
  printf -v "$1" '%s' "$answer"
}

random() { openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c "${1:-32}"; }

echo "== Roam production setup =="
echo "Press Enter to accept the value in [brackets]. You can change anything later in deploy/.env."
echo

ask PUBLIC_DOMAIN "Web address people will use, without https:// (e.g. roam.yourdomain.com)" ""
while [ -z "$PUBLIC_DOMAIN" ] || [[ "$PUBLIC_DOMAIN" == *"://"* ]] || [[ "$PUBLIC_DOMAIN" == *"/"* ]]; do
  echo "  Please enter just the domain, like roam.example.com"
  ask PUBLIC_DOMAIN "Web address" ""
done
ask OPERATOR_NAME "Your name or organisation (shown in the privacy policy and terms)" ""
ask CONTACT_EMAIL "Contact email for users (shown in the privacy policy)" ""
ask ADMIN_EMAIL "Email for the Roam administrator account" "$CONTACT_EMAIL"
ask CLOUDFLARE_TUNNEL_TOKEN "Cloudflare Tunnel token (paste it, or press Enter to add it later)" ""

ADMIN_PASSWORD="$(random 20)Aa1!"
POSTGRES_PASSWORD="$(random 32)"
CERT_PASSWORD="$(random 32)"

# Certificate that encrypts the sign-in keys stored in the database (valid 20 years).
# A relative folder works the same on Linux and Git Bash for Windows.
workdir="$(mktemp -d ./.setup-tmp.XXXXXX)"
trap 'rm -rf "$workdir"' EXIT
openssl req -x509 -newkey rsa:3072 -nodes -days 7300 -subj "/CN=roam-data-protection" \
  -keyout "$workdir/key.pem" -out "$workdir/cert.pem" >/dev/null 2>"$workdir/openssl.log"   || { cat "$workdir/openssl.log"; echo "Could not create the encryption certificate."; exit 1; }
openssl pkcs12 -export -inkey "$workdir/key.pem" -in "$workdir/cert.pem" \
  -passout "pass:$CERT_PASSWORD" -out "$workdir/dp.pfx" >/dev/null 2>"$workdir/openssl.log"   || { cat "$workdir/openssl.log"; echo "Could not package the encryption certificate."; exit 1; }
CERT_BASE64="$(base64 < "$workdir/dp.pfx" | tr -d '\n\r')"

PROFILES=""
[ -n "$CLOUDFLARE_TUNNEL_TOKEN" ] && PROFILES="tunnel"

umask 077
cat > .env <<EOF
# Roam production settings. KEEP THIS FILE PRIVATE and back it up somewhere safe:
# without it the database cannot be opened and everyone is signed out.

# ---- Site ----
PUBLIC_DOMAIN=$PUBLIC_DOMAIN
OPERATOR_NAME=$OPERATOR_NAME
CONTACT_EMAIL=$CONTACT_EMAIL

# ---- Cloudflare Tunnel (makes the site public) ----
CLOUDFLARE_TUNNEL_TOKEN=$CLOUDFLARE_TUNNEL_TOKEN
# "tunnel" starts the cloudflared container. Leave empty to run only on this machine.
COMPOSE_PROFILES=$PROFILES

# ---- Administrator (created on first start) ----
BOOTSTRAP_ADMIN_EMAIL=$ADMIN_EMAIL
BOOTSTRAP_ADMIN_PASSWORD=$ADMIN_PASSWORD
# Comma-separated emails of existing, confirmed accounts to approve as volunteers on start (optional).
BOOTSTRAP_VOLUNTEER_EMAILS=

# ---- Video relay (optional but recommended: Cloudflare Realtime TURN) ----
# Set TURN_PROVIDER=cloudflare and fill in both values from the Cloudflare dashboard.
TURN_PROVIDER=
CLOUDFLARE_TURN_KEY_ID=
CLOUDFLARE_TURN_API_TOKEN=

# ---- Email for password resets (optional, e.g. a Gmail app password) ----
SMTP_HOST=
SMTP_PORT=587
SMTP_USERNAME=
SMTP_PASSWORD=
SMTP_FROM_EMAIL=
SMTP_FROM_NAME=Roam

# ---- Map (optional; free OpenFreeMap tiles are used when empty) ----
MAPTILER_API_KEY=

# ---- Generated secrets: do not change after the first start ----
POSTGRES_USER=roam
POSTGRES_DB=roam
POSTGRES_PASSWORD=$POSTGRES_PASSWORD
DATA_PROTECTION_CERT_PASSWORD=$CERT_PASSWORD
DATA_PROTECTION_CERT_BASE64=$CERT_BASE64

# ---- Capacity ----
API_REPLICAS=2
WEB_PORT=8080
EOF

echo
echo "Created deploy/.env"
echo
echo "  Administrator email:    $ADMIN_EMAIL"
echo "  Administrator password: $ADMIN_PASSWORD"
echo
echo "Write the administrator password down now (it is also in deploy/.env)."
echo "Next:  docker compose up -d --build"
