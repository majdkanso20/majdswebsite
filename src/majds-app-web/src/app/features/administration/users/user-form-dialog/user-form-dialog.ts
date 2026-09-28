import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatRadioModule } from '@angular/material/radio';
import { UserDto } from '../user.models';

export interface UserFormDialogData {
  user: UserDto | null;
  roleOptions: string[];
}

export interface UserFormResult {
  email: string;
  password: string | null;
  sendSetPasswordEmail: boolean;
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
    MatSelectModule,
    MatRadioModule
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
    // Either a password set here now, or an emailed set-password link — the same choice FR-USER-002 names.
    passwordMode: ['set' as 'set' | 'email'],
    password: ['', this.isEdit ? [] : [Validators.required, Validators.minLength(6)]],
    fullName: [this.data.user?.fullName ?? ''],
    phoneNumber: [this.data.user?.phoneNumber ?? ''],
    roles: [this.data.user?.roles ?? ([] as string[])]
  });

  constructor() {
    // The password field is only required in the "set a password now" mode; switching modes must not leave a stale error showing.
    this.form.controls.passwordMode.valueChanges.subscribe((mode) => {
      if (this.isEdit) return;
      if (mode === 'email') {
        this.form.controls.password.clearValidators();
      } else {
        this.form.controls.password.setValidators([Validators.required, Validators.minLength(6)]);
      }
      this.form.controls.password.updateValueAndValidity();
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const sendSetPasswordEmail = !this.isEdit && value.passwordMode === 'email';
    this.dialogRef.close({
      email: value.email,
      password: sendSetPasswordEmail ? null : value.password || null,
      sendSetPasswordEmail,
      fullName: value.fullName || null,
      phoneNumber: value.phoneNumber || null,
      roles: value.roles
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
