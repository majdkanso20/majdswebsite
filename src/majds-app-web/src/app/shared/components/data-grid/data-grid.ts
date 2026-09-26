import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, ContentChild, TemplateRef, computed, input, output } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { EmptyState } from '../empty-state/empty-state';
import { ErrorState } from '../error-state/error-state';
import { GridColumn, GridPage, GridSort } from './data-grid.model';

/**
 * Reusable server-side paged/sorted grid (F-Data FR-GRID-005): presentational only — the consumer
 * owns the actual HTTP call and passes rows/totalCount/loading in, and reacts to (page)/(sort)
 * outputs. Cell rendering and row actions are fully overridable via content-projected templates
 * (U1 FR-UI-005) rather than hard-coded here, so this component never changes per feature.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-data-grid',
  imports: [TranslatePipe, NgTemplateOutlet, MatTableModule, MatPaginatorModule, MatSortModule, MatProgressBarModule, EmptyState, ErrorState],
  styleUrl: './data-grid.scss',
  templateUrl: './data-grid.html'
})
export class DataGrid<T extends object> {
  readonly columns = input.required<GridColumn<T>[]>();
  readonly rows = input<T[]>([]);
  readonly totalCount = input(0);
  readonly loading = input(false);
  readonly pageSize = input(20);
  readonly pageSizeOptions = input<number[]>([10, 20, 50]);
  readonly emptyMessage = input('No records found.');
  /** Set when the rows could not be loaded, so the grid shows the error state instead of an empty table. */
  readonly failed = input(false);
  readonly errorMessage = input('The list could not be loaded.');

  readonly page = output<GridPage>();
  readonly sortChange = output<GridSort>();
  /** The error state's "Try again" was pressed. */
  readonly retry = output<void>();

  /** Consumer-supplied cell renderer: <ng-template #cellTemplate let-row let-column="column">...</ng-template> */
  @ContentChild('cellTemplate') cellTemplate?: TemplateRef<{ $implicit: T; column: GridColumn<T> }>;

  /** Consumer-supplied row actions: <ng-template #actionsTemplate let-row>...</ng-template> */
  @ContentChild('actionsTemplate') actionsTemplate?: TemplateRef<{ $implicit: T }>;

  /** Replaces the progress bar shown while loading: <ng-template #loadingTemplate>...</ng-template> (FR-SHELL-005) */
  @ContentChild('loadingTemplate') loadingTemplate?: TemplateRef<unknown>;

  /** Replaces the empty state: <ng-template #emptyTemplate>...</ng-template> */
  @ContentChild('emptyTemplate') emptyTemplate?: TemplateRef<unknown>;

  /** Replaces the error state: <ng-template #errorTemplate>...</ng-template> */
  @ContentChild('errorTemplate') errorTemplate?: TemplateRef<unknown>;

  readonly displayedColumns = computed(() => {
    const keys = this.columns().map((c) => c.key);
    return this.actionsTemplate ? [...keys, 'actions'] : keys;
  });

  defaultValue(row: T, column: GridColumn<T>): string {
    if (column.value) return column.value(row);
    const raw = (row as Record<string, unknown>)[column.key];
    return raw === null || raw === undefined ? '' : String(raw);
  }

  onPage(event: PageEvent): void {
    this.page.emit({ pageIndex: event.pageIndex, pageSize: event.pageSize });
  }

  onSort(sort: Sort): void {
    this.sortChange.emit({ active: sort.active, direction: sort.direction });
  }
}
