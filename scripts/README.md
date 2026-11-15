# Local RabbitMQ and SMTP on Windows (no Docker, no admin)

The repository's `docker-compose.yml` is still the preferred way to run the backing services when
Docker Desktop is available. This folder is the fallback for a Windows machine where Docker/WSL is
not installed and the shell cannot elevate: RabbitMQ, Erlang and smtp4dev are downloaded as
portable builds and run out of the user profile, so nothing is written to Program Files and no
service is registered.

## What it installs

| Component | Version | Default location |
|---|---|---|
| Erlang/OTP | 29.1.2 | `%USERPROFILE%\Tools\erlang-29.1.2` |
| RabbitMQ | 4.4.0 | `%USERPROFILE%\Tools\rabbitmq-4.4.0` |
| Node data, logs | — | `%USERPROFILE%\Tools\rabbitmq-data` |

RabbitMQ 4.4 supports Erlang 27 through 29. The management plugin is enabled, so the UI is at
<http://localhost:15672> with the default `guest` / `guest` account. As on any RabbitMQ install,
that account only connects from the loopback address, which is exactly what the local API uses.

## Usage

```powershell
# once
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install-rabbitmq-local.ps1

# each time you want the broker
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-rabbitmq.ps1

# stop it
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\stop-rabbitmq.ps1

# check ports, management API, and the OpsPilot exchanges/queues
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check-rabbitmq.ps1
```

`start-rabbitmq.ps1` starts the node detached, waits until 5672 accepts connections, and prints
the management URL. Pass `-Foreground` to keep the Erlang console attached instead.

## Application settings

`OpsPilot.API/appsettings.Development.json` already points the application at this broker:

```json
"RabbitMq": {
  "Enabled": true,
  "ConsumerEnabled": true,
  "HostName": "localhost",
  "Port": 5672,
  "VirtualHost": "/",
  "UserName": "guest",
  "Password": "guest",
  "UseTls": false
}
```

Keep `UseTls` false locally: the portable node does not have a TLS listener. A deployment uses
the same section with `UseTls` true and credentials supplied from the environment.

## SMTP (smtp4dev)

`OpsPilot.API/appsettings.Development.json` points SMTP at `localhost:2525`. When Docker is not
available, `start-smtp4dev.ps1` downloads the smtp4dev Windows x64 standalone build, expands it
under `%USERPROFILE%\Tools\smtp4dev-<version>`, and starts it with the SMTP listener on 2525 and
the web UI on <http://localhost:5001>:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-smtp4dev.ps1

# optional: keep the process attached to the console
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-smtp4dev.ps1 -Foreground
```

| Component | Version | Default location |
|---|---|---|
| smtp4dev | 3.16.1 | `%USERPROFILE%\Tools\smtp4dev-3.16.1` |
| Message database | — | `%APPDATA%\smtp4dev\database.db` |

Pass `-Force` to re-download, `-SmtpPort` / `-UiPort` to move the listeners. Nothing is installed
machine-wide and no service is registered, so after a reboot run the script again.

With smtp4dev listening, `email-dispatch` delivers queued notifications: `Notifications.Status`
becomes `Sent` with `SentAt` set, and the message appears in the smtp4dev inbox. If SMTP is
enabled but nothing is listening, each notification burns its attempts and ends `Failed` — see
`reset-failed-emails.sql` and `docs/Runbook.md` to requeue those rows.

## Webhook receiver

The webhook channel needs somewhere to deliver to. `webhook-receiver.ps1` is a loopback-only
receiver for demos and manual E2E runs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\webhook-receiver.ps1
```

It listens on <http://localhost:5099/webhook>, prints the `X-OpsPilot-*` headers and the JSON
body of each call, answers `200`, and appends every call to `webhook-receiver.log` in the repo
root. It uses a raw TCP listener, so it does not need a URL ACL or administrator rights. Stop it
with Ctrl+C.

Register `http://localhost:5099/webhook` as an endpoint before creating an incident — deliveries
are staged when the incident event is stored, so an endpoint added later hears about nothing.
The webhook sweep runs every 15 seconds via Hangfire's `webhook-dispatch` job.

## Notes

- The node name is the default `rabbit@<machine-name>`; the data directory is kept under
  `%USERPROFILE%\Tools\rabbitmq-data` so a reboot does not lose definitions.
- Nothing is installed as a Windows service. After a reboot, run the start script again.
- To use Docker instead, install Docker Desktop, copy `.env.example` to `.env`, and run
  `docker compose up -d rabbitmq`.
