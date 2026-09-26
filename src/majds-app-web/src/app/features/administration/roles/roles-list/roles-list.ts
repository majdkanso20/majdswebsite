import { ActiveFilter } from '../../../../shared/components/active-filter/active-filter';
import { LocalizationService } from '../../../../core/i18n/localization.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DataGrid } from '../../../../shared/components/data-grid/data-grid';
import { GridColumn, GridPage, GridSort } from '../../../../shared/components/data-grid/data-grid.model';
import { HasPermissionDirective } from '../../../../core/directives/has-permission.directive';
import { RolesApiService } from '../roles-api.service';
import { RoleDto } from '../role.models';
import { RoleFormDialog, RoleFormDialogData, RoleFormResult } from '../role-form-dialog/role-form-dialog';
import { RoleReassignDialog, RoleReassignDialogData } from '../role-reassign-dialog/role-reassign-dialog';
import { RolePermissionsDialog, RolePermissionsDialogData } from '../role-permissions-dialog/role-permissions-dialog';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-roles-list',
  imports: [ActiveFilter, TranslatePipe, DataGrid, HasPermissionDirective, MatButtonModule, MatIconModule, MatChipsModule],
  styleUrl: './roles-list.scss',
  templateUrl: './roles-list.html'
})
export class RolesList {
  private readonly rolesApi = inject(RolesApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly l10n = inject(LocalizationService);

  readonly columns: GridColumn<RoleDto>[] = [
    { key: 'name', header: 'Name', sortable: true },
    { key: 'displayName', header: 'Display name', sortable: true, value: (r) => r.displayName ?? '—' },
    { key: 'userCount', header: 'Users', value: (r) => String(r.userCount) },
    { key: 'flags', header: 'Type' }
  ];

  readonly rows = signal<RoleDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(true);
  /** The last load failed, so the grid offers a retry instead of looking empty. */
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
    const ref = this.dialog.open<RoleFormDialog, RoleFormDialogData, RoleFormResult>(RoleFormDialog, {
      data: { role: null }
    });

    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.rolesApi.create(result.name, result.displayName, result.isDefault).subscribe({
        next: () => {
          this.snackBar.open('Role created.', 'Dismiss', { duration: 3000 });
          this.load();
        },
        error: (err) => this.showError(err)
      });
    });
  }

  openEditDialog(role: RoleDto): void {
    const ref = this.dialog.open<RoleFormDialog, RoleFormDialogData, RoleFormResult>(RoleFormDialog, {
      data: { role }
    });

    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.rolesApi.update(role.id, result.displayName, result.isDefault).subscribe({
        next: () => {
          this.snackBar.open('Role updated.', 'Dismiss', { duration: 3000 });
          this.load();
        },
        error: (err) => this.showError(err)
      });
    });
  }

  openPermissionsDialog(role: RoleDto): void {
    this.dialog.open<RolePermissionsDialog, RolePermissionsDialogData>(RolePermissionsDialog, { data: { role } });
  }

  deleteRole(role: RoleDto): void {
    // A role with users cannot just disappear: ask which role takes them over first (FR-ROLE-004).
    if (role.userCount > 0) {
      this.dialog
        .open<RoleReassignDialog, RoleReassignDialogData, string>(RoleReassignDialog, { data: { role } })
        .afterClosed()
        .subscribe((target) => {
          if (target) this.performDelete(role, target);
        });
      return;
    }

    if (!confirm(this.l10n.translate("Delete role '{0}'?", role.name))) return;
    this.performDelete(role);
  }

  private performDelete(role: RoleDto, reassignTo?: string): void {
    this.rolesApi.delete(role.id, reassignTo).subscribe({
      next: () => {
        this.snackBar.open('Role deleted.', 'Dismiss', { duration: 3000 });
        this.load();
      },
      error: (err) => this.showError(err)
    });
  }

  defaultValueFallback(row: RoleDto, column: GridColumn<RoleDto>): string {
    if (column.value) return column.value(row);
    const raw = row[column.key as keyof RoleDto];
    return raw === null || raw === undefined ? '' : String(raw);
  }

  reload(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.rolesApi.list({ page: this.page + 1, pageSize: this.pageSize, sort: this.sort || undefined, filter: this.filter() || undefined }).subscribe({
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
