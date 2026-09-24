import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { environment } from '../../../../environments/environment';
import { ErrorState } from '../../../shared/components/error-state/error-state';
import { DataGrid } from '../../../shared/components/data-grid/data-grid';
import { GridColumn, GridPage, GridSort } from '../../../shared/components/data-grid/data-grid.model';
import { HasPermissionDirective } from '../../../core/directives/has-permission.directive';
import { ResponseDto, PagedResponse } from '../../../core/models/response-dto';
import { PluginRecordDialog, PluginRecordDialogData } from '../plugin-record-dialog/plugin-record-dialog';
import { PluginRecord, PluginUiSchema } from '../plugin.models';

/**
 * The ONE generic page every metadata-driven plugin (P5) is rendered through: it knows nothing about
 * "Tasks" specifically — it fetches the plugin's own ui-schema, then drives the shared DataGrid and a
 * schema-built dialog off it, calling the plugin's own list/create/update/delete endpoints. A second
 * CRUD plugin needs zero new Angular code, only a new backend project + manifest.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-plugin-crud-page',
  imports: [TranslatePipe, ErrorState, DataGrid, HasPermissionDirective, MatButtonModule, MatIconModule],
  styleUrl: './plugin-crud-page.scss',
  templateUrl: './plugin-crud-page.html'
})
export class PluginCrudPage {
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  // The dynamically-registered route's own path segment doubles as the API path segment by
  // convention (PluginsService registers both from the same manifest `route` value).
  private readonly apiBase = `${environment.apiBaseUrl}/${inject(ActivatedRoute).snapshot.routeConfig?.path}`;

  readonly schema = signal<PluginUiSchema | null>(null);
  readonly columns = signal<GridColumn<PluginRecord>[]>([]);
  readonly rows = signal<PluginRecord[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);

  private page = 0;
  private pageSize = 20;
  private sort = '';

  constructor() {
    void this.init();
  }

  /** Public so the error state's "Try again" can call it (FR-SHELL-005, FR-PLUG-019: a plugin that fails to load shows a placeholder, never a blank shell). */
  async init(): Promise<void> {
    this.loadFailed.set(false);
    try {
      const response = await firstValueFrom(this.http.get<ResponseDto<PluginUiSchema>>(`${this.apiBase}/ui-schema`));
      const schema = response.data!;
      this.schema.set(schema);
      this.columns.set(schema.columns.map((c) => ({ key: c.key, header: c.header })));
      this.load();
    } catch {
      this.loadFailed.set(true);
    }
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

  openCreateDialog(): void {
    const schema = this.schema()!;
    const ref = this.dialog.open<PluginRecordDialog, PluginRecordDialogData, Record<string, unknown>>(PluginRecordDialog, {
      data: { title: schema.title, fields: schema.fields, record: null }
    });

    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      firstValueFrom(this.http.post<ResponseDto<string>>(`${this.apiBase}/create`, result)).then(
        () => {
          this.snackBar.open('Created.', 'Dismiss', { duration: 3000 });
          this.load();
        },
        (err) => this.showError(err)
      );
    });
  }

  openEditDialog(row: PluginRecord): void {
    const schema = this.schema()!;
    const ref = this.dialog.open<PluginRecordDialog, PluginRecordDialogData, Record<string, unknown>>(PluginRecordDialog, {
      data: { title: schema.title, fields: schema.fields, record: row }
    });

    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      firstValueFrom(this.http.post<ResponseDto<null>>(`${this.apiBase}/update`, { id: row['id'], ...result })).then(
        () => {
          this.snackBar.open('Saved.', 'Dismiss', { duration: 3000 });
          this.load();
        },
        (err) => this.showError(err)
      );
    });
  }

  deleteRow(row: PluginRecord): void {
    if (!confirm('Delete this record?')) return;
    firstValueFrom(this.http.post<ResponseDto<null>>(`${this.apiBase}/delete`, { id: row['id'] })).then(
      () => {
        this.snackBar.open('Deleted.', 'Dismiss', { duration: 3000 });
        this.load();
      },
      (err) => this.showError(err)
    );
  }

  defaultValue(row: PluginRecord, key: string): string {
    const raw = row[key];
    return raw === null || raw === undefined ? '' : String(raw);
  }

  private load(): void {
    this.loading.set(true);
    const params: Record<string, string | number> = { page: this.page + 1, pageSize: this.pageSize };
    if (this.sort) params['sort'] = this.sort;

    firstValueFrom(this.http.get<ResponseDto<PagedResponse<PluginRecord>>>(`${this.apiBase}/list`, { params })).then(
      (response) => {
        this.rows.set(response.data?.items ?? []);
        this.totalCount.set(response.data?.totalCount ?? 0);
        this.loading.set(false);
      },
      () => this.loading.set(false)
    );
  }

  private showError(err: unknown): void {
    const message = (err as { error?: { message?: string } })?.error?.message ?? 'Something went wrong.';
    this.snackBar.open(message, 'Dismiss', { duration: 5000 });
  }
}
