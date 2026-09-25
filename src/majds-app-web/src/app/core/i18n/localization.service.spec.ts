import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LocalizationService } from './localization.service';

describe('LocalizationService', () => {
  let http: HttpTestingController;

  function create(stored?: string): LocalizationService {
    localStorage.clear();
    if (stored) localStorage.setItem('majds-app.language', stored);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(LocalizationService);
  }

  afterEach(() => {
    localStorage.clear();
    document.documentElement.dir = 'ltr';
    document.documentElement.lang = 'en';
  });

  it('starts in English with no translation file to load', () => {
    const service = create();

    expect(service.language()).toBe('en');
    expect(service.translate('Save')).toBe('Save');
    http.expectNone('/i18n/ar.json');
  });

  it('switches language at runtime, without a reload, and flips the direction for a right-to-left language (AC-I18N-1, AC-I18N-3)', async () => {
    const service = create();

    const switching = service.set('ar');
    http.expectOne('/i18n/ar.json').flush({ Save: 'حفظ' });
    await switching;

    expect(service.language()).toBe('ar');
    expect(service.translate('Save')).toBe('حفظ');
    expect(document.documentElement.dir).toBe('rtl');
    expect(document.documentElement.lang).toBe('ar');

    await service.set('en');
    expect(service.translate('Save')).toBe('Save');
    expect(document.documentElement.dir).toBe('ltr');
  });

  it('shows the English text for a key nobody translated, never a blank (AC-I18N-2)', async () => {
    const service = create();
    const switching = service.set('ar');
    http.expectOne('/i18n/ar.json').flush({ Save: 'حفظ' });
    await switching;

    expect(service.translate('A brand new sentence')).toBe('A brand new sentence');
  });

  it('falls back to English when the translation file cannot be loaded', async () => {
    const service = create();
    const switching = service.set('ar');
    http.expectOne('/i18n/ar.json').error(new ProgressEvent('error'));
    await switching;

    expect(service.language()).toBe('ar');
    expect(service.translate('Save')).toBe('Save');
  });

  it('fills the values into a parameterized text, in whichever order the language needs', async () => {
    const service = create();
    expect(service.translate('Delete {0}?', 'Ada')).toBe('Delete Ada?');
    expect(service.translate('Version {0} of {1}', '2', 'Tasks')).toBe('Version 2 of Tasks');

    const switching = service.set('ar');
    http.expectOne('/i18n/ar.json').flush({ 'Version {0} of {1}': 'الإصدار {1} هو {0}' });
    await switching;

    expect(service.translate('Version {0} of {1}', '2', 'Tasks')).toBe('الإصدار Tasks هو 2');
  });

  it('leaves a placeholder alone when no value was given for it', () => {
    const service = create();

    expect(service.translate('Hello {0} and {1}', 'Ada')).toBe('Hello Ada and {1}');
  });

  it('ignores a language it does not support', async () => {
    const service = create();

    await service.set('fr');

    expect(service.language()).toBe('en');
    http.expectNone('/i18n/fr.json');
  });

  it('remembers the users own choice and prefers it over the application default', async () => {
    const service = create();
    const chosen = service.set('ar');
    http.expectOne('/i18n/ar.json').flush({});
    await chosen;

    expect(localStorage.getItem('majds-app.language')).toBe('ar');

    await service.applyDefault('en'); // the admin default must not override a personal choice
    expect(service.language()).toBe('ar');
  });

  it('applies the application default only when the user has not chosen', async () => {
    const service = create();

    const applying = service.applyDefault('ar');
    http.expectOne('/i18n/ar.json').flush({});
    await applying;

    expect(service.language()).toBe('ar');
    expect(localStorage.getItem('majds-app.language')).toBeNull(); // the default is not stored as a personal choice
  });
});
