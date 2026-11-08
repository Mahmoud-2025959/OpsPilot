# OpsPilot demo script (5–7 minutes)

Audience: a reviewer who has not seen the repo. Use `dotnet run --launch-profile http` plus RabbitMQ and, if possible, smtp4dev and `scripts\webhook-receiver.ps1`.

## 0:30 — What it is

OpsPilot is a multi-tenant incident API: companies, users, services, incidents, SLA, webhooks, email, and a Hangfire scheduler.

Open Swagger at http://localhost:5093/swagger.

## 1:00 — Create a company

`POST /api/v1/auth/register-tenant` (from `OPS_FLOW.http`). Show that the owner can `GET /users/me`.

## 2:00 — Incident

Create a team, a production service, and a P1 incident. Assign it, add a comment, acknowledge it.

## 3:00 — Live update

Open http://localhost:5093/signalr-test, sign in as the owner, transition the incident. Point at `incidentUpdated` / `notificationCreated`.

## 4:00 — SLA

Show the SLA policies. Either wait for the next `sla-violation-sweep` on an overdue incident or show a previously recorded `SlaViolation` plus Hangfire.

## 5:00 — Webhook

Show the local receiver window, then `GET /webhooks/deliveries` with `Delivered`. Mention the job id `webhook-dispatch`.

## 6:00 — Hangfire

http://localhost:5093/jobs — four recurring jobs, last success. Optionally show Seq at http://localhost:8081.

## Optional extra (if smtp4dev is up)

Open http://localhost:3000 and show one incident email.

Stop before change-password so the demo tenant stays reusable.
