import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { environment } from '../../../../environments/environment';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Consumes the link from ForgotPassword — reads `email` and `code` off the query string, then
 *  posts the new password to the server, which validates the token via Identity's own reset flow. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  styleUrl: '../login/login.scss',
  templateUrl: './reset-password.html'
})
export class ResetPassword {
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly email = signal<string | null>(this.route.snapshot.queryParamMap.get('email'));
  readonly code = signal<string | null>(this.route.snapshot.queryParamMap.get('code'));

  readonly form = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(6)]]
  });

  readonly submitting = signal(false);
  readonly done = signal(false);
  readonly error = signal<string | null>(null);

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error.set('Enter a password of at least 6 characters.');
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      const { newPassword } = this.form.getRawValue();
      await firstValueFrom(
        this.http.post(`${environment.apiBaseUrl}/account/reset-password`, {
          email: this.email(),
          code: this.code(),
          newPassword
        })
      );
      this.done.set(true);
    } catch (err) {
      const body = (err as { error?: { message?: string; errors?: string[] } })?.error;
      this.error.set(body?.errors?.[0] ?? body?.message ?? 'Something went wrong.');
    } finally {
      this.submitting.set(false);
    }
  }
}
