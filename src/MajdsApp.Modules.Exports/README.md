# MajdsApp.Modules.Exports

**Background exports (F-Export)** — SRS FR-EXP-004, AC-EXP-3

Large exports run as background jobs instead of holding a request open. The user starts one, carries on working, gets an in-app notification with a link when it is ready, and downloads the file from **My exports**. The finished file is also stored in F-Files, owned by that user.

Any module's `IExportSource` (defined in `MajdsApp.SharedKernel/Export`) can be exported this way with no further code; users and the audit log are registered today.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `POST /api/exports/start` — body `{ source, format, filters }`. `source` is an export source key (`users`, `audit`), `format` is `csv` or `xlsx`, `filters` are the same filters the list accepts (for example `{ "filter": "ada", "isActive": "true" }`). Returns the queued job immediately. Refused with 403 without the source's own view permission, 404 for an unknown source, 400 for PDF (limited to 2,000 rows, so download it directly), 409 when the caller already has three exports in progress.
- `GET /api/exports/list` — the caller's 25 most recent exports with status (`Pending`, `Running`, `Completed`, `Failed`), file name and size, or the error.
- `GET /api/exports/download?jobId=…` — the finished file (not the envelope). Only the user who started the export can download it; anyone else gets 404, and an unfinished export gets 409.

## How it works

1. `start` checks the permission while the request still knows who is asking, then stores an `ExportJob` (source, format, filters as JSON).
2. `ExportWorker` (a hosted service) takes the oldest pending job every couple of seconds and claims it atomically, so two instances never run the same job. A job left `Running` by a restart is put back in the queue at startup.
3. `ExportJobProcessor` builds the file through the source (up to 200,000 rows), saves it through F-Files' storage as the requesting user's file, marks the job `Completed`, and publishes a notification ("Your export is ready", linking to `/exports`). If anything throws, the job is marked `Failed` with the reason and the user is notified of that instead.

## Permissions

None of its own: exporting needs the source's view permission (`Users.View`, `Audit.View`), and the list and download are always scoped to the caller. Requires the **Files** feature to be on, because results are delivered as files.

## Settings defined

- `Exports.RetentionDays` — finished exports and their files are deleted after this many days (default 7).

## Data

Table: `ExportJobs`. Migration lives in `MajdsApp.Core`.

## Recurring jobs

- *Export cleanup* — daily; removes expired jobs together with their files.

## Notes

- Not covered: imports do not run in the background yet (they process up to 5,000 rows during the request), and the file is built in memory before it is written, which is why the cap is 200,000 rows.
- The notification link needs the frontend route `/exports`; the email copy of a notification carries the text only, not the link.
