# JBDS-GymApp

Multi-product, multi-tenant **Gym Membership, QR/Barcode Check-In, Class Scheduling & Billing Progressive Web App (PWA)** built with **.NET 10 Blazor WebAssembly**, **ASP.NET Core 10 Web API**, **EF Core 10**, and **Oracle MySQL (`MySql.EntityFrameworkCore`)**, hosted on **Google Cloud & Firebase Always Free (\$0/month)** infrastructure.

Created and maintained by **Jason Barrera / [JB Digital Services](https://jbdigitalservices.net)**.

---

## 100% Always Free Google Cloud & Firebase Architecture (\$0/month)

| Layer | Technology | Google Cloud / Firebase Free Tier |
|---|---|---|
| **Frontend (PWA)** | .NET 10 Blazor WebAssembly + Service Worker | **Firebase Hosting** (Global CDN, 10 GB storage, 360 MB/day transfer, free SSL & custom domains) |
| **API / Serverless** | .NET 10 ASP.NET Core Web API + EF Core 10 | **Google Cloud Run** (2M requests/mo free, scales to zero, rewritten from Firebase `/api/**` with zero CORS) |
| **Database** | Oracle MySQL 8.4 | **Google Compute Engine Always Free `e2-micro` VM** (`us-central1` / `us-west1` / `us-east1`, 30 GB `pd-standard` disk, memory-tuned with 2 GB swap) |

---

## Multi-Product & Multi-Tenant Architecture (`TenantId` + `ProductId`)

`JBDS-GymApp` is designed on a reusable platform foundation (`JBDS.Platform.Core`) that supports **multiple products/projects** and **shared tenants** that can subscribe to **multiple products** (`TenantProductSubscription` many-to-many):

- **Shared Platform Tables (Unfiltered):**
  - `Products` — e.g., `gym-membership`, `personal-training`
  - `Tenants` — shared organizations (e.g., `ironforge`, `apex`) with branding & custom domains
  - `TenantProductSubscriptions` — many-to-many subscriptions linking a `Tenant` to one or more `Products`, with optional per-product subdomain/custom domain overrides
- **Isolated Product + Tenant Tables (`IProductTenantEntity`):**
  - Every domain table (`MembershipPlans`, `Members`, `MemberSubscriptions`, `CheckIns`, `GymClasses`, `ClassSessions`, `ClassBookings`, `Invoices`, `Payments`) includes both `TenantId` and `ProductId` discriminator columns with composite indexes `(TenantId, ProductId, ...)`.
- **EF Core `ITenantResolverService` & Global Query Filters:**
  - `GymAppDbContext` injects the scoped `ITenantResolverService` and applies **EF Core Global Query Filters** on every `ITenantEntity` / `IProductTenantEntity`.
  - `SaveChanges` / `SaveChangesAsync` automatically stamps `TenantId` and `ProductId` on inserts and blocks cross-tenant / cross-product writes (`UnauthorizedAccessException`).
- **Request Resolution Order (`TenantResolutionMiddleware`):**
  1. **Custom Domain / Subdomain** (`X-Forwarded-Host` from Firebase Hosting → Cloud Run rewrite, or `Host` header)
  2. **Authenticated JWT Claims** (`tenant_id`, `product_id`)
  3. **Explicit Headers** (`X-Tenant-Id` / `X-Tenant-Slug`, `X-Product-Id` / `X-Product-Key`) for local dev and Platform Admin tenant switching

---

## Solution Structure

- `src/JBDS.Platform.Core` — Reusable multi-product/multi-tenant abstractions (`ITenantEntity`, `IProductTenantEntity`, `ITenantResolverService`, `TenantResolverService`, `Product`, `Tenant`, `TenantProductSubscription`, roles & claim constants).
- `src/JBDS.GymApp.Shared` — Shared DTOs and enums used by both the API and Blazor WASM client.
- `src/JBDS.GymApp.Api` — ASP.NET Core 10 Web API with EF Core 10 + Oracle MySQL (`MySql.EntityFrameworkCore`), initial migration (`Data/Migrations/`), ASP.NET Core Identity + JWT auth, `TenantResolutionMiddleware`, Stripe Checkout & webhooks, and demo seeder.
- `src/JBDS.GymApp.Client` — .NET 10 Blazor WebAssembly Standalone PWA with network-first cache freshness, browser camera QR/barcode scanner (`wwwroot/js/qr-scanner.js`), SEO metadata/JSON-LD, and full UI (Dashboard, Check-In, Members, Plans, Classes, Billing, Platform Admin).
- `tests/JBDS.GymApp.Tests` — xUnit multi-tenant & multi-product isolation and middleware resolution tests.
- `Dockerfile`, `firebase.json`, `.firebaserc` — Google Cloud Run + Firebase Hosting deployment configuration.
- `scripts/setup-gcp-mysql-vm.sh` & `scripts/deploy-gcp.sh` — Turnkey scripts for Always Free `e2-micro` Oracle MySQL 8.4 + Cloud Run + Firebase Hosting.
- `.github/workflows/deploy-gcp.yml` — Automated CI/CD workflow for tests, Cloud Run API deploy, and Firebase Hosting PWA deploy.

---

## Deploying to Google Cloud & Firebase (100% Always Free)

### 1. Create a Google Cloud / Firebase Project
1. Go to **[console.firebase.google.com](https://console.firebase.google.com/)** → **Create a project** (e.g., `jbds-gymapp`).
2. Upgrade the project to the **Blaze (pay-as-you-go)** plan (required to link Firebase Hosting to Cloud Run; you stay **\$0/mo** inside the Always Free quotas—set a **\$1 budget alert** in Google Cloud Billing for peace of mind).
3. Update `.firebaserc` with your project ID.

### 2. Provision the Always Free Oracle MySQL 8.4 VM (`e2-micro`, \$0/mo)
Open **Google Cloud Shell** (the `>_` icon at the top-right of [console.cloud.google.com](https://console.cloud.google.com/)):
```bash
git clone https://github.com/jasonpbarrera/JBDS-GymApp.git
cd JBDS-GymApp
gcloud config set project YOUR_GCP_PROJECT_ID
bash scripts/setup-gcp-mysql-vm.sh
```
Copy the printed **Connection String** (and wait ~90 seconds for Docker + Oracle MySQL 8.4 to finish booting).

### 3. Deploy the `.NET 10` API (Cloud Run) + Blazor WASM PWA (Firebase Hosting)
Still in Google Cloud Shell:
```bash
GCP_PROJECT_ID="YOUR_GCP_PROJECT_ID" \
DB_CONNECTION="Server=...;Port=3306;Database=jbds_platform;User=jbds_app;Password=...;SslMode=Required;" \
JWT_KEY="$(openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 64)" \
  bash scripts/deploy-gcp.sh
```
Your app will be live at **`https://YOUR_GCP_PROJECT_ID.web.app`** with `/api/**` automatically rewritten to Cloud Run!

---

## Running Locally

### Start Oracle MySQL + API via Docker
```bash
cp .env.example .env
docker compose up -d --build
```
Or run the API locally with In-Memory DB for quick UI testing:
```bash
Database__UseInMemory=true dotnet run --project src/JBDS.GymApp.Api --urls http://localhost:5050
```

### Run the Blazor WASM PWA Client Locally
`src/JBDS.GymApp.Client/wwwroot/appsettings.Development.json` automatically points to `http://localhost:5050/`:
```bash
dotnet run --project src/JBDS.GymApp.Client --urls http://localhost:5002
```

### Run Unit & Multi-Tenant Isolation Tests
```bash
dotnet test
```

### Seeded Demo Accounts
| Role | Email | Password |
|---|---|---|
| Platform Admin (All Tenants & Products) | `platform@jbdigitalservices.net` | `Platform123!` |
| Iron Forge Gym Admin | `admin@ironforge.demo` | `IronForge123!` |
| Iron Forge Staff | `staff@ironforge.demo` | `Staff123!` |
| Iron Forge Member (Member Portal + Door Unlock) | `member@ironforge.demo` | `Member123!` |
| Apex Athletics Admin | `admin@apex.demo` | `Apex123!` |
| Apex Athletics Member (Member Portal + Door Unlock) | `member@apex.demo` | `Member123!` |

### Smart Door Unlock Webhook (`DoorAccess`)
When a member taps **Check In & Unlock Door** in the Member Portal (`/member`), `WebhookDoorAccessService` validates their membership and fires an HTTP POST to `DoorAccess:WebhookUrl` (with optional `X-Door-Api-Key` from `DoorAccess:ApiKey` and `DoorAccess:UnlockDurationSeconds`, default `5`). Leave `DoorAccess:WebhookUrl` blank to run in simulated hardware relay mode with full `DoorAccessEvents` audit logging.
