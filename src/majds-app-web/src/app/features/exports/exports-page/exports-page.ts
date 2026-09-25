import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ExportJob, ExportsService } from '../../../core/services/exports.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { LoadingState } from '../../../shared/components/loading-state/loading-state';

/**
 * "My exports" (F-Export FR-EXP-004): the background exports the user started, with their status and a download for the
 * finished ones. Refreshes itself while any export is still queued or running, then stops.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-exports-page',
  imports: [DatePipe, MatButtonModule, MatIconModule, TranslatePipe, EmptyState, LoadingState],
  styleUrl: './exports-page.scss',
  templateUrl: './exports-page.html'
})
export class ExportsPage implements OnInit {
  private readonly api = inject(ExportsService);
  private readonly destroyRef = inject(DestroyRef);

  private static readonly RefreshMs = 3000;

  readonly jobs = signal<ExportJob[] | undefined>(undefined);
  readonly downloading = signal<string | null>(null);
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
        if (this.hasActive()) this.timer = setTimeout(() => this.refresh(), ExportsPage.RefreshMs);
      },
      error: () => this.jobs.set([])
    });
  }

  download(job: ExportJob): void {
    this.downloading.set(job.id);
    this.api.download(job).subscribe({
      next: () => this.downloading.set(null),
      error: () => this.downloading.set(null)
    });
  }

  icon(job: ExportJob): string {
    switch (job.status) {
      case 'Completed':
        return 'check_circle';
      case 'Failed':
        return 'error';
      default:
        return 'hourglass_top';
    }
  }
}
