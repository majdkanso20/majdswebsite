import { ExportFormat } from '../../../../core/utils/download';
import { ImportDialog, ImportDialogData } from '../../../../shared/components/import-dialog/import-dialog';
import { ImportsService } from '../../../../core/services/imports.service';
import { ExportMenu } from '../../../../shared/components/export-menu/export-menu';
import { ActiveFilter } from '../../../../shared/components/active-filter/active-filter';
import { LocalizationService } from '../../../../core/i18n/localization.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ExportsService, activeFilters } from '../../../../core/services/exports.service';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DataGrid } from '../../../../shared/components/data-grid/data-grid';
import { GridColumn, GridPage, GridSort } from '../../../../shared/components/data-grid/data-grid.model';
import { HasPermissionDirective } from '../../../../core/directives/has-permission.directive';
import { UsersApiService } from '../users-api.service';
import { RolesApiService } from '../../roles/roles-api.service';
import { UserDto } from '../user.models';
import { UserPermissionsDialog, UserPermissionsDialogData } from '../user-permissions-dialog/user-permissions-dialog';
import { UserFormDialog, UserFormDialogData, UserFormResult } from '../user-form-dialog/user-form-dialog';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-users-list',
  imports: [ActiveFilter, ExportMenu, TranslatePipe, DataGrid, HasPermissionDirective, MatButtonModule, MatIconModule, MatChipsModule, MatButtonToggleModule],
  styleUrl: './users-list.scss',
  templateUrl: './users-list.html'
})
export class UsersList {
  private readonly usersApi = inject(UsersApiService);
  private readonly rolesApi = inject(RolesApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly l10n = inject(LocalizationService);
  private readonly exportsApi = inject(ExportsService);
  private readonly importsApi = inject(ImportsService);
  private readonly router = inject(Router);

  readonly columns: GridColumn<UserDto>[] = [
    { key: 'email', header: 'Email', sortable: true },
    { key: 'fullName', header: 'Full name', sortable: true, value: (u) => u.fullName ?? '—' },
    { key: 'roles', header: 'Roles' },
    { key: 'status', header: 'Status' }
  ];

  readonly rows = signal<UserDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(true);
  /** The last load failed, so the grid offers a retry instead of looking empty. */
  readonly failed = signal(false);
  readonly roleOptions = signal<string[]>([]);

  readonly filter = signal('');
  /** Which accounts the list shows: everyone, only active ones, or only deactivated ones (FR-USER-001). */
  readonly status = signal<'all' | 'active' | 'inactive'>('all');
  private page = 0;
  private pageSize = 20;
  private sort = '';

  constructor() {
    this.rolesApi.list({ page: 1, pageSize: 100 }).subscribe((result) => {
      this.roleOptions.set(result.items.map((r) => r.name));
    });
    inject(ActivatedRoute).queryParamMap.subscribe((params) => {
      this.filter.set(params.get('q') ?? '');
      this.page = 0;
      this.load();
    });
  }

  setStatus(status: 'all' | 'active' | 'inactive'): void {
    this.status.set(status);
    this.page = 0;
    this.load();
  }

  /** The API takes true or false for a status filter, and nothing for "everyone". */
  private isActiveFilter(): boolean | undefined {
    const status = this.status();
    return status === 'all' ? undefined : status === 'active';
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
    const ref = this.dialog.open<UserFormDialog, UserFormDialogData, UserFormResult>(UserFormDialog, {
      data: { user: null, roleOptions: this.roleOptions() }
    });

    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.usersApi
        .create({
          email: result.email,
          password: result.password,
          sendSetPasswordEmail: result.sendSetPasswordEmail,
          fullName: result.fullName,
          roles: result.roles
        })
        .subscribe({
          next: () => {
            this.snackBar.open(
              result.sendSetPasswordEmail ? 'User created. A set-password email was sent.' : 'User created.',
              'Dismiss',
              { duration: 3000 }
            );
            this.load();
          },
          error: (err) => this.showError(err)
        });
    });
  }

  openPermissionsDialog(user: UserDto): void {
    this.dialog.open<UserPermissionsDialog, UserPermissionsDialogData>(UserPermissionsDialog, { data: { user } });
  }

  openEditDialog(user: UserDto): void {
    const ref = this.dialog.open<UserFormDialog, UserFormDialogData, UserFormResult>(UserFormDialog, {
      data: { user, roleOptions: this.roleOptions() }
    });

    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.usersApi
        .update({ userId: user.id, fullName: result.fullName, phoneNumber: result.phoneNumber, roles: result.roles })
        .subscribe({
          next: () => {
            this.snackBar.open('User updated.', 'Dismiss', { duration: 3000 });
            this.load();
          },
          error: (err) => this.showError(err)
        });
    });
  }

  toggleActivation(user: UserDto): void {
    this.usersApi.setActivation(user.id, !user.isActive).subscribe({
      next: () => {
        this.snackBar.open(user.isActive ? 'User deactivated.' : 'User activated.', 'Dismiss', { duration: 3000 });
        this.load();
      },
      error: (err) => this.showError(err)
    });
  }

  /** Queues the import as a background job (F-Export FR-EXP-004); the list is not reloaded here since nothing has
   * necessarily happened to it yet — Import history is where the outcome, and any row that failed, shows up. */
  openImportDialog(): void {
    this.dialog
      .open<ImportDialog, ImportDialogData, boolean>(ImportDialog, {
        data: {
          title: 'Import users',
          upload: (file) => this.importsApi.start('users', file),
          template: (format) => this.importsApi.template('users', format)
        },
        width: 'min(640px, 95vw)',
        maxWidth: '95vw'
      })
      .afterClosed()
      .subscribe((queued) => {
        if (queued) this.snackBar.open('Import queued. See Import history for progress.', 'Dismiss', { duration: 4000 });
      });
  }

  /** Queues the users export (with the list's filter) as a background job and points the user to the Exports page. */
  exportInBackground(format: 'csv' | 'xlsx'): void {
    this.exportsApi.start('users', format, activeFilters({ filter: this.filter(), isActive: this.isActiveFilter() })).subscribe({
      next: () => {
        this.snackBar
          .open('Export started. You will be notified when it is ready.', 'View', { duration: 6000 })
          .onAction()
          .subscribe(() => void this.router.navigate(['/exports']));
      },
      error: (err) => this.showError(err)
    });
  }

  export(format: ExportFormat): void {
    this.usersApi.export(format, { filter: this.filter() || undefined, isActive: this.isActiveFilter() }).subscribe({ error: (err) => this.showError(err) });
  }

  resetTwoFactor(user: UserDto): void {
    if (!confirm(this.l10n.translate('Turn off two-factor authentication for {0}? They can set it up again afterwards.', user.email))) return;
    this.usersApi.resetTwoFactor(user.id).subscribe({
      next: () => {
        this.snackBar.open('Two-factor authentication was reset.', 'Dismiss', { duration: 3000 });
        this.load();
      },
      error: (err) => this.showError(err)
    });
  }

  deleteUser(user: UserDto): void {
    if (!confirm(this.l10n.translate("Delete {0}? This can't be undone from the UI.", user.email))) return;
    this.usersApi.delete(user.id).subscribe({
      next: () => {
        this.snackBar.open('User deleted.', 'Dismiss', { duration: 3000 });
        this.load();
      },
      error: (err) => this.showError(err)
    });
  }

  resetPassword(user: UserDto): void {
    this.usersApi.resetPassword(user.id).subscribe({
      next: () => this.snackBar.open(this.l10n.translate('Password reset email sent to {0}.', user.email), 'Dismiss', { duration: 4000 }),
      error: (err) => this.showError(err)
    });
  }

  unlock(user: UserDto): void {
    this.usersApi.unlock(user.id).subscribe({
      next: () => {
        this.snackBar.open('Account unlocked.', 'Dismiss', { duration: 3000 });
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
    this.usersApi
      .list({ page: this.page + 1, pageSize: this.pageSize, sort: this.sort || undefined, filter: this.filter() || undefined, isActive: this.isActiveFilter() })
      .subscribe({
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

  defaultValueFallback(row: UserDto, column: GridColumn<UserDto>): string {
    if (column.value) return column.value(row);
    const raw = row[column.key as keyof UserDto];
    return raw === null || raw === undefined ? '' : String(raw);
  }

  private showError(err: unknown): void {
    const message = (err as { error?: { message?: string } })?.error?.message ?? 'Something went wrong.';
    this.snackBar.open(message, 'Dismiss', { duration: 5000 });
  }
}
