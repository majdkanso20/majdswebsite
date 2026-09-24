import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { environment } from '../../../../environments/environment';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Self-service "forgot password" for a local account (F-Account). Always shows the same success
 *  message regardless of whether the email exists, matching the server's anti-enumeration behavior. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  styleUrl: '../login/login.scss',
  templateUrl: './forgot-password.html'
})
export class ForgotPassword {
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]]
  });

  readonly submitting = signal(false);
  readonly done = signal(false);

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    try {
      const { email } = this.form.getRawValue();
      await firstValueFrom(this.http.post(`${environment.apiBaseUrl}/account/forgot-password`, { email }));
    } finally {
      this.submitting.set(false);
      // Always show success, even on error — never reveal whether the address has an account.
      this.done.set(true);
    }
  }
}
