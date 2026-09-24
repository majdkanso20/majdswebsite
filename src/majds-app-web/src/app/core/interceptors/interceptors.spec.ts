import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '../services/auth.service';
import { authInterceptor } from './auth.interceptor';
import { errorInterceptor } from './error.interceptor';

describe('HTTP interceptors', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  const snackBar = { open: vi.fn() };
  const router = { navigate: vi.fn() };
  const auth = { accessToken: null as string | null, logout: vi.fn() };

  beforeEach(() => {
    snackBar.open.mockReset();
    router.navigate.mockReset();
    auth.logout.mockReset();
    auth.accessToken = null;

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: auth }
      ]
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('attaches the bearer token when signed in and nothing when not', () => {
    http.get('/anonymous').subscribe();
    expect(backend.expectOne('/anonymous').request.headers.has('Authorization')).toBe(false);

    auth.accessToken = 'abc123';
    http.get('/secured').subscribe();
    expect(backend.expectOne('/secured').request.headers.get('Authorization')).toBe('Bearer abc123');
  });

  it('signs the user out and returns to the login page on a 401 (FR-ERR-004)', () => {
    http.get('/secured').subscribe({ error: () => undefined });
    backend.expectOne('/secured').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(auth.logout).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('tells the user when they are refused, using the server message when there is one', () => {
    http.get('/x').subscribe({ error: () => undefined });
    backend.expectOne('/x').flush({ message: "Missing permission 'Users.View'." }, { status: 403, statusText: 'Forbidden' });

    expect(snackBar.open).toHaveBeenCalledWith("Missing permission 'Users.View'.", 'Dismiss', expect.anything());
  });

  it.each([
    [429, 'Too many requests. Please wait a moment and try again.'],
    [500, 'The server ran into a problem. Please try again.'],
    [503, 'The server ran into a problem. Please try again.'],
    [0, 'Cannot reach the server. Check your connection and try again.']
  ])('shows a friendly message for status %i', (status, message) => {
    http.get('/x').subscribe({ error: () => undefined });
    const request = backend.expectOne('/x');
    if (status === 0) request.error(new ProgressEvent('error'));
    else request.flush({}, { status, statusText: 'err' });

    expect(snackBar.open).toHaveBeenCalledWith(message, 'Dismiss', expect.anything());
  });

  it.each([400, 404, 409])('leaves a %i to the screen that made the call', (status) => {
    let caught: number | undefined;
    http.get('/x').subscribe({ error: (e) => (caught = e.status) });
    backend.expectOne('/x').flush({ message: 'Validation failed.' }, { status, statusText: 'err' });

    expect(snackBar.open).not.toHaveBeenCalled();
    expect(caught).toBe(status); // still delivered to the caller
  });
});
