import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { LocalizationService } from '../i18n/localization.service';
import { languageInterceptor } from './language.interceptor';

describe('languageInterceptor', () => {
  const language = signal('ar');
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    language.set('ar');
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([languageInterceptor])),
        provideHttpClientTesting(),
        { provide: LocalizationService, useValue: { language } }
      ]
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  it('tells the API which language the screen is in', () => {
    http.get(`${environment.apiBaseUrl}/users/list`).subscribe();

    expect(controller.expectOne(`${environment.apiBaseUrl}/users/list`).request.headers.get('Accept-Language')).toBe('ar');
  });

  it('follows a language switch on the very next request', () => {
    language.set('en');
    http.get(`${environment.apiBaseUrl}/users/list`).subscribe();

    expect(controller.expectOne(`${environment.apiBaseUrl}/users/list`).request.headers.get('Accept-Language')).toBe('en');
  });

  it('leaves static files and other origins alone', () => {
    http.get('/i18n/ar.json').subscribe();
    http.get('https://example.com/data').subscribe();

    expect(controller.expectOne('/i18n/ar.json').request.headers.has('Accept-Language')).toBe(false);
    expect(controller.expectOne('https://example.com/data').request.headers.has('Accept-Language')).toBe(false);
  });

  it('keeps a language a caller set on purpose', () => {
    http.get(`${environment.apiBaseUrl}/x`, { headers: { 'Accept-Language': 'en' } }).subscribe();

    expect(controller.expectOne(`${environment.apiBaseUrl}/x`).request.headers.get('Accept-Language')).toBe('en');
  });
});
