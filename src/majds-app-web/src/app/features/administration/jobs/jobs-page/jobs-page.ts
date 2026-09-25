import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DataGrid } from '../../../../shared/components/data-grid/data-grid';
import { GridColumn, GridPage, GridSort } from '../../../../shared/components/data-grid/data-grid.model';
import { HasPermissionDirective } from '../../../../core/directives/has-permission.directive';
import { FormattingService } from '../../../../core/i18n/formatting.service';
import { LocalizationService } from '../../../../core/i18n/localization.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { BackgroundJobDto, BackgroundJobStatus, JobDto, JobRunDto, JobsService } from '../jobs.service';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-jobs-page',
  imports: [DataGrid, HasPermissionDirective, TranslatePipe, MatButtonModule, MatIconModule, MatFormFieldModule, MatSelectModule],
  styleUrl: './jobs-page.scss',
  templateUrl: './jobs-page.html'
})
export class JobsPage {
  private readonly jobsApi = inject(JobsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly l10n = inject(LocalizationService);
  private readonly fmt = inject(FormattingService);

  private readonly local = (utc: string | null): string => (utc ? this.fmt.date(utc) : '—');

  readonly jobColumns: GridColumn<JobDto>[] = [
    { key: 'name', header: 'Job' },
    { key: 'intervalMinutes', header: 'Every', value: (j) => j.cron ?? JobsPage.formatInterval(j.intervalMinutes) },
    { key: 'lastRunAt', header: 'Last run', value: (j) => this.local(j.lastRunAt) },
    { key: 'lastSuccess', header: 'Last result', value: (j) => this.lastResult(j) },
    { key: 'nextRunAt', header: 'Next run', value: (j) => this.local(j.nextRunAt) }
  ];

  readonly runColumns: GridColumn<JobRunDto>[] = [
    { key: 'startedAt', header: 'When', sortable: true, value: (r) => this.local(r.startedAt) },
    { key: 'jobName', header: 'Job', sortable: true },
    { key: 'trigger', header: 'Trigger' },
    { key: 'durationMs', header: 'Duration', sortable: true, value: (r) => `${r.durationMs} ms` },
    { key: 'success', header: 'Result', value: (r) => (r.success ? this.l10n.translate('OK') : this.l10n.translate('Failed: {0}', r.error ?? '')) }
  ];

  readonly queueColumns: GridColumn<BackgroundJobDto>[] = [
    { key: 'createdAt', header: 'Queued', sortable: true, value: (j) => this.local(j.createdAt) },
    { key: 'type', header: 'Job', sortable: true },
    { key: 'status', header: 'Status', value: (j) => (j.status === 'Pending' ? this.l10n.translate('Pending, next try {0}', this.local(j.nextAttemptAt)) : this.l10n.translate(j.status)) },
    { key: 'attempts', header: 'Attempts', sortable: true, value: (j) => `${j.attempts} / ${j.maxAttempts}` },
    { key: 'userName', header: 'Queued by', value: (j) => j.userName ?? '—' },
    { key: 'lastError', header: 'Last error', value: (j) => j.lastError ?? '' }
  ];

  readonly statuses: BackgroundJobStatus[] = ['Pending', 'Running', 'Succeeded', 'Failed'];
  queueStatus = '';

  readonly queued = signal<BackgroundJobDto[]>([]);
  readonly queuedTotal = signal(0);
  readonly queueLoading = signal(true);

  private queuePage = 0;
  private queuePageSize = 10;
  private queueSort = 'createdAt:desc';

  readonly jobs = signal<JobDto[]>([]);
  readonly runs = signal<JobRunDto[]>([]);
  readonly runsTotal = signal(0);
  readonly loading = signal(true);

  private page = 0;
  private pageSize = 10;
  private sort = 'startedAt:desc';

  constructor() {
    this.load();
  }

  onPage(event: GridPage): void {
    this.page = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  onSort(event: GridSort): void {
    this.sort = event.direction ? `${event.active}:${event.direction}` : '';
    this.load();
  }

  onQueuePage(event: GridPage): void {
    this.queuePage = event.pageIndex;
    this.queuePageSize = event.pageSize;
    this.loadQueue();
  }

  onQueueSort(event: GridSort): void {
    this.queueSort = event.direction ? `${event.active}:${event.direction}` : '';
    this.loadQueue();
  }

  applyQueueFilter(): void {
    this.queuePage = 0;
    this.loadQueue();
  }

  retry(job: BackgroundJobDto): void {
    this.jobsApi.retry(job.id).subscribe({
      next: () => {
        this.snackBar.open(this.l10n.translate('"{0}" was queued again.', job.type), 'Dismiss', { duration: 3000 });
        this.loadQueue();
      },
      error: (err) => this.showError(err)
    });
  }

  remove(job: BackgroundJobDto): void {
    if (!confirm(this.l10n.translate('Delete this "{0}" job?', job.type))) return;
    this.jobsApi.delete(job.id).subscribe({
      next: () => {
        this.snackBar.open('The job was deleted.', 'Dismiss', { duration: 3000 });
        this.loadQueue();
      },
      error: (err) => this.showError(err)
    });
  }

  runNow(job: JobDto): void {
    this.jobsApi.run(job.name).subscribe({
      next: (ran) => {
        this.snackBar.open(ran ? `"${job.name}" finished.` : `"${job.name}" is already running.`, 'Dismiss', { duration: 3000 });
        this.load();
      },
      error: (err) => this.showError(err)
    });
  }

  private showError(err: unknown): void {
    const message = (err as { error?: { message?: string } })?.error?.message ?? 'Something went wrong.';
    this.snackBar.open(message, 'Dismiss', { duration: 5000 });
  }

  private loadQueue(): void {
    this.queueLoading.set(true);
    this.jobsApi.queue(this.queuePage + 1, this.queuePageSize, this.queueStatus || undefined, this.queueSort || undefined).subscribe({
      next: (result) => {
        this.queued.set(result.items);
        this.queuedTotal.set(result.totalCount);
        this.queueLoading.set(false);
      },
      error: () => this.queueLoading.set(false)
    });
  }

  private lastResult(job: JobDto): string {
    if (job.lastSuccess === null) return '—';
    if (job.lastSuccess) return this.l10n.translate('OK');
    return job.consecutiveFailures > 1 ? this.l10n.translate('Failed ({0} in a row)', job.consecutiveFailures) : this.l10n.translate('Failed');
  }

  private load(): void {
    this.loadQueue();
    this.loading.set(true);
    this.jobsApi.list().subscribe((jobs) => this.jobs.set(jobs));
    this.jobsApi.runs(this.page + 1, this.pageSize, this.sort || undefined).subscribe({
      next: (result) => {
        this.runs.set(result.items);
        this.runsTotal.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  private static formatInterval(minutes: number): string {
    if (minutes % 1440 === 0) return `${minutes / 1440} d`;
    if (minutes % 60 === 0) return `${minutes / 60} h`;
    return `${minutes} min`;
  }
}
