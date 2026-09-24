import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { SessionService } from '../../../core/services/session.service';
import { Login } from './login';

describe('Login', () => {
  const auth = { login: vi.fn(), externalProviders: vi.fn(), registrationOpen: vi.fn(), startExternalLogin: vi.fn() };
  const session = { bootstrapAsync: vi.fn() };

  async function createPage() {
    const fixture = TestBed.createComponent(Login);
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, page: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  }

  async function submit(page: Login, email = 'a@b.co', password = 'secret') {
    page.form.setValue({ email, password, code: '' });
    await page.submit();
  }

  beforeEach(() => {
    auth.login.mockReset();
    auth.externalProviders.mockReset().mockResolvedValue([]);
    auth.registrationOpen.mockReset().mockResolvedValue(false);
    session.bootstrapAsync.mockReset().mockResolvedValue(undefined);

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: SessionService, useValue: session }
      ]
    });
  });

  it('offers a way to recover a forgotten password', async () => {
    const { element } = await createPage();

    const link = element.querySelector('a[href="/forgot-password"]');
    expect(link).not.toBeNull();
  });

  it('will not submit an empty or invalid form', async () => {
    const { page } = await createPage();

    await submit(page, 'not-an-email', '');

    expect(auth.login).not.toHaveBeenCalled();
    expect(page.form.controls.email.touched).toBe(true);
  });

  it('signs in, loads the session, and goes to the dashboard', async () => {
    auth.login.mockReturnValue(of({ accessToken: 't' }));
    const { page } = await createPage();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    await submit(page);

    expect(auth.login).toHaveBeenCalledWith({ email: 'a@b.co', password: 'secret' });
    expect(session.bootstrapAsync).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('shows a generic message for wrong credentials (no hint about which part was wrong)', async () => {
    auth.login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));
    const { page } = await createPage();

    await submit(page);

    expect(page.errorMessage()).toBe('Incorrect email or password, or the account is not confirmed/active.');
  });

  it('asks for the second factor when the account requires one', async () => {
    auth.login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401, error: { detail: 'RequiresTwoFactor' } })));
    const { page } = await createPage();

    await submit(page);

    expect(page.requiresTwoFactor()).toBe(true);
    expect(page.errorMessage()).toBeNull();
  });

  it('tells the user to wait when they are being rate limited', async () => {
    auth.login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 429 })));
    const { page } = await createPage();

    await submit(page);

    expect(page.errorMessage()).toBe('Too many attempts. Please wait a minute and try again.');
  });

  it('sends a recovery code (with a dash) as such and an authenticator code as a code', async () => {
    auth.login.mockReturnValue(of({ accessToken: 't' }));
    const { page } = await createPage();
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    page.form.setValue({ email: 'a@b.co', password: 'secret', code: 'abcde-fghij' });
    await page.submit();
    page.form.setValue({ email: 'a@b.co', password: 'secret', code: '123456' });
    await page.submit();

    expect(auth.login).toHaveBeenNthCalledWith(1, expect.objectContaining({ twoFactorRecoveryCode: 'abcde-fghij' }));
    expect(auth.login).toHaveBeenNthCalledWith(2, expect.objectContaining({ twoFactorCode: '123456' }));
  });

  it('shows "Create account" only when the server says registration is open', async () => {
    auth.registrationOpen.mockResolvedValue(true);
    const { element } = await createPage();

    expect(element.querySelector('a[href="/register"]')).not.toBeNull();
  });
});
