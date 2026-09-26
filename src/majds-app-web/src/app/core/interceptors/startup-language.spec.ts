import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideRouter } from '@angular/router';
import { LocalizationService } from '../i18n/localization.service';
import { TranslatingSnackBar } from '../i18n/translating-material';
import { errorInterceptor } from './error.interceptor';

/**
 * Regression: with Arabic remembered, the language service fetches its translation file while it is being constructed. That request goes through
 * the interceptors, and the error interceptor needed the snack bar, which needs the language service: a circular dependency that left the app blank
 * on every reload. This wires the same providers as the app.
 */
describe('startup with a remembered language', () => {
  function configure(stored?: string): void {
    localStorage.clear();
    if (stored) localStorage.setItem('majds-app.language', stored);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useExisting: TranslatingSnackBar }
      ]
    });
  }

  afterEach(() => {
    localStorage.clear();
    document.documentElement.dir = 'ltr';
    document.documentElement.lang = 'en';
  });

  it('creates the language service without a circular dependency and loads the translations', async () => {
    configure('ar');

    const service = TestBed.inject(LocalizationService); // used to throw NG0200

    TestBed.inject(HttpTestingController).expectOne('/i18n/ar.json').flush({ Save: 'حفظ' });
    await vi.waitFor(() => expect(service.translate('Save')).toBe('حفظ'));
    expect(service.language()).toBe('ar');
  });

  it('still shows a message when a request fails', () => {
    configure();
    TestBed.inject(LocalizationService);
    const open = vi.spyOn(TestBed.inject(TranslatingSnackBar), 'open');

    TestBed.inject(HttpClient).get('/api/anything').subscribe({ error: () => undefined });
    TestBed.inject(HttpTestingController).expectOne('/api/anything').flush({}, { status: 500, statusText: 'Server Error' });

    expect(open).toHaveBeenCalled();
  });
});
