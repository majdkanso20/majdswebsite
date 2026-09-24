# MajdsApp.Modules.Audit

**Audit (F-Audit)** — SRS FR-AUDIT-001..006

The append-only record of who did what. Auditable commands are recorded with user, action, HTTP method and path, duration, client IP, browser, outcome, redacted parameters and property-level before/after values of every entity they changed. Refusals (403) and failed commands are recorded too.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/audit/export` — `format` (`csv` default, `xlsx`, `pdf`) plus the list's filters. Returns the file, not the envelope.
- `GET /api/audit/get`
- `GET /api/audit/list`

## Permissions

`Audit.View` for all three endpoints.

Declared here: `Audit.View`.

## Settings defined

- `Audit.RetentionDays` — Audit log retention (days)

## Data

Tables: `AuditEntityChanges`, `AuditLogEntries`, `AuditPropertyChanges`. Migrations live in `MajdsApp.Core`.

## Recurring jobs

- *Audit log retention*

## Notes

- The list accepts `page`, `pageSize`, `sort`, `filter` (text), `from`, `to` (UTC dates, end inclusive) and `outcome` (`Success`, `Failure`, or one of `Forbidden`, `Invalid`, `NotFound`, `Conflict`, `Unauthorized`, `Error`). The export takes the same filters, in any format.
- There is intentionally no endpoint that modifies or deletes entries, and the save interceptor refuses to update or delete audit rows. The only removal is the daily retention job.
- Credential-like fields (password, hash, token, stamp, secret...) and encrypted setting values are stored as `[redacted]`. Notification, job-run and audit tables are not change-tracked, by design.
- Bulk `ExecuteUpdate`/`ExecuteDelete` statements bypass change tracking and so produce no entity changes. Client IP is the direct connection address (proxy headers are not interpreted).
- To audit a new command, make it implement `IAuditableCommand`; nothing else is required.

## Configuration keys

- Setting `Audit.RetentionDays` (default 90; `0` keeps everything) drives the daily *Audit log retention* job.

## Tests

Covered by the integration tests in `src/MajdsApp.Tests` (run `dotnet test src/MajdsApp.Tests`).
