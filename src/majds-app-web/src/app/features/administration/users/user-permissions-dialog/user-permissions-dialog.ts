import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { RolesApiService } from '../../roles/roles-api.service';
import { PermissionGroup } from '../../roles/role.models';
import { UserDto } from '../user.models';
import { UsersApiService, UserPermissionsDto } from '../users-api.service';

export interface UserPermissionsDialogData {
  user: UserDto;
}

/** How a permission is decided for this user: by their roles ('inherit'), granted to them directly, or denied to them directly (FR-AUTHZ-003). */
export type PermissionChoice = 'inherit' | 'grant' | 'deny';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-user-permissions-dialog',
  imports: [TranslatePipe, MatDialogModule, MatButtonToggleModule, MatButtonModule, MatProgressSpinnerModule],
  styleUrl: './user-permissions-dialog.scss',
  templateUrl: './user-permissions-dialog.html'
})
export class UserPermissionsDialog {
  private readonly usersApi = inject(UsersApiService);
  private readonly rolesApi = inject(RolesApiService);
  private readonly dialogRef = inject(MatDialogRef<UserPermissionsDialog>);
  readonly data = inject<UserPermissionsDialogData>(MAT_DIALOG_DATA);

  readonly loading = signal(true);
  readonly groups = signal<PermissionGroup[]>([]);
  readonly explained = signal<UserPermissionsDto | null>(null);
  readonly choices = signal<Readonly<Record<string, PermissionChoice>>>({});

  constructor() {
    forkJoin({ tree: this.rolesApi.getPermissionTree(), explained: this.usersApi.getPermissions(this.data.user.id) }).subscribe(
      ({ tree, explained }) => {
        const choices: Record<string, PermissionChoice> = {};
        for (const name of explained.granted) choices[name] = 'grant';
        for (const name of explained.denied) choices[name] = 'deny';
        this.groups.set(tree);
        this.explained.set(explained);
        this.choices.set(choices);
        this.loading.set(false);
      }
    );
  }

  choiceOf(permission: string): PermissionChoice {
    return this.choices()[permission] ?? 'inherit';
  }

  /** Whether this user's roles give the permission, shown next to "By role" so the effect of Grant and Deny is clear. */
  givenByRoles(permission: string): boolean {
    const explained = this.explained();
    return !!explained && (explained.isSuperAdmin || explained.fromRoles.includes(permission));
  }

  choose(permission: string, choice: PermissionChoice): void {
    this.choices.update((current) => ({ ...current, [permission]: choice }));
  }

  save(): void {
    const granted: string[] = [];
    const denied: string[] = [];
    for (const [name, choice] of Object.entries(this.choices())) {
      if (choice === 'grant') granted.push(name);
      if (choice === 'deny') denied.push(name);
    }
    this.usersApi.updatePermissions(this.data.user.id, granted, denied).subscribe(() => this.dialogRef.close(true));
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
