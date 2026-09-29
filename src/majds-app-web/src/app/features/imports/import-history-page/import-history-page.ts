import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { LocalDatePipe } from '../../../core/i18n/format.pipes';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ImportJob, ImportsService } from '../../../core/services/imports.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { LoadingState } from '../../../shared/components/loading-state/loading-state';

/**
 * "Import history" (F-Export FR-EXP-003/004): the background imports the user started, with their status and,
 * once finished, how many rows succeeded and — for the rest — the row and why. Refreshes itself while any import
 * is still queued or running, then stops.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-import-history-page',
  imports: [LocalDatePipe, MatButtonModule, MatIconModule, TranslatePipe, EmptyState, LoadingState],
  styleUrl: './import-history-page.scss',
  templateUrl: './import-history-page.html'
})
export class ImportHistoryPage implements OnInit {
  private readonly api = inject(ImportsService);
  private readonly destroyRef = inject(DestroyRef);

  private static readonly RefreshMs = 3000;

  readonly jobs = signal<ImportJob[] | undefined>(undefined);
  readonly hasActive = computed(() => (this.jobs() ?? []).some((j) => j.status === 'Pending' || j.status === 'Running'));

  private timer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    this.destroyRef.onDestroy(() => clearTimeout(this.timer));
    this.refresh();
  }

  refresh(): void {
    this.api.list().subscribe({
      next: (jobs) => {
        this.jobs.set(jobs);
        clearTimeout(this.timer);
        if (this.hasActive()) this.timer = setTimeout(() => this.refresh(), ImportHistoryPage.RefreshMs);
      },
      error: () => this.jobs.set([])
    });
  }

  icon(job: ImportJob): string {
    switch (job.status) {
      case 'Completed':
        return job.errors && job.errors.length > 0 ? 'warning' : 'check_circle';
      case 'Failed':
        return 'error';
      default:
        return 'hourglass_top';
    }
  }
}
