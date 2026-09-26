import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { LocalizationService } from '../../../../core/i18n/localization.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { RoleDto } from '../role.models';
import { RolesApiService } from '../roles-api.service';

export interface RoleReassignDialogData {
  role: RoleDto;
}

/** Asks which role takes over a role's users before it is deleted (F-Roles FR-ROLE-004). Closes with the chosen role's id. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-role-reassign-dialog',
  imports: [TranslatePipe, FormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatSelectModule],
  templateUrl: './role-reassign-dialog.html'
})
export class RoleReassignDialog {
  private readonly rolesApi = inject(RolesApiService);
  private readonly dialogRef = inject(MatDialogRef<RoleReassignDialog, string>);
  readonly l10n = inject(LocalizationService);
  readonly data = inject<RoleReassignDialogData>(MAT_DIALOG_DATA);

  readonly candidates = signal<RoleDto[]>([]);
  target = '';

  constructor() {
    this.rolesApi.list({ page: 1, pageSize: 100 }).subscribe((result) => {
      this.candidates.set(result.items.filter((r) => r.id !== this.data.role.id));
    });
  }

  confirm(): void {
    this.dialogRef.close(this.target);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
