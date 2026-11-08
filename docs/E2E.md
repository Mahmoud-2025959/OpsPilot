# OpsPilot end-to-end scenario

Run [`OPS_FLOW.http`](../OPS_FLOW.http) top to bottom against `http://localhost:5093` (VS Code REST Client or an equivalent). Each named request captures tokens and ids for the next step.

## Before you start

1. API is up (`dotnet run --project OpsPilot.API --launch-profile http`).
2. RabbitMQ is up (`scripts\start-rabbitmq.ps1` or `docker compose up -d rabbitmq`).
3. Hangfire is on (`Hangfire:Enabled=true` in Development).
4. Optional mail: `docker compose --profile mail up -d`, or without Docker
   `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-smtp4dev.ps1` (SMTP on
   `localhost:2525`, inbox at <http://localhost:5001>).
5. Optional webhooks: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\webhook-receiver.ps1`.
6. Use a fresh `@companySlug` / `@ownerEmail` if the tenant already exists.

Severity values are the enum names — `Critical`, `High`, `Medium`, `Low` — not `P0`–`P4`. A
`P1` string fails validation on incidents, SLA policies, event ingestion and notification
preferences.

## Path

| Step | What | Success |
|---|---|---|
| 1 | `POST /auth/register-tenant` | 200, company + owner |
| 2–4 | Login, refresh, `GET /users/me` | access + refresh tokens |
| 5 | Permissions / roles / tenant | 200 |
| 6–8 | User (Engineer), team, member, service | ids captured |
| 9 | SLA policies update | 204/200 |
| 10 | Webhook endpoint **before** incidents | 200 |
| 11 | Incident create → assign → comment → timeline → transitions Ack…Close | only legal transitions accepted |
| 12 | Postmortem create → review → publish | published |
| 13–14 | API key + `POST /events` with `X-API-Key` | correlated/merged or new incident |
| 15 | Webhook deliveries | row `Delivered` after `webhook-dispatch` |
| 16 | Notifications, preferences, analytics, audit | 200 |
| 17 | `/signalr-test` | `notificationCreated` / `incidentUpdated` |
| 18 | Open incident past SLA | `SlaViolation` within a minute |

Skip step 19 (change-password) unless you intend to update `@ownerPassword`.

## Infrastructure checks during the run

- `OutboxMessages`: `State` becomes `Processed`.
- RabbitMQ: consumer on `opspilot.integration`, queue is not growing.
- Hangfire `/jobs`: `outbox-drain`, `email-dispatch`, `sla-violation-sweep`, `webhook-dispatch` all `Succeeded`.
- API log: no `IAsyncDisposable` or broker connection errors.
- Email (if SMTP on): smtp4dev inbox, `Notifications.Status = Sent` and `SentAt` set.
- Webhook: `WebhookDeliveries.Status = Delivered` (not `Sent` — that is the email status name).

## Fixes applied while completing this path

- `OPS_FLOW.http` login URL is `POST {{baseUrl}}/api/v1/auth/login` (the broken `vUrl` line was removed).
- Webhook dispatch is a Hangfire recurring job (`webhook-dispatch`) as well as a scoped worker, so deliveries send when Hangfire owns the schedule.
- Integration test `The_Scheduled_Webhook_Job_Delivers_A_Queued_Call` covers that seam.
- `OPS_FLOW.http` severity strings changed from `P0`–`P4` to the real enum names
  (`Critical`/`High`/`Medium`/`Low`), which is what the API validates.
- `scripts/webhook-receiver.ps1` resolves its default `LogPath` inside the body; the parameter
  default ran before `$PSScriptRoot` was populated under Windows PowerShell 5.1.
- Webhook delivery state is `Delivered`; `Sent` belongs to notifications.

## Verified live run (2026-11-07)

Environment: SQL Server `DESKTOP-O516MLP\HOLASERVER`, RabbitMQ 4.4 portable on 5672, API
`http://localhost:5093` with Hangfire on, `scripts\webhook-receiver.ps1` on 5099, portable
smtp4dev 3.16.1 on SMTP 2525 / UI 5001.

| Check | Evidence |
|---|---|
| Four Hangfire recurring jobs, all `Succeeded` | `sla-violation-sweep`, `outbox-drain`, `email-dispatch`, `webhook-dispatch` in `HangFire.Set` and the dashboard |
| Tenant / user / team / service / SLA / incident / postmortem / API key / ingestion | all `200`/`201`/`202`; see the status table printed by the E2E driver |
| Webhook delivered | `WebhookDeliveries` row `15b7d167-4f38-452c-94c8-10fb3527b23b` `IncidentCreated` `Delivered`, and a `POST /webhook` line in `webhook-receiver.log` with `X-OpsPilot-Signature` |
| Email delivered | three `Notifications` rows `Sent` with `SentAt`; `GET http://localhost:5001/api/messages` returns three messages |
| SLA sweep | incident left open past `ResponseDueAt` → `SlaViolations` row `Response`, timeline `SlaViolationDetected`, and `InApp` + `Email` `SlaViolation`/`SlaViolationEscalated` notifications `Sent` |

Note: creating an incident through `POST /api/v1/incidents` records timeline events but does not
stage outbox/webhook rows — only the ingestion path (`POST /api/v1/events`) does. The webhook
delivery above therefore came from the ingested event, which is the documented current
behaviour.

## Evidence

After a live run, keep:

1. Hangfire dashboard screenshot (four jobs, last state Succeeded).
2. The SQL snippets in [`Runbook.md`](Runbook.md) for deliveries and notifications.
3. smtp4dev message list if mail was in scope.
