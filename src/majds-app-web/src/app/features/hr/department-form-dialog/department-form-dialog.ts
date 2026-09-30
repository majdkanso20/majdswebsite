import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { DepartmentDto } from '../hr.models';

export interface DepartmentFormDialogData {
  department: DepartmentDto | null;
}

export interface DepartmentFormResult {
  name: string;
  description: string | null;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-department-form-dialog',
  imports: [TranslatePipe, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  styleUrl: './department-form-dialog.scss',
  templateUrl: './department-form-dialog.html'
})
export class DepartmentFormDialog {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<DepartmentFormDialog, DepartmentFormResult>);
  readonly data = inject<DepartmentFormDialogData>(MAT_DIALOG_DATA);

  readonly isEdit = this.data.department !== null;

  readonly form = this.fb.nonNullable.group({
    name: [this.data.department?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    description: [this.data.department?.description ?? '', [Validators.maxLength(2000)]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.dialogRef.close({ name: value.name, description: value.description || null });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
