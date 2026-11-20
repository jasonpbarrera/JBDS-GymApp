#!/usr/bin/env bash
# Provisions a 100% Always Free ($0/mo) Google Compute Engine e2-micro VM running
# Oracle MySQL 8.4 (memory-tuned for 1 GB RAM + 2 GB swap) in us-central1-a.
#
# Prerequisites:
#   - Google Cloud CLI (`gcloud`) authenticated (`gcloud auth login`)
#   - Project selected (`gcloud config set project YOUR_PROJECT_ID`)
#
# Usage:
#   bash scripts/setup-gcp-mysql-vm.sh

set -euo pipefail

PROJECT_ID="${GCP_PROJECT_ID:-$(gcloud config get-value project 2>/dev/null)}"
ZONE="${GCP_ZONE:-us-central1-a}" # us-central1, us-west1, or us-east1 qualify for Always Free e2-micro
VM_NAME="${GCP_MYSQL_VM:-jbds-gymapp-mysql}"
DB_NAME="${MYSQL_DATABASE:-jbds_platform}"
DB_USER="${MYSQL_USER:-jbds_app}"
DB_PASS="${MYSQL_PASSWORD:-$(openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 24)!}"
ROOT_PASS="${MYSQL_ROOT_PASSWORD:-$(openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 24)!}"

if [ -z "$PROJECT_ID" ] || [ "$PROJECT_ID" = "(unset)" ]; then
  echo "ERROR: Set your GCP project first: gcloud config set project YOUR_PROJECT_ID"
  exit 1
fi

echo "=== [1/3] Enabling Compute Engine API on project ${PROJECT_ID} ==="
gcloud services enable compute.googleapis.com --project="$PROJECT_ID"

STARTUP_SCRIPT=$(cat <<EOF
#!/usr/bin/env bash
set -euo pipefail

# 1. Configure 2 GB swap so Oracle MySQL 8.4 runs smoothly on Always Free e2-micro (1 GB RAM)
if [ ! -f /swapfile ]; then
  fallocate -l 2G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

# 2. Install Docker
if ! command -v docker >/dev/null 2>&1; then
  curl -fsSL https://get.docker.com | sh
fi

# 3. Memory-tuned Oracle MySQL 8.4 configuration for e2-micro
mkdir -p /opt/jbds-mysql/conf.d /opt/jbds-mysql/data
cat > /opt/jbds-mysql/conf.d/low-memory.cnf <<'CNF'
[mysqld]
innodb_buffer_pool_size = 256M
innodb_log_file_size = 64M
max_connections = 60
performance_schema = OFF
table_open_cache = 200
tmp_table_size = 16M
max_heap_table_size = 16M
CNF

# 4. Run Oracle MySQL 8.4 container
if ! docker ps -a --format '{{.Names}}' | grep -q '^jbds-mysql$'; then
  docker run -d \
    --name jbds-mysql \
    --restart unless-stopped \
    -e MYSQL_ROOT_PASSWORD='${ROOT_PASS}' \
    -e MYSQL_DATABASE='${DB_NAME}' \
    -e MYSQL_USER='${DB_USER}' \
    -e MYSQL_PASSWORD='${DB_PASS}' \
    -p 3306:3306 \
    -v /opt/jbds-mysql/data:/var/lib/mysql \
    -v /opt/jbds-mysql/conf.d:/etc/mysql/conf.d \
    mysql:8.4
fi
EOF
)

echo "=== [2/3] Creating Always Free e2-micro VM (${VM_NAME}) in ${ZONE} ==="
gcloud compute instances create "$VM_NAME" \
  --project="$PROJECT_ID" \
  --zone="$ZONE" \
  --machine-type="e2-micro" \
  --boot-disk-size="30GB" \
  --boot-disk-type="pd-standard" \
  --image-family="ubuntu-2404-lts-amd64" \
  --image-project="ubuntu-os-cloud" \
  --tags="jbds-mysql" \
  --metadata="startup-script=${STARTUP_SCRIPT}"

echo "=== [3/3] Creating firewall rule for MySQL (port 3306) ==="
# Note: For production hardening, restrict --source-ranges or connect via Serverless VPC Access.
gcloud compute firewall-rules create allow-jbds-mysql \
  --project="$PROJECT_ID" \
  --allow="tcp:3306" \
  --target-tags="jbds-mysql" \
  --description="Allow Cloud Run API to reach JBDS Oracle MySQL 8.4 VM" 2>/dev/null || true

VM_IP=$(gcloud compute instances describe "$VM_NAME" --project="$PROJECT_ID" --zone="$ZONE" --format="get(networkInterfaces[0].accessConfigs[0].natIP)")

echo ""
echo "=================================================================="
echo "Always Free Oracle MySQL 8.4 VM created!"
echo "VM External IP : ${VM_IP}"
echo "Database       : ${DB_NAME}"
echo "User           : ${DB_USER}"
echo "Password       : ${DB_PASS}"
echo ""
echo "Connection String for Cloud Run:"
echo "Server=${VM_IP};Port=3306;Database=${DB_NAME};User=${DB_USER};Password=${DB_PASS};SslMode=Required;"
echo "=================================================================="
echo "Wait ~90 seconds for startup script to finish installing Docker & MySQL."
