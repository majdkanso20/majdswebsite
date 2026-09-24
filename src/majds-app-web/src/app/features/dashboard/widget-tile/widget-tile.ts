import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ChartData, DashboardService, DashboardWidget, FeedData, KpiData, WidgetData } from '../../../core/services/dashboard.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { LoadingState } from '../../../shared/components/loading-state/loading-state';

/**
 * One dashboard tile (FR-DASH-001). It loads its own data, so a slow or failing widget never affects the others,
 * and it renders by kind: a KPI figure, a bar chart, or an activity feed. Purely presentational, styled with theme
 * tokens only, so every skin applies (U1).
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-widget-tile',
  imports: [DatePipe, MatCardModule, TranslatePipe, LoadingState, EmptyState],
  styleUrl: './widget-tile.scss',
  templateUrl: './widget-tile.html'
})
export class WidgetTile implements OnInit {
  private readonly api = inject(DashboardService);
  private readonly destroyRef = inject(DestroyRef);

  readonly widget = input.required<DashboardWidget>();

  readonly failed = signal(false);
  private readonly data = signal<WidgetData | null | undefined>(undefined); // undefined = still loading

  readonly loading = computed(() => this.data() === undefined && !this.failed());
  readonly kpi = computed(() => (this.widget().kind === 'kpi' ? (this.data() as KpiData | null) : null));
  readonly chart = computed(() => (this.widget().kind === 'chart' ? (this.data() as ChartData | null) : null));
  readonly feed = computed(() => (this.widget().kind === 'feed' ? (this.data() as FeedData | null) : null));

  /** Tallest bar is 100%; an all-zero series draws no bars rather than dividing by zero. */
  readonly chartMax = computed(() => Math.max(1, ...(this.chart()?.points.map((p) => p.value) ?? [0])));

  readonly chartSummary = computed(() => (this.chart()?.points ?? []).map((p) => `${p.label}: ${p.value}`).join(', '));

  ngOnInit(): void {
    this.api
      .data(this.widget().key)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => this.data.set(data),
        error: () => this.failed.set(true)
      });
  }
}
