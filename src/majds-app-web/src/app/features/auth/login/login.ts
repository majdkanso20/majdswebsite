import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services/auth.service';
import { SessionService } from '../../../core/services/session.service';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-login',
  imports: [TranslatePipe, RouterLink, 
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  styleUrl: './login.scss',
  templateUrl: './login.html'
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
    code: ['']
  });

  readonly providers = signal<string[]>([]);
  readonly registrationOpen = signal(false);
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  /** Set when the account has two-factor on and the first attempt came back "RequiresTwoFactor". */
  readonly requiresTwoFactor = signal(false);

  constructor() {
    void this.authService.externalProviders().then((p) => this.providers.set(p));
    void this.authService.registrationOpen().then((open) => this.registrationOpen.set(open));
  }

  signInWith(provider: string): void {
    this.authService.startExternalLogin(provider);
  }

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    try {
      const { email, password, code } = this.form.getRawValue();
      const trimmed = code.trim();
      // Recovery codes look like "abcde-fghij"; authenticator codes are 6 digits.
      await firstValueFrom(
        this.authService.login({
          email,
          password,
          ...(trimmed && (trimmed.includes('-') ? { twoFactorRecoveryCode: trimmed } : { twoFactorCode: trimmed }))
        })
      );
      await this.session.bootstrapAsync();
      await this.router.navigate(['/dashboard']);
    } catch (err) {
      const detail = (err as { error?: { detail?: string } })?.error?.detail;
      if ((err as { status?: number })?.status === 429) {
        this.errorMessage.set('Too many attempts. Please wait a minute and try again.');
      } else if (detail === 'RequiresTwoFactor') {
        this.requiresTwoFactor.set(true);
        this.errorMessage.set(null);
      } else {
        this.errorMessage.set(
          this.requiresTwoFactor()
            ? 'Incorrect code. Try again or use a recovery code.'
            : 'Incorrect email or password, or the account is not confirmed/active.'
        );
      }
    } finally {
      this.submitting.set(false);
    }
  }
}
