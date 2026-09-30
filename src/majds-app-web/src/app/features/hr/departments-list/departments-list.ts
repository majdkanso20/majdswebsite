import { ActiveFilter } from '../../../shared/components/active-filter/active-filter';
import { LocalizationService } from '../../../core/i18n/localization.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DataGrid } from '../../../shared/components/data-grid/data-grid';
import { GridColumn, GridPage, GridSort } from '../../../shared/components/data-grid/data-grid.model';
import { HasPermissionDirective } from '../../../core/directives/has-permission.directive';
import { HrApiService } from '../hr-api.service';
import { DepartmentDto } from '../hr.models';
import { DepartmentFormDialog, DepartmentFormDialogData, DepartmentFormResult } from '../department-form-dialog/department-form-dialog';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-departments-list',
  imports: [ActiveFilter, TranslatePipe, DataGrid, HasPermissionDirective, MatButtonModule, MatIconModule],
  styleUrl: './departments-list.scss',
  templateUrl: './departments-list.html'
})
export class DepartmentsList {
  private readonly hrApi = inject(HrApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly l10n = inject(LocalizationService);

  readonly columns: GridColumn<DepartmentDto>[] = [
    { key: 'name', header: 'Name', sortable: true },
    { key: 'description', header: 'Description', value: (d) => d.description ?? '—' },
    { key: 'employeeCount', header: 'Employees', value: (d) => String(d.employeeCount) }
  ];

  readonly rows = signal<DepartmentDto[]>([]);
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

  openCreateDialog(): void {
    const ref = this.dialog.open<DepartmentFormDialog, DepartmentFormDialogData, DepartmentFormResult>(DepartmentFormDialog, {
      data: { department: null }
    });

    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.hrApi.createDepartment(result.name, result.description).subscribe({
        next: () => {
          this.snackBar.open('Department created.', 'Dismiss', { duration: 3000 });
          this.load();
        },
        error: (err) => this.showError(err)
      });
    });
  }

  openEditDialog(department: DepartmentDto): void {
    const ref = this.dialog.open<DepartmentFormDialog, DepartmentFormDialogData, DepartmentFormResult>(DepartmentFormDialog, {
      data: { department }
    });

    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.hrApi.updateDepartment(department.id, result.name, result.description).subscribe({
        next: () => {
          this.snackBar.open('Department updated.', 'Dismiss', { duration: 3000 });
          this.load();
        },
        error: (err) => this.showError(err)
      });
    });
  }

  deleteDepartment(department: DepartmentDto): void {
    if (!confirm(this.l10n.translate("Delete department '{0}'?", department.name))) return;

    this.hrApi.deleteDepartment(department.id).subscribe({
      next: () => {
        this.snackBar.open('Department deleted.', 'Dismiss', { duration: 3000 });
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
    this.hrApi.listDepartments({ page: this.page + 1, pageSize: this.pageSize, sort: this.sort || undefined, filter: this.filter() || undefined }).subscribe({
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
