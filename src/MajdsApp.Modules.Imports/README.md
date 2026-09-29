# MajdsApp.Modules.Imports

**Background imports (F-Export)** — SRS FR-EXP-003/004

Imports run as background jobs instead of holding a request open, the same shape `MajdsApp.Modules.Exports` gives exports. The user uploads a file, carries on working, gets an in-app notification with the outcome when it is done, and can see the per-row results in **Import history**.

Any module's `IImportSource` (defined in `MajdsApp.SharedKernel/Import`) can be imported this way with no further code; users are registered today.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `POST /api/imports/start?source=users` — multipart file upload (field `file`). `source` is an import source key. Returns the queued job immediately. Refused with 403 without the source's own create permission, 404 for an unknown source, 400 for an empty file or one over 5 MB, 409 when the caller already has three imports in progress.
- `GET /api/imports/list` — the caller's 50 most recent imports with status (`Pending`, `Running`, `Completed`, `Failed`), row counts and, once finished, the per-row errors (row number and reason) or a whole-file error.
- `GET /api/imports/template?source=users&format=csv` — the blank template for that source (not the envelope), built from `IImportSource.TemplateColumns`.

## How it works

1. `start` checks the permission while the request still knows who is asking, then stores an `ImportJob` (source, file name, the file's bytes).
2. `ImportWorker` (a hosted service) takes the oldest pending job every couple of seconds and claims it atomically, so two instances never run the same job. A job left `Running` by a restart is put back in the queue at startup.
3. `ImportJobProcessor` runs the file through the source's `RunAsync` (which parses it and imports each row exactly as before — `UsersImportSource` delegates to `ImportUsersCommand`, so a row still goes through `CreateUserCommand`, obeys the same validation, and is audited the same way), records how many rows succeeded and, for the rest, the row and reason, discards the file's bytes (job done, no reason to keep them), marks the job `Completed`, and publishes a notification with the outcome. If parsing itself fails (wrong file type, missing column — a whole-file problem, not a row problem), the job is marked `Failed` with the reason instead.

## Permissions

None of its own: importing needs the source's create permission (`Users.Create`), and the list and template are scoped by source; the list is always scoped to the caller.

## Settings defined

- `Imports.RetentionDays` — finished import jobs are deleted after this many days (default 30).

## Feature flag

`Imports` (on by default).

## Data

Table: `ImportJobs`. Migration lives in `MajdsApp.Core`. The uploaded file's bytes are cleared once the job finishes (success or failure); only the small per-row result stays.

## Recurring jobs

- *Import cleanup* — daily; removes expired jobs.

## Notes

- The 5 MB / 5,000-row cap is unchanged from the old synchronous endpoint (`TabularReader.MaxBytes`/`MaxRows`) — running in the background removes the "it must fit inside one request" problem, but the file is still held whole in the job row, so the cap stays for now (exports raised theirs to 200,000 rows because the built file goes straight to storage, not a database column).
- The notification link needs the frontend route `/imports`; the email copy of a notification carries the text only, not the link.
