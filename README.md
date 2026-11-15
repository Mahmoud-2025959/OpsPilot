# OpsPilot

Incident-management API: multi-tenant companies, incidents, SLA, webhooks, email, outbox/RabbitMQ, and Hangfire.

The schema write-up lives in [`docs/ERD.md`](docs/ERD.md).

## Requirements

- .NET 10 SDK
- SQL Server or LocalDB (`(localdb)\MSSQLLocalDB`)
- Optional: Docker Desktop (SQL, RabbitMQ, Seq, smtp4dev, API)
- Optional without Docker: portable RabbitMQ and smtp4dev under `scripts/` (see [`scripts/README.md`](scripts/README.md))

## Quickstart (local, ~15 minutes)

```powershell
# 1. Database: LocalDB is enough for a first run
sqllocaldb start MSSQLLocalDB

# 2. Broker (pick one)
docker compose up -d rabbitmq seq
# or:  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install-rabbitmq-local.ps1
#      powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-rabbitmq.ps1

# 2b. Mail (optional, no Docker): smtp4dev on :2525 / UI :5001
#     powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-smtp4dev.ps1

# 3. Schema (once, against the connection string in OpsPilot.API/appsettings.json)
dotnet ef database update --project OpsPilot.Infrastructure --startup-project OpsPilot.API

# 4. API
dotnet run --project OpsPilot.API --launch-profile http
```

Then:

| What | URL |
|---|---|
| API | http://localhost:5093 |
| OpenAPI / Swagger | http://localhost:5093/swagger |
| Hangfire dashboard | http://localhost:5093/jobs |
| Health | http://localhost:5093/health/ready |
| SignalR console (Development) | http://localhost:5093/signalr-test |

Create the first company with `POST /api/v1/auth/register-tenant` (see [`OPS_FLOW.http`](OPS_FLOW.http)). Registering a tenant seeds Owner/Admin/Engineer roles, SLA policies, and the owner user.

Development signing and encryption keys live in `OpsPilot.API/appsettings.Development.json`. They are throwaway local values. A deployment supplies `JWT__SIGNINGKEY` and `APIKEYENCRYPTION__KEY` from the environment.

## Docker (API + SQL + RabbitMQ + Seq)

```powershell
copy .env.example .env   # set SA_PASSWORD, JWT__SIGNINGKEY, APIKEYENCRYPTION__KEY, HANGFIRE_DASHBOARD_PASSWORD
docker compose up -d --build
curl http://localhost:8080/health/ready
```

The API container applies EF migrations on startup (`Database:MigrateOnStartup`). The Hangfire dashboard at http://localhost:8080/jobs requires the Basic-auth user/password from `.env`.

Mail (optional):

```powershell
# in .env: SMTP_ENABLED=true
docker compose --profile mail up -d
```

smtp4dev UI: http://localhost:3000. Local `dotnet run` talks to it on `localhost:2525` when `Smtp:Enabled` is true in `appsettings.Development.json`.

Without Docker, the same SMTP endpoint comes from the portable build:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-smtp4dev.ps1
# SMTP localhost:2525, inbox http://localhost:5001
```

## Tests

```powershell
dotnet test tests\OpsPilot.UnitTests\OpsPilot.UnitTests.csproj -c Release
dotnet test OpsPilot.ArchitectureTests\OpsPilot.ArchitectureTests.csproj -c Release
dotnet test OpsPilot.IntegrationTests\OpsPilot.IntegrationTests.csproj -c Release
```

Integration tests need LocalDB on Windows.

## Docs

| Doc | What it covers |
|---|---|
| [`docs/Runbook.md`](docs/Runbook.md) | Start/stop, queues, Hangfire, logs, SMTP, failed-email reset |
| [`docs/E2E.md`](docs/E2E.md) | Full HTTP scenario and how to prove the stack |
| [`docs/Demo.md`](docs/Demo.md) | 5–7 minute demo script |
| [`docs/Infrastructure.md`](docs/Infrastructure.md) | Workers, Hangfire jobs, RabbitMQ, SMTP |
| [`docs/ERD.md`](docs/ERD.md) | Schema |
| [`OPS_FLOW.http`](OPS_FLOW.http) | REST Client walkthrough |
| [`OPS_AGENT_WALKTHROUGH.md`](OPS_AGENT_WALKTHROUGH.md) | Endpoint-by-endpoint agent guide |

Frontend is not in this repository (API-only). CORS origins can be set later via `Cors:AllowedOrigins`.
