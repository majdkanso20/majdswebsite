# MajdsApp.Modules.Jobs

**Background jobs (F-Background-Jobs)** — SRS FR-JOB-001..005

Two kinds of background work, both persisted and monitored:

- **Recurring jobs** — a class implementing `IRecurringJob` (name, interval or cron expression). A hosted scheduler runs it when due and records every run.
- **Queued jobs** — a class implementing `IBackgroundJob` (a type name and `ExecuteAsync`). Any module queues one through `IBackgroundJobQueue.EnqueueAsync(type, parameters, delay)`; it runs once, as soon as a worker is free or after the delay. Both kinds are discovered automatically.

## Queuing a job

```csharp
public class SendReportJob : IBackgroundJob
{
    public string Type => "reports.send";
    public int MaxAttempts => 5;                       // default 3
    public async Task ExecuteAsync(BackgroundJobContext ctx, CancellationToken ct)
    {
        var reportId = ctx.Parameters["reportId"];
        // ctx.UserId / ctx.UserName: who queued it (FR-JOB-004); ctx.Attempt: 1 for the first try
    }
}

await queue.EnqueueAsync("reports.send", new Dictionary<string, string> { ["reportId"] = "42" }, delay: TimeSpan.FromMinutes(5));
```

Make a job safe to run again: a retry, or a run after a restart, repeats it.

## How retries and restarts behave

- **Queued jobs** are rows in `BackgroundJobs`, so they survive a restart. A failure puts the job back with a growing delay (10 s, 20 s, 40 s, ... up to one hour). When `MaxAttempts` is used up it stays **Failed** and the `Admin` role is notified.
- A job that was **running when the process stopped** is put back in the queue at the next start. That interrupted try counts as an attempt, so a job that keeps crashing the process ends up Failed rather than looping.
- **Recurring jobs**: a failed run is retried after 1, 2 and 4 minutes, then waits for its next scheduled time. Administrators are notified when the retries are used up (immediately for a run started by hand).
- A worker claims a job atomically, so two workers never run the same one. The recurring scheduler's own "already running" guard is in-process, so it assumes one node.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/jobs/list` — recurring jobs with schedule (interval or cron), last result, failure streak and next run.
- `GET /api/jobs/runs` — history of recurring runs.
- `POST /api/jobs/run` — run a recurring job now.
- `GET /api/jobs/queue` — queued jobs, newest first; `status` (`Pending`, `Running`, `Succeeded`, `Failed`), `filter` (type), paging and sorting.
- `POST /api/jobs/retry` — put a failed job back in the queue with a fresh set of attempts.
- `POST /api/jobs/delete` — remove a job that is not running.

## Permissions

`Jobs.View` (all lists), `Jobs.Run` (run a recurring job now) and `Jobs.Manage` (retry and delete queued jobs). Retry, delete and run are audited.

Declared here: `Jobs.Manage`, `Jobs.Run`, `Jobs.View`.

## Baseline recurring jobs

| Job | Where | What |
|---|---|---|
| Audit log retention | Audit | deletes entries past the retention setting |
| Notification cleanup | Notifications | deletes old read notifications |
| Export cleanup | Exports | deletes finished exports and their files after the retention setting |
| Orphaned file cleanup | Files | deletes stored files no record points to, once they are a day old |
| Plugin staging cleanup | Plugins | deletes abandoned plugin scratch folders a day old |

## Settings defined

- `Jobs.Enabled` — Run scheduled background jobs (recurring schedule only; queued jobs always run)

## Data

Tables: `JobRuns`, `BackgroundJobs`. Migrations live in `MajdsApp.Core`.

## Configuration keys

- Setting `Jobs.Enabled` (default `true`); when off, recurring jobs only run when started manually.
- `Jobs:Scheduler:Enabled` and `Jobs:Worker:Enabled` (default on; set to `false` to switch the recurring scheduler or the queue worker off on a node).
- Cron expressions are five fields, evaluated in UTC (for example `0 3 * * *` is 03:00 every day). Override `IRecurringJob.Cron` to use one.

## Notes

- No built-in feature queues jobs through `IBackgroundJob` yet. Exports (`ExportJobs`) and email delivery keep their own persisted queues, which behave the same way.
- Recurring jobs are not distributed: run the scheduler on one node.

## Tests

`src/MajdsApp.Tests`: the schedule and retry rules (`JobScheduleTests`) and the queue, retries, restart recovery, user context, monitoring API, recurring-failure alerts, orphan sweep and the real worker (`BackgroundJobTests`). The test host switches the recurring scheduler off so no job runs against real folders.
