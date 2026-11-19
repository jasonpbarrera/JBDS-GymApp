# JBDS-GymApp

Multi-product, multi-tenant **Gym Membership, QR/Barcode Check-In, Class Scheduling & Billing Progressive Web App (PWA)** built with **.NET 10 Blazor WebAssembly**, **ASP.NET Core 10 Web API**, **EF Core 10**, and **Oracle MySQL (`MySql.EntityFrameworkCore`)**.

Created and maintained by **Jason Barrera / [JB Digital Services](https://jbdigitalservices.net)**.

---

## 100% Free Tier Architecture (\$0/month)

| Layer | Technology | Hosting (Free Tier) |
|---|---|---|
| **Frontend (PWA)** | .NET 10 Blazor WebAssembly + Service Worker | **Cloudflare Workers with Static Assets** (Free Plan — unlimited static asset requests + 100k Worker req/day) |
| **API Proxy / Ingress** | `worker/index.js` + `cloudflared` | **Cloudflare Workers + Cloudflare Tunnel** (Free Plan — HTTPS with zero open VM ports) |
| **Backend API** | .NET 10 ASP.NET Core Web API + EF Core 10 | **Oracle Cloud Infrastructure (OCI) Always Free VM** (Docker) |
| **Database** | Oracle MySQL 8.4 | **OCI Always Free VM** (Docker `mysql:8.4` or OCI MySQL) |

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
  1. **Custom Domain / Subdomain** (`X-Forwarded-Host` from Cloudflare Worker or `Host` header)
  2. **Authenticated JWT Claims** (`tenant_id`, `product_id`)
  3. **Explicit Headers** (`X-Tenant-Id` / `X-Tenant-Slug`, `X-Product-Id` / `X-Product-Key`) for local dev and Platform Admin tenant switching

---

## Solution Structure

- `src/JBDS.Platform.Core` — Reusable multi-product/multi-tenant abstractions (`ITenantEntity`, `IProductTenantEntity`, `ITenantResolverService`, `TenantResolverService`, `Product`, `Tenant`, `TenantProductSubscription`, roles & claim constants).
- `src/JBDS.GymApp.Shared` — Shared DTOs and enums used by both the API and Blazor WASM client.
- `src/JBDS.GymApp.Api` — ASP.NET Core 10 Web API with EF Core 10 + Oracle MySQL (`MySql.EntityFrameworkCore`), ASP.NET Core Identity + JWT auth, `TenantResolutionMiddleware`, Stripe Checkout & webhooks, and demo seeder.
- `src/JBDS.GymApp.Client` — .NET 10 Blazor WebAssembly Standalone PWA with network-first cache freshness, SEO metadata/JSON-LD, and full UI (Dashboard, QR/Barcode Check-In, Members, Plans, Classes, Billing, Platform Admin).
- `tests/JBDS.GymApp.Tests` — xUnit multi-tenant & multi-product isolation and middleware resolution tests.
- `worker/index.js` & `wrangler.toml` — Cloudflare Workers with Static Assets Free Tier configuration.
- `docker-compose.yml` — Oracle MySQL 8.4 + .NET 10 API + optional Cloudflare Tunnel stack for OCI Always Free.

---

## Running & Deploying the Oracle Cloud (OCI Always Free) Backend

### Option A: 1-Command OCI Always Free VM Bootstrap (\$0/mo)
Provision an **Always Free** Ubuntu/Debian VM in Oracle Cloud (ARM64 Ampere A1 or AMD Micro), SSH in, and run:
```bash
CLOUDFLARE_TUNNEL_TOKEN="YOUR_FREE_TUNNEL_TOKEN" \
  curl -fsSL https://raw.githubusercontent.com/jasonpbarrera/JBDS-GymApp/main/scripts/setup-oci-backend.sh | bash
```
This installs Docker, clones `JBDS-GymApp`, generates strong random Oracle MySQL & JWT secrets in `.env`, runs EF Core MySQL migrations automatically on startup (`InitialCreate`), and starts Oracle MySQL 8.4 + `.NET 10` API + `cloudflared`.

### Option B: Local Development (Docker or In-Memory DB)
```bash
cp .env.example .env
docker compose up -d --build
```
Or run the API locally with In-Memory DB for quick UI testing:
```bash
Database__UseInMemory=true dotnet run --project src/JBDS.GymApp.Api --urls http://localhost:5050
```

### EF Core Oracle MySQL Migrations
Initial migration is committed under `src/JBDS.GymApp.Api/Data/Migrations/`. To add future schema changes:
```bash
dotnet ef migrations add <MigrationName> --project src/JBDS.GymApp.Api/JBDS.GymApp.Api.csproj --output-dir Data/Migrations
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
| Apex Athletics Admin | `admin@apex.demo` | `Apex123!` |

---

## Deploying to Cloudflare Workers (Free Tier)

1. Set `API_ORIGIN` in `wrangler.toml` to your Cloudflare Tunnel API URL (e.g., `https://api-gymapp.jbdigitalservices.net`).
2. Add GitHub repository secrets `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID`.
3. Push to `main` (`.github/workflows/cloudflare-worker.yml` runs tests, publishes the Blazor WASM PWA, and deploys via Wrangler) — or deploy manually:
   ```bash
   dotnet publish src/JBDS.GymApp.Client/JBDS.GymApp.Client.csproj -c Release -o publish_output
   npx wrangler deploy
   ```
