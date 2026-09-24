import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services/auth.service';
import { SessionService } from '../../../core/services/session.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

const ERRORS: Record<string, string> = {
  registration_disabled: 'New accounts are not being accepted right now. Ask an administrator to create one for you.',
  not_allowed: 'This account is inactive or locked.',
  no_email: 'The provider did not share an email address.',
  external_failed: 'Sign-in with the provider did not complete. Please try again.'
};

/** Landing page for the browser after the provider round-trip: the API redirects here with the result
 *  in the URL fragment (#access_token=… or #error=…), which never reaches any server. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-login-callback',
  imports: [RouterLink, TranslatePipe, MatButtonModule, MatCardModule, MatProgressSpinnerModule],
  styleUrl: '../login/login.scss',
  templateUrl: './login-callback.html'
})
export class LoginCallback {
  private readonly auth = inject(AuthService);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  readonly error = signal<string | null>(null);

  constructor() {
    void this.complete();
  }

  private async complete(): Promise<void> {
    const params = new URLSearchParams(window.location.hash.replace(/^#/, ''));
    const token = params.get('access_token');
    const email = params.get('email');

    if (!token || !email) {
      this.error.set(ERRORS[params.get('error') ?? ''] ?? ERRORS['external_failed']);
      return;
    }

    this.auth.completeExternalLogin(token, email);
    try {
      await this.session.bootstrapAsync();
      await this.router.navigate(['/dashboard'], { replaceUrl: true });
    } catch {
      this.auth.logout();
      this.error.set(ERRORS['external_failed']);
    }
  }
}
