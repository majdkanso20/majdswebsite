import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TwoFactorCard } from '../two-factor-card/two-factor-card';
import { MyPreferencesCard } from '../my-preferences-card/my-preferences-card';
import { NotificationPreferencesCard } from '../notification-preferences-card/notification-preferences-card';
import { AccountService } from '../../../core/services/account.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-account-page',
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    TwoFactorCard,
    MyPreferencesCard,
    NotificationPreferencesCard,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule
  ],
  styleUrl: './account-page.scss',
  templateUrl: './account-page.html'
})
export class AccountPage {
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  readonly account = inject(AccountService);

  readonly savingProfile = signal(false);
  readonly savingPassword = signal(false);
  readonly uploadingPicture = signal(false);

  readonly profileForm = this.fb.nonNullable.group({
    fullName: [''],
    phoneNumber: ['']
  });

  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(6)]]
  });

  constructor() {
    void this.account.loadAsync();
    effect(() => {
      const profile = this.account.profile();
      if (profile) {
        this.profileForm.reset({ fullName: profile.fullName ?? '', phoneNumber: profile.phoneNumber ?? '' });
      }
    });
  }

  async saveProfile(): Promise<void> {
    const value = this.profileForm.getRawValue();
    this.savingProfile.set(true);
    try {
      await this.account.updateProfileAsync(value.fullName || null, value.phoneNumber || null);
      this.snackBar.open('Profile updated.', 'Dismiss', { duration: 3000 });
    } catch (err) {
      this.showError(err);
    } finally {
      this.savingProfile.set(false);
    }
  }

  async changePassword(): Promise<void> {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }
    const value = this.passwordForm.getRawValue();
    this.savingPassword.set(true);
    try {
      await this.account.changePasswordAsync(value.currentPassword, value.newPassword);
      this.passwordForm.reset();
      this.snackBar.open('Password changed.', 'Dismiss', { duration: 3000 });
    } catch (err) {
      this.showError(err);
    } finally {
      this.savingPassword.set(false);
    }
  }

  async onPictureSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.uploadingPicture.set(true);
    try {
      await this.account.setPictureAsync(file);
      this.snackBar.open('Picture updated.', 'Dismiss', { duration: 3000 });
    } catch (err) {
      this.showError(err);
    } finally {
      this.uploadingPicture.set(false);
    }
  }

  private showError(err: unknown): void {
    const body = (err as { error?: { message?: string; errors?: string[] } })?.error;
    this.snackBar.open(body?.errors?.[0] ?? body?.message ?? 'Something went wrong.', 'Dismiss', { duration: 5000 });
  }
}
