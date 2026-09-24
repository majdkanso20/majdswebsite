# MajdsApp.Modules.Jobs

**Background jobs (F-Background-Jobs)** — SRS FR-JOB-001..005

Recurring jobs run by a hosted scheduler, with run history and a manual 'run now'. A job is any class implementing `IRecurringJob` (name and interval); it is discovered automatically. The platform ships *Audit log retention* and *Notification cleanup*, both daily.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/jobs/list`
- `GET /api/jobs/runs`
- `POST /api/jobs/run`

## Permissions

`Jobs.View` (list, runs) and `Jobs.Run` (run now).

Declared here: `Jobs.Run`, `Jobs.View`.

## Settings defined

- `Jobs.Enabled` — Run scheduled background jobs

## Data

Tables: `JobRuns`. Migrations live in `MajdsApp.Core`.

## Notes

- Schedules derive from the last recorded run, so they survive restarts. Failures are recorded and notify administrators.
- Jobs are interval-based only: no cron, delayed or fire-and-forget jobs, retry or job parameters yet. Email delivery uses its own queue inside the Notifications module.

## Configuration keys

- Setting `Jobs.Enabled` (default `true`); when off, jobs only run when started manually.

## Tests

Not yet covered by automated tests (see the traceability document).
