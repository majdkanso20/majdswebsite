import { ExportFormat } from '../../../../core/utils/download';
import { ExportMenu } from '../../../../shared/components/export-menu/export-menu';
import { ActiveFilter } from '../../../../shared/components/active-filter/active-filter';
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
import { UsersApiService } from '../users-api.service';
import { RolesApiService } from '../../roles/roles-api.service';
import { UserDto } from '../user.models';
import { UserFormDialog, UserFormDialogData, UserFormResult } from '../user-form-dialog/user-form-dialog';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-users-list',
  imports: [ActiveFilter, ExportMenu, TranslatePipe, DataGrid, HasPermissionDirective, MatButtonModule, MatIconModule, MatChipsModule],
  styleUrl: './users-list.scss',
  templateUrl: './users-list.html'
})
export class UsersList {
  private readonly usersApi = inject(UsersApiService);
  private readonly rolesApi = inject(RolesApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly columns: GridColumn<UserDto>[] = [
    { key: 'email', header: 'Email', sortable: true },
    { key: 'fullName', header: 'Full name', sortable: true, value: (u) => u.fullName ?? '—' },
    { key: 'roles', header: 'Roles' },
    { key: 'status', header: 'Status' }
  ];

  readonly rows = signal<UserDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(true);
  readonly roleOptions = signal<string[]>([]);

  readonly filter = signal('');
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
        .create({ email: result.email, password: result.password!, fullName: result.fullName, roles: result.roles })
        .subscribe({
          next: () => {
            this.snackBar.open('User created.', 'Dismiss', { duration: 3000 });
            this.load();
          },
          error: (err) => this.showError(err)
        });
    });
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

  export(format: ExportFormat): void {
    this.usersApi.export(format, { filter: this.filter() || undefined }).subscribe({ error: (err) => this.showError(err) });
  }

  resetTwoFactor(user: UserDto): void {
    if (!confirm(`Turn off two-factor authentication for ${user.email}? They can set it up again afterwards.`)) return;
    this.usersApi.resetTwoFactor(user.id).subscribe({
      next: () => {
        this.snackBar.open('Two-factor authentication was reset.', 'Dismiss', { duration: 3000 });
        this.load();
      },
      error: (err) => this.showError(err)
    });
  }

  deleteUser(user: UserDto): void {
    if (!confirm(`Delete ${user.email}? This can't be undone from the UI.`)) return;
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
      next: () => this.snackBar.open(`Password reset email sent to ${user.email}.`, 'Dismiss', { duration: 4000 }),
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

  private load(): void {
    this.loading.set(true);
    this.usersApi
      .list({ page: this.page + 1, pageSize: this.pageSize, sort: this.sort || undefined, filter: this.filter() || undefined })
      .subscribe({
        next: (result) => {
          this.rows.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
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
