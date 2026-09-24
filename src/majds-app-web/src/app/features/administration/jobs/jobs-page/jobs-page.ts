import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DataGrid } from '../../../../shared/components/data-grid/data-grid';
import { GridColumn, GridPage, GridSort } from '../../../../shared/components/data-grid/data-grid.model';
import { HasPermissionDirective } from '../../../../core/directives/has-permission.directive';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { JobDto, JobRunDto, JobsService } from '../jobs.service';

const asLocal = (utc: string | null) => (utc ? new Date(utc + 'Z').toLocaleString() : '—');

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-jobs-page',
  imports: [DataGrid, HasPermissionDirective, TranslatePipe, MatButtonModule, MatIconModule],
  styleUrl: './jobs-page.scss',
  templateUrl: './jobs-page.html'
})
export class JobsPage {
  private readonly jobsApi = inject(JobsService);
  private readonly snackBar = inject(MatSnackBar);

  readonly jobColumns: GridColumn<JobDto>[] = [
    { key: 'name', header: 'Job' },
    { key: 'intervalMinutes', header: 'Every', value: (j) => JobsPage.formatInterval(j.intervalMinutes) },
    { key: 'lastRunAt', header: 'Last run', value: (j) => asLocal(j.lastRunAt) },
    { key: 'lastSuccess', header: 'Last result', value: (j) => (j.lastSuccess === null ? '—' : j.lastSuccess ? 'OK' : 'Failed') },
    { key: 'nextRunAt', header: 'Next run', value: (j) => asLocal(j.nextRunAt) }
  ];

  readonly runColumns: GridColumn<JobRunDto>[] = [
    { key: 'startedAt', header: 'When', sortable: true, value: (r) => asLocal(r.startedAt) },
    { key: 'jobName', header: 'Job', sortable: true },
    { key: 'trigger', header: 'Trigger' },
    { key: 'durationMs', header: 'Duration', sortable: true, value: (r) => `${r.durationMs} ms` },
    { key: 'success', header: 'Result', value: (r) => (r.success ? 'OK' : `Failed: ${r.error ?? ''}`) }
  ];

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

  runNow(job: JobDto): void {
    this.jobsApi.run(job.name).subscribe({
      next: (ran) => {
        this.snackBar.open(ran ? `"${job.name}" finished.` : `"${job.name}" is already running.`, 'Dismiss', { duration: 3000 });
        this.load();
      },
      error: (err) => {
        const message = (err as { error?: { message?: string } })?.error?.message ?? 'Something went wrong.';
        this.snackBar.open(message, 'Dismiss', { duration: 5000 });
      }
    });
  }

  private load(): void {
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
