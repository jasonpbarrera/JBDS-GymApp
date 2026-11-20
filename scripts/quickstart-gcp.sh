#!/usr/bin/env bash
# All-in-one Google Cloud Shell quickstart for JBDS-GymApp (100% Always Free $0/mo):
#   1. Provisions Always Free e2-micro VM running Oracle MySQL 8.4 (us-central1-a)
#   2. Deploys .NET 10 ASP.NET Core API to Google Cloud Run (us-central1)
#   3. Builds .NET 10 Blazor WASM PWA (using Docker .NET 10 SDK if not installed locally)
#   4. Deploys Blazor WASM PWA to Firebase Hosting (with /api/** -> Cloud Run rewrite)
#
# Run inside Google Cloud Shell (https://shell.cloud.google.com):
#   git clone https://github.com/jasonpbarrera/JBDS-GymApp.git && cd JBDS-GymApp
#   bash scripts/quickstart-gcp.sh YOUR_GCP_PROJECT_ID

set -euo pipefail

PROJECT_ID="${1:-${GCP_PROJECT_ID:-$(gcloud config get-value project 2>/dev/null)}}"
REGION="${GCP_REGION:-us-central1}"
ZONE="${GCP_ZONE:-us-central1-a}"
VM_NAME="jbds-gymapp-mysql"
SERVICE_NAME="jbds-gymapp-api"

if [ -z "$PROJECT_ID" ] || [ "$PROJECT_ID" = "(unset)" ]; then
  echo "Usage: bash scripts/quickstart-gcp.sh YOUR_GCP_PROJECT_ID"
  exit 1
fi

gcloud config set project "$PROJECT_ID"

# Update .firebaserc with the target project ID
cat > .firebaserc <<EOF
{
  "projects": {
    "default": "${PROJECT_ID}"
  }
}
EOF

echo "=== [1/5] Enabling required Google Cloud APIs ==="
gcloud services enable \
  compute.googleapis.com \
  run.googleapis.com \
  cloudbuild.googleapis.com \
  artifactregistry.googleapis.com \
  firebase.googleapis.com \
  firebasehosting.googleapis.com \
  --project="$PROJECT_ID"

echo "=== [2/5] Provisioning Always Free Oracle MySQL 8.4 VM (${VM_NAME}) ==="
DB_PASS="$(openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 24)!"
ROOT_PASS="$(openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 24)!"
JWT_KEY="$(openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 64)"

if ! gcloud compute instances describe "$VM_NAME" --zone="$ZONE" --project="$PROJECT_ID" >/dev/null 2>&1; then
  GCP_PROJECT_ID="$PROJECT_ID" GCP_ZONE="$ZONE" MYSQL_PASSWORD="$DB_PASS" MYSQL_ROOT_PASSWORD="$ROOT_PASS" \
    bash scripts/setup-gcp-mysql-vm.sh
  echo "Waiting 90 seconds for Docker & Oracle MySQL 8.4 to finish first-time boot..."
  sleep 90
else
  echo "VM ${VM_NAME} already exists. Please pass DB_CONNECTION if re-deploying."
fi

VM_IP=$(gcloud compute instances describe "$VM_NAME" --project="$PROJECT_ID" --zone="$ZONE" --format="get(networkInterfaces[0].accessConfigs[0].natIP)")
DB_CONNECTION="${DB_CONNECTION:-Server=${VM_IP};Port=3306;Database=jbds_platform;User=jbds_app;Password=${DB_PASS};SslMode=Preferred;}"

echo "=== [3/5] Deploying .NET 10 API to Google Cloud Run (${SERVICE_NAME}) ==="
gcloud run deploy "$SERVICE_NAME" \
  --project="$PROJECT_ID" \
  --region="$REGION" \
  --source="." \
  --allow-unauthenticated \
  --min-instances=0 \
  --max-instances=2 \
  --memory=512Mi \
  --cpu=1 \
  --set-env-vars="ASPNETCORE_ENVIRONMENT=Production,Platform__AutoMigrateAndSeed=true,Jwt__Issuer=JBDS.Platform,Jwt__Audience=JBDS.GymApp,ConnectionStrings__DefaultConnection=${DB_CONNECTION},Jwt__Key=${JWT_KEY}"

echo "=== [4/5] Publishing .NET 10 Blazor WASM PWA ==="
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks | grep -q '^10\.'; then
  dotnet publish src/JBDS.GymApp.Client/JBDS.GymApp.Client.csproj -c Release -o publish_output -p:ContinuousIntegrationBuild=true
else
  echo "Using Docker mcr.microsoft.com/dotnet/sdk:10.0 to publish Blazor WASM PWA..."
  docker run --rm -v "$(pwd):/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
    dotnet publish src/JBDS.GymApp.Client/JBDS.GymApp.Client.csproj -c Release -o publish_output -p:ContinuousIntegrationBuild=true
fi

echo "=== [5/5] Deploying Blazor WASM PWA to Firebase Hosting ==="
npx --yes firebase-tools login --no-localhost || true
npx --yes firebase-tools deploy --only hosting --project "$PROJECT_ID"

echo ""
echo "=================================================================="
echo "JBDS-GymApp is LIVE on Google Cloud & Firebase!"
echo "App URL  : https://${PROJECT_ID}.web.app"
echo "Login    : platform@jbdigitalservices.net / Platform123!"
echo "MySQL IP : ${VM_IP}"
echo "=================================================================="
