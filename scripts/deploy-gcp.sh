#!/usr/bin/env bash
# Builds and deploys JBDS-GymApp to Google Cloud Run (.NET 10 API) + Firebase Hosting (Blazor WASM PWA).
#
# Required env vars:
#   GCP_PROJECT_ID   - Your Google Cloud / Firebase project ID
#   DB_CONNECTION    - Oracle MySQL connection string (from scripts/setup-gcp-mysql-vm.sh)
#   JWT_KEY          - 32+ char random signing key
#
# Optional env vars:
#   GCP_REGION       - Default: us-central1
#   STRIPE_SECRET_KEY / STRIPE_WEBHOOK_SECRET
#
# Usage:
#   GCP_PROJECT_ID="my-gym-proj" DB_CONNECTION="Server=..." JWT_KEY="..." bash scripts/deploy-gcp.sh

set -euo pipefail

PROJECT_ID="${GCP_PROJECT_ID:?Set GCP_PROJECT_ID}"
REGION="${GCP_REGION:-us-central1}"
SERVICE_NAME="${CLOUD_RUN_SERVICE:-jbds-gymapp-api}"
DB_CONNECTION="${DB_CONNECTION:?Set DB_CONNECTION to your Oracle MySQL connection string}"
JWT_KEY="${JWT_KEY:?Set JWT_KEY (min 32 chars)}"

echo "=== [1/4] Enabling Cloud Run & Cloud Build APIs ==="
gcloud services enable run.googleapis.com cloudbuild.googleapis.com artifactregistry.googleapis.com --project="$PROJECT_ID"

echo "=== [2/4] Deploying .NET 10 API to Google Cloud Run (${SERVICE_NAME} in ${REGION}) ==="
gcloud run deploy "$SERVICE_NAME" \
  --project="$PROJECT_ID" \
  --region="$REGION" \
  --source="." \
  --allow-unauthenticated \
  --min-instances=0 \
  --max-instances=2 \
  --memory=512Mi \
  --cpu=1 \
  --set-env-vars="ASPNETCORE_ENVIRONMENT=Production,Platform__AutoMigrateAndSeed=true,Jwt__Issuer=JBDS.Platform,Jwt__Audience=JBDS.GymApp,ConnectionStrings__DefaultConnection=${DB_CONNECTION},Jwt__Key=${JWT_KEY},Stripe__SecretKey=${STRIPE_SECRET_KEY:-},Stripe__WebhookSecret=${STRIPE_WEBHOOK_SECRET:-}"

echo "=== [3/4] Publishing .NET 10 Blazor WASM + PWA ==="
dotnet publish src/JBDS.GymApp.Client/JBDS.GymApp.Client.csproj -c Release -o publish_output -p:ContinuousIntegrationBuild=true

echo "=== [4/4] Deploying Blazor WASM PWA to Firebase Hosting ==="
npx --yes firebase-tools deploy --only hosting --project "$PROJECT_ID"

echo ""
echo "Deployment complete!"
echo "Open your Firebase Hosting URL: https://${PROJECT_ID}.web.app"
