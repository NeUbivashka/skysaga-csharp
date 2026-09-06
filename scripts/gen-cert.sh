#!/usr/bin/env bash
# Self-signed cert for the Caddy front door. Not committed (see .gitignore).
set -euo pipefail

cd "$(dirname "$0")/.."

openssl req -x509 -newkey rsa:2048 -nodes \
	-keyout server.key -out server.crt -days 3650 \
	-subj "/CN=skysaga.local" \
	-addext "subjectAltName=DNS:skysaga.local,DNS:localhost,IP:127.0.0.1"

echo "wrote server.crt / server.key"
