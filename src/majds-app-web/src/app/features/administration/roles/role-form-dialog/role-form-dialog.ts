import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { RoleDto } from '../role.models';

export interface RoleFormDialogData {
  role: RoleDto | null;
}

export interface RoleFormResult {
  name: string;
  displayName: string | null;
  isDefault: boolean;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-role-form-dialog',
  imports: [TranslatePipe, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatCheckboxModule],
  styleUrl: './role-form-dialog.scss',
  templateUrl: './role-form-dialog.html'
})
export class RoleFormDialog {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<RoleFormDialog, RoleFormResult>);
  readonly data = inject<RoleFormDialogData>(MAT_DIALOG_DATA);

  readonly isEdit = this.data.role !== null;

  readonly form = this.fb.nonNullable.group({
    name: [{ value: this.data.role?.name ?? '', disabled: this.isEdit }, [Validators.required, Validators.maxLength(64)]],
    displayName: [this.data.role?.displayName ?? ''],
    isDefault: [this.data.role?.isDefault ?? false]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.dialogRef.close({ name: value.name, displayName: value.displayName || null, isDefault: value.isDefault });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
