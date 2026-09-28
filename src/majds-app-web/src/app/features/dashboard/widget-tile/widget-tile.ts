import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Subscription } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { LocalDatePipe, LocalNumberPipe } from '../../../core/i18n/format.pipes';
import { FormattingService } from '../../../core/i18n/formatting.service';
import { LocalizationService } from '../../../core/i18n/localization.service';
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
  imports: [LocalDatePipe, LocalNumberPipe, MatCardModule, TranslatePipe, LoadingState, EmptyState],
  styleUrl: './widget-tile.scss',
  templateUrl: './widget-tile.html'
})
export class WidgetTile {
  private readonly api = inject(DashboardService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fmt = inject(FormattingService);
  private readonly l10n = inject(LocalizationService);
  private request?: Subscription;

  readonly widget = input.required<DashboardWidget>();

  readonly failed = signal(false);
  private readonly data = signal<WidgetData | null | undefined>(undefined); // undefined = still loading

  readonly loading = computed(() => this.data() === undefined && !this.failed());
  readonly kpi = computed(() => (this.widget().kind === 'kpi' ? (this.data() as KpiData | null) : null));
  /** A KPI that is a number is shown with the language's digits and separators; anything else is shown as sent. */
  readonly kpiValue = computed(() => {
    const value = this.kpi()?.value ?? '';
    return value.trim() !== '' && Number.isFinite(Number(value)) ? this.fmt.number(Number(value)) : value;
  });

  readonly chart = computed(() => (this.widget().kind === 'chart' ? (this.data() as ChartData | null) : null));
  readonly feed = computed(() => (this.widget().kind === 'feed' ? (this.data() as FeedData | null) : null));

  /** Tallest bar is 100%; an all-zero series draws no bars rather than dividing by zero. */
  readonly chartMax = computed(() => Math.max(1, ...(this.chart()?.points.map((p) => p.value) ?? [0])));

  readonly chartSummary = computed(() => (this.chart()?.points ?? []).map((p) => `${p.label}: ${p.value}`).join(', '));

  constructor() {
    this.destroyRef.onDestroy(() => this.request?.unsubscribe());

    // Some of what a widget shows is worded by the server (a caption, the day names), in the language of the request. So the data is fetched
    // again when the language changes, instead of staying in the language it was first loaded in. The old figures stay up until the new ones arrive.
    effect(() => {
      this.l10n.language();
      untracked(() => this.load());
    });
  }

  private load(): void {
    this.request?.unsubscribe();
    this.failed.set(false);
    this.request = this.api.data(this.widget().key).subscribe({
      next: (data) => this.data.set(data),
      error: () => this.failed.set(true)
    });
  }
}
