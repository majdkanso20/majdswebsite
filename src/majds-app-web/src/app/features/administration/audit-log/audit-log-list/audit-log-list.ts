import { ExportFormat } from '../../../../core/utils/download';
import { ExportMenu } from '../../../../shared/components/export-menu/export-menu';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { DataGrid } from '../../../../shared/components/data-grid/data-grid';
import { GridColumn, GridPage, GridSort } from '../../../../shared/components/data-grid/data-grid.model';
import { AuditLogApiService } from '../audit-log-api.service';
import { AuditLogEntryDto } from '../audit-log.models';
import { AuditLogDetailDialog } from '../audit-log-detail-dialog/audit-log-detail-dialog';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-audit-log-list',
  imports: [
    TranslatePipe, FormsModule, DataGrid, ExportMenu, MatButtonModule, MatChipsModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatSelectModule
  ],
  styleUrl: './audit-log-list.scss',
  templateUrl: './audit-log-list.html'
})
export class AuditLogList {
  private readonly auditLogApi = inject(AuditLogApiService);
  private readonly dialog = inject(MatDialog);

  readonly columns: GridColumn<AuditLogEntryDto>[] = [
    { key: 'createdAt', header: 'When', sortable: true, value: (a) => new Date(a.createdAt + 'Z').toLocaleString() },
    { key: 'action', header: 'Action', sortable: true },
    { key: 'userName', header: 'User', sortable: true, value: (a) => a.userName ?? '—' },
    { key: 'outcome', header: 'Outcome', sortable: true },
    { key: 'durationMs', header: 'Duration', sortable: true, value: (a) => `${a.durationMs} ms` },
    { key: 'clientIp', header: 'IP address', value: (a) => a.clientIp ?? '—' }
  ];

  readonly outcomes = ['Success', 'Failure', 'Forbidden', 'Invalid', 'NotFound', 'Conflict', 'Unauthorized', 'Error'];

  readonly rows = signal<AuditLogEntryDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(true);

  outcome = '';
  from = '';
  to = '';

  private page = 0;
  private pageSize = 20;
  private sort = 'createdAt:desc';

  constructor() {
    this.load();
  }

  applyFilters(): void {
    this.page = 0;
    this.load();
  }

  clearFilters(): void {
    this.outcome = '';
    this.from = '';
    this.to = '';
    this.applyFilters();
  }

  get hasFilters(): boolean {
    return !!(this.outcome || this.from || this.to);
  }

  export(format: ExportFormat): void {
    this.auditLogApi.export(format, this.filterParams()).subscribe();
  }

  openDetails(entry: AuditLogEntryDto): void {
    this.dialog.open(AuditLogDetailDialog, { data: { id: entry.id }, width: 'min(720px, 95vw)', maxWidth: '95vw' });
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

  private filterParams() {
    return { outcome: this.outcome || undefined, from: this.from || undefined, to: this.to || undefined };
  }

  private load(): void {
    this.loading.set(true);
    this.auditLogApi
      .list({ page: this.page + 1, pageSize: this.pageSize, sort: this.sort || undefined, ...this.filterParams() })
      .subscribe({
        next: (result) => {
          this.rows.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      });
  }
}
