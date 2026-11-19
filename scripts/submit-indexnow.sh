#!/usr/bin/env bash
set -euo pipefail

HOST="${1:-gymapp.jbdigitalservices.net}"
KEY="jbds-gymapp-indexnow-2026-key"

curl -fsS -X POST "https://api.indexnow.org/indexnow" \
  -H "Content-Type: application/json; charset=utf-8" \
  -d "{
    \"host\": \"${HOST}\",
    \"key\": \"${KEY}\",
    \"keyLocation\": \"https://${HOST}/${KEY}.txt\",
    \"urlList\": [
      \"https://${HOST}/\",
      \"https://${HOST}/login\"
    ]
  }"

echo "IndexNow ping submitted for https://${HOST}/"
