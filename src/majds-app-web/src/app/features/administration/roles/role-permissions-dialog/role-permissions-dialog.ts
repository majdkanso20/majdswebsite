import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { RolesApiService } from '../roles-api.service';
import { PermissionGroup, RoleDto } from '../role.models';

export interface RolePermissionsDialogData {
  role: RoleDto;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-role-permissions-dialog',
  imports: [TranslatePipe, FormsModule, MatDialogModule, MatCheckboxModule, MatButtonModule, MatProgressSpinnerModule],
  styleUrl: './role-permissions-dialog.scss',
  templateUrl: './role-permissions-dialog.html'
})
export class RolePermissionsDialog {
  private readonly rolesApi = inject(RolesApiService);
  private readonly dialogRef = inject(MatDialogRef<RolePermissionsDialog>);
  readonly data = inject<RolePermissionsDialogData>(MAT_DIALOG_DATA);

  readonly loading = signal(true);
  readonly groups = signal<PermissionGroup[]>([]);
  readonly granted = signal<Set<string>>(new Set());

  constructor() {
    forkJoin({
      tree: this.rolesApi.getPermissionTree(),
      granted: this.rolesApi.getRolePermissions(this.data.role.id)
    }).subscribe(({ tree, granted }) => {
      this.groups.set(tree);
      this.granted.set(new Set(granted));
      this.loading.set(false);
    });
  }

  isGranted(permission: string): boolean {
    return this.granted().has(permission);
  }

  toggle(permission: string, checked: boolean): void {
    const next = new Set(this.granted());
    if (checked) next.add(permission);
    else next.delete(permission);
    this.granted.set(next);
  }

  isGroupFullyGranted(group: PermissionGroup): boolean {
    return group.permissions.every((p) => this.isGranted(p));
  }

  isGroupPartiallyGranted(group: PermissionGroup): boolean {
    const grantedCount = group.permissions.filter((p) => this.isGranted(p)).length;
    return grantedCount > 0 && grantedCount < group.permissions.length;
  }

  toggleGroup(group: PermissionGroup, checked: boolean): void {
    const next = new Set(this.granted());
    for (const permission of group.permissions) {
      if (checked) next.add(permission);
    else next.delete(permission);
    }
    this.granted.set(next);
  }

  save(): void {
    this.rolesApi.updateRolePermissions(this.data.role.id, [...this.granted()]).subscribe(() => {
      this.dialogRef.close(true);
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
