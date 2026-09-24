import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { DashboardService, DashboardWidget } from '../../../core/services/dashboard.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { LoadingState } from '../../../shared/components/loading-state/loading-state';
import { WidgetTile } from '../widget-tile/widget-tile';

/**
 * The dashboard shell (F-Dashboard). It knows no widget by name: it asks the API which widgets the user may see
 * (FR-DASH-002) and renders a tile for each, so a widget registered on the server appears here with no change
 * (AC-DASH-2). Tiles are deferred until scrolled into view and each loads its own data, so a slow widget never
 * blocks the page. The user can hide and reorder widgets; that layout is saved per user (FR-DASH-004).
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-dashboard',
  imports: [MatButtonModule, MatIconModule, MatSlideToggleModule, TranslatePipe, EmptyState, LoadingState, WidgetTile],
  styleUrl: './dashboard.scss',
  templateUrl: './dashboard.html'
})
export class Dashboard implements OnInit {
  private readonly api = inject(DashboardService);
  private readonly destroyRef = inject(DestroyRef);

  /** undefined while loading; the permitted widgets in the user's order once loaded. */
  readonly widgets = signal<DashboardWidget[] | undefined>(undefined);
  readonly loadFailed = signal(false);
  readonly saving = signal(false);

  readonly customizing = signal(false);
  readonly draft = signal<DashboardWidget[]>([]);

  readonly visible = computed(() => (this.widgets() ?? []).filter((w) => w.visible));

  ngOnInit(): void {
    this.api
      .widgets()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (widgets) => this.widgets.set(widgets),
        error: () => {
          this.loadFailed.set(true);
          this.widgets.set([]);
        }
      });
  }

  openCustomize(): void {
    this.draft.set((this.widgets() ?? []).map((w) => ({ ...w })));
    this.customizing.set(true);
  }

  closeCustomize(): void {
    this.customizing.set(false);
  }

  toggle(index: number): void {
    this.draft.update((list) => list.map((w, i) => (i === index ? { ...w, visible: !w.visible } : w)));
  }

  move(index: number, direction: -1 | 1): void {
    const target = index + direction;
    this.draft.update((list) => {
      if (target < 0 || target >= list.length) return list;
      const copy = [...list];
      [copy[index], copy[target]] = [copy[target], copy[index]];
      return copy;
    });
  }

  save(): void {
    this.persist(this.draft(), false);
  }

  /** Drops the personal layout; the server then returns everything visible in the default order. */
  reset(): void {
    this.persist([], true);
  }

  private persist(layout: DashboardWidget[], isDefault: boolean): void {
    this.saving.set(true);
    this.api
      .saveLayout(layout, isDefault)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.customizing.set(false);
          if (isDefault) this.reload();
          else this.widgets.set(layout);
        },
        error: () => this.saving.set(false) // the error interceptor already told the user why
      });
  }

  private reload(): void {
    this.api
      .widgets()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((widgets) => this.widgets.set(widgets));
  }
}
