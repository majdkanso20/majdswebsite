import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { UserDto } from '../user.models';

export interface UserFormDialogData {
  user: UserDto | null;
  roleOptions: string[];
}

export interface UserFormResult {
  email: string;
  password: string | null;
  fullName: string | null;
  phoneNumber: string | null;
  roles: string[];
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-user-form-dialog',
  imports: [TranslatePipe, 
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSelectModule
  ],
  styleUrl: './user-form-dialog.scss',
  templateUrl: './user-form-dialog.html'
})
export class UserFormDialog {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<UserFormDialog, UserFormResult>);
  readonly data = inject<UserFormDialogData>(MAT_DIALOG_DATA);

  readonly isEdit = this.data.user !== null;

  readonly form = this.fb.nonNullable.group({
    email: [
      { value: this.data.user?.email ?? '', disabled: this.isEdit },
      [Validators.required, Validators.email]
    ],
    password: ['', this.isEdit ? [] : [Validators.required, Validators.minLength(6)]],
    fullName: [this.data.user?.fullName ?? ''],
    phoneNumber: [this.data.user?.phoneNumber ?? ''],
    roles: [this.data.user?.roles ?? ([] as string[])]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.dialogRef.close({
      email: value.email,
      password: value.password || null,
      fullName: value.fullName || null,
      phoneNumber: value.phoneNumber || null,
      roles: value.roles
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
