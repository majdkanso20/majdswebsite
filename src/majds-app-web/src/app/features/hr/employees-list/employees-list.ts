import { ActiveFilter } from '../../../shared/components/active-filter/active-filter';
import { LocalizationService } from '../../../core/i18n/localization.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DataGrid } from '../../../shared/components/data-grid/data-grid';
import { GridColumn, GridPage, GridSort } from '../../../shared/components/data-grid/data-grid.model';
import { HasPermissionDirective } from '../../../core/directives/has-permission.directive';
import { HrApiService } from '../hr-api.service';
import { EmployeeDto, EmployeeInput } from '../hr.models';
import { EmployeeFormDialog, EmployeeFormDialogData } from '../employee-form-dialog/employee-form-dialog';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-employees-list',
  imports: [ActiveFilter, TranslatePipe, DataGrid, HasPermissionDirective, MatButtonModule, MatIconModule, MatChipsModule],
  styleUrl: './employees-list.scss',
  templateUrl: './employees-list.html'
})
export class EmployeesList {
  private readonly hrApi = inject(HrApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly l10n = inject(LocalizationService);

  readonly columns: GridColumn<EmployeeDto>[] = [
    { key: 'fullName', header: 'Full name', sortable: true },
    { key: 'email', header: 'Email', sortable: true },
    { key: 'jobTitle', header: 'Job title', value: (e) => e.jobTitle ?? '—' },
    { key: 'departmentName', header: 'Department', value: (e) => e.departmentName ?? '—' },
    { key: 'status', header: 'Status', sortable: true }
  ];

  readonly rows = signal<EmployeeDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly filter = signal('');

  private page = 0;
  private pageSize = 20;
  private sort = '';

  constructor() {
    inject(ActivatedRoute).queryParamMap.subscribe((params) => {
      this.filter.set(params.get('q') ?? '');
      this.page = 0;
      this.load();
    });
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

  defaultValueFallback(row: EmployeeDto, column: GridColumn<EmployeeDto>): string {
    if (column.value) return column.value(row);
    const raw = row[column.key as keyof EmployeeDto];
    return raw === null || raw === undefined ? '' : String(raw);
  }

  openCreateDialog(): void {
    const ref = this.dialog.open<EmployeeFormDialog, EmployeeFormDialogData, EmployeeInput>(EmployeeFormDialog, {
      data: { employee: null }
    });

    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.hrApi.createEmployee(result).subscribe({
        next: () => {
          this.snackBar.open('Employee created.', 'Dismiss', { duration: 3000 });
          this.load();
        },
        error: (err) => this.showError(err)
      });
    });
  }

  openEditDialog(employee: EmployeeDto): void {
    const ref = this.dialog.open<EmployeeFormDialog, EmployeeFormDialogData, EmployeeInput>(EmployeeFormDialog, {
      data: { employee }
    });

    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.hrApi.updateEmployee(employee.id, result).subscribe({
        next: () => {
          this.snackBar.open('Employee updated.', 'Dismiss', { duration: 3000 });
          this.load();
        },
        error: (err) => this.showError(err)
      });
    });
  }

  deleteEmployee(employee: EmployeeDto): void {
    if (!confirm(this.l10n.translate("Delete employee '{0}'?", employee.fullName))) return;

    this.hrApi.deleteEmployee(employee.id).subscribe({
      next: () => {
        this.snackBar.open('Employee deleted.', 'Dismiss', { duration: 3000 });
        this.load();
      },
      error: (err) => this.showError(err)
    });
  }

  reload(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.hrApi.listEmployees({ page: this.page + 1, pageSize: this.pageSize, sort: this.sort || undefined, filter: this.filter() || undefined }).subscribe({
      next: (result) => {
        this.rows.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.failed.set(true);
      }
    });
  }

  private showError(err: unknown): void {
    const message = (err as { error?: { message?: string } })?.error?.message ?? 'Something went wrong.';
    this.snackBar.open(message, 'Dismiss', { duration: 5000 });
  }
}
