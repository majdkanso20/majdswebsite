import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import QRCode from 'qrcode';
import { AccountService } from '../../../core/services/account.service';
import { AppSettingsService } from '../../../core/services/app-settings.service';
import { TwoFactorService, TwoFactorState } from '../../../core/services/two-factor.service';
import { LocalizationService } from '../../../core/i18n/localization.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Two-factor authentication setup and management (F-Account). */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-two-factor-card',
  imports: [ReactiveFormsModule, TranslatePipe, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  styleUrl: './two-factor-card.scss',
  templateUrl: './two-factor-card.html'
})
export class TwoFactorCard {
  private readonly twoFactor = inject(TwoFactorService);
  private readonly account = inject(AccountService);
  private readonly appSettings = inject(AppSettingsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly l10n = inject(LocalizationService);

  readonly state = signal<TwoFactorState | null>(null);
  readonly busy = signal(false);
  /** True while the user is between "Set up" and a verified code. */
  readonly settingUp = signal(false);
  readonly qrDataUrl = signal<string | null>(null);
  readonly recoveryCodes = signal<string[]>([]);

  readonly code = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(6)] });

  /** Authenticator key in 4-character groups, easier to type by hand. */
  readonly displayKey = computed(() => (this.state()?.sharedKey ?? '').replace(/(.{4})/g, '$1 ').trim());

  constructor() {
    void this.refresh();
  }

  async startSetup(): Promise<void> {
    await this.run(async () => {
      const state = await this.twoFactor.status();
      this.state.set(state);

      const issuer = this.appSettings.get('General.ApplicationName', "Majd's App");
      const email = this.account.profile()?.email ?? '';
      const uri = `otpauth://totp/${encodeURIComponent(issuer)}:${encodeURIComponent(email)}?secret=${state.sharedKey}&issuer=${encodeURIComponent(issuer)}&digits=6`;
      this.qrDataUrl.set(await QRCode.toDataURL(uri, { margin: 1, width: 200 }));

      this.code.reset('');
      this.settingUp.set(true);
    });
  }

  cancelSetup(): void {
    this.settingUp.set(false);
    this.qrDataUrl.set(null);
  }

  async confirm(): Promise<void> {
    if (this.code.invalid) {
      this.code.markAsTouched();
      return;
    }

    await this.run(async () => {
      let state = await this.twoFactor.enable(this.code.value.replace(/\s/g, ''));
      // Identity only issues codes when none exist yet; a user who switched 2FA off and on again would
      // otherwise get no codes to save, so always issue a fresh set here.
      if (!state.recoveryCodes?.length) state = await this.twoFactor.newRecoveryCodes();
      this.state.set(state);
      this.recoveryCodes.set(state.recoveryCodes ?? []);
      this.settingUp.set(false);
      this.qrDataUrl.set(null);
      await this.account.loadAsync();
      this.snackBar.open('Two-factor authentication is on.', 'Dismiss', { duration: 3000 });
    }, 'That code is not valid. Check the time on your phone and try again.');
  }

  async newRecoveryCodes(): Promise<void> {
    await this.run(async () => {
      const state = await this.twoFactor.newRecoveryCodes();
      this.state.set(state);
      this.recoveryCodes.set(state.recoveryCodes ?? []);
    });
  }

  async disable(): Promise<void> {
    if (!confirm(this.l10n.translate('Turn off two-factor authentication? Your account will be protected by your password only.'))) return;

    await this.run(async () => {
      this.state.set(await this.twoFactor.disable());
      this.recoveryCodes.set([]);
      await this.account.loadAsync();
      this.snackBar.open('Two-factor authentication is off.', 'Dismiss', { duration: 3000 });
    });
  }

  private async refresh(): Promise<void> {
    await this.run(async () => this.state.set(await this.twoFactor.status()));
  }

  private async run(action: () => Promise<void>, errorMessage = 'Something went wrong.'): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch {
      this.snackBar.open(errorMessage, 'Dismiss', { duration: 5000 });
    } finally {
      this.busy.set(false);
    }
  }
}
