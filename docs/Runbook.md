# OpsPilot runbook

## Start / stop (developer machine)

### API only (LocalDB + optional portable RabbitMQ)

```powershell
sqllocaldb start MSSQLLocalDB
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-rabbitmq.ps1
dotnet run --project OpsPilot.API --launch-profile http
```

Stop the broker with `scripts\stop-rabbitmq.ps1`. Check it with `scripts\check-rabbitmq.ps1`.

### Docker backing services

```powershell
copy .env.example .env
docker compose up -d sqlserver rabbitmq seq
```

### Full stack in Docker

```powershell
docker compose up -d --build
docker compose --profile mail up -d    # smtp4dev on :3000 / SMTP :2525
```

Health: `curl http://localhost:8080/health/ready` (container) or `http://localhost:5093/health/ready` (`dotnet run`).

## Hangfire

Four recurring jobs, registered only when `Hangfire:Enabled=true`:

| Id | Cron (default) | Work |
|---|---|---|
| `sla-violation-sweep` | every minute | SLA breaches |
| `outbox-drain` | every 30s | publish outbox to RabbitMQ |
| `email-dispatch` | every 30s | send queued notification email |
| `webhook-dispatch` | every 15s | POST pending webhook deliveries |

Dashboard: `/jobs`.

- **Development** (`dotnet run`): open, no login, as long as `Hangfire:DashboardUsername` is empty.
- **Production / Docker**: HTTP Basic. Set `HANGFIRE_DASHBOARD_USER` / `HANGFIRE_DASHBOARD_PASSWORD`. An empty username in Production **denies** every request.

The workers' own timers start only when Hangfire is off. Do not run both.

## RabbitMQ

- AMQP: `localhost:5672`
- Management: http://localhost:15672 (`guest`/`guest`, loopback only)
- App exchange: `opspilot.events`
- Consumer queue: `opspilot.integration`
- Dead letter: `opspilot.events.dead`

A healthy run has an active consumer on `opspilot.integration` and `OutboxMessages.State = Processed` rather than a growing backlog.

## Logs

- Console (Serilog)
- Seq: http://localhost:8081 (ingestion `http://localhost:5341`)
- In Docker, the API posts to `http://seq:5341`

Look for `IAsyncDisposable` / connection errors around the outbox publisher — the outbox scope must be async (`CreateAsyncScope()`).

## SMTP

| Mode | Host | Port | UI |
|---|---|---|---|
| Docker profile `mail` | `smtp4dev` from the API container, `localhost` from the host | 25 in-network, 2525 on the host | http://localhost:3000 |
| `dotnet run` Development | `localhost` | 2525 | same UI if the profile is up |
| Portable smtp4dev (no Docker) | `localhost` | 2525 | http://localhost:5001 |

Start the no-Docker option with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-smtp4dev.ps1
```

It installs smtp4dev 3.16.1 under `%USERPROFILE%\Tools` and keeps the message database in
`%APPDATA%\smtp4dev`; see [`scripts/README.md`](../scripts/README.md).

`Smtp:Enabled=false` leaves queued emails `Pending` and does not burn attempts.

### Reset previously failed emails

After smtp4dev is up and nothing is in-flight:

```sql
UPDATE Notifications
SET Status = 'Pending', Attempts = 0, LastError = NULL
WHERE Channel = 'Email' AND Status = 'Failed';
```

Saved as [`scripts/reset-failed-emails.sql`](../scripts/reset-failed-emails.sql).

## Webhooks

1. Start `scripts\webhook-receiver.ps1` (http://localhost:5099/webhook).
2. `POST /api/v1/webhooks` **before** creating incidents.
3. Watch Hangfire `webhook-dispatch` and `WebhookDeliveries.Status` (`Pending` → `Delivered`).

```sql
SELECT TOP 10 Id, EventType, Status, Attempts, DeliveredAt, LastError, CreatedAt
FROM WebhookDeliveries
ORDER BY CreatedAt DESC;
```

## Migrations

- Local: `dotnet ef database update --project OpsPilot.Infrastructure --startup-project OpsPilot.API`
- Docker API: `Database:MigrateOnStartup=true` (hosted initializer with retries)
- Do not add a migration unless a column or table actually changed

## Production hardening checklist

- Secrets only from env / user-secrets (`JWT__SIGNINGKEY`, `APIKEYENCRYPTION__KEY`, SMTP, RabbitMQ, Hangfire dashboard password)
- Hangfire dashboard: credentials set, or `Hangfire:DashboardEnabled=false`
- `Cors:AllowedOrigins` if a browser frontend is added
- `HttpsRedirection:Enabled` true behind TLS; false for the plain-HTTP compose API
- Rate limiting stays on the ingestion endpoint
- Seq URL points at an authenticated instance in a real deployment
