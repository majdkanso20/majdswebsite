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

/** Public sign-up (F-Account). The server enforces the "Allow users to self-register" setting; this
 *  page just shows what the server says. The new account must confirm its email before signing in. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  styleUrl: '../login/login.scss',
  templateUrl: './register.html'
})
export class Register {
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);

  readonly form = this.fb.nonNullable.group({
    fullName: [''],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  readonly submitting = signal(false);
  readonly done = signal(false);
  readonly error = signal<string | null>(null);

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error.set('Enter a valid email and a password of at least 6 characters.');
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      const { fullName, email, password } = this.form.getRawValue();
      await firstValueFrom(
        this.http.post(`${environment.apiBaseUrl}/account/register`, { email, password, fullName: fullName || null })
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
