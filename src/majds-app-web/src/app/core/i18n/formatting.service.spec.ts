import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AppSettingsService } from '../services/app-settings.service';
import { LocalDatePipe, LocalCurrencyPipe, LocalNumberPipe } from './format.pipes';
import { FormattingService } from './formatting.service';
import { LocalizationService } from './localization.service';

describe('FormattingService', () => {
  const language = signal('en');
  const values: Record<string, string> = {};

  function create(): FormattingService {
    language.set('en');
    for (const key of Object.keys(values)) delete values[key];
    TestBed.configureTestingModule({
      providers: [
        { provide: LocalizationService, useValue: { language } },
        { provide: AppSettingsService, useValue: { get: (name: string, fallback = '') => values[name] ?? fallback } }
      ]
    });
    return TestBed.inject(FormattingService);
  }

  it('reads a bare server timestamp as UTC and shows it in the users own time zone (FR-I18N-004)', () => {
    const formatting = create();
    const stamp = '2026-09-25T10:00:00'; // the server sends UTC with no zone marker

    values['Appearance.Timezone'] = 'UTC';
    expect(formatting.date(stamp, 'time')).toContain('10:00');

    values['Appearance.Timezone'] = 'Asia/Amman'; // UTC+3 in September 2026
    expect(formatting.date(stamp, 'time')).toContain('1:00');
  });

  it('respects a zone marker that is already there', () => {
    const formatting = create();
    values['Appearance.Timezone'] = 'UTC';

    expect(formatting.parse('2026-09-25T10:00:00Z')?.toISOString()).toBe('2026-09-25T10:00:00.000Z');
    expect(formatting.parse('2026-09-25T10:00:00+03:00')?.toISOString()).toBe('2026-09-25T07:00:00.000Z');
  });

  it('shows month names and digits of the active language', () => {
    const formatting = create();
    values['Appearance.Timezone'] = 'UTC';

    expect(formatting.date('2026-09-25T10:00:00', 'date')).toContain('Sep');

    // What "the language's rules" are is the platform's business (digits, separators and month names differ by language and ICU version):
    // the service must simply hand the active language to Intl.
    language.set('ar');
    expect(formatting.date('2026-09-25T10:00:00', 'long')).toMatch(/[؀-ۿ]/);   // Arabic month name
    expect(formatting.number(1234.5)).toBe(new Intl.NumberFormat('ar').format(1234.5));
  });

  it('falls back to UTC for a time zone it does not know and to nothing for a value that is not a date', () => {
    const formatting = create();
    values['Appearance.Timezone'] = 'Not/AZone';

    expect(formatting.timeZone()).toBe('UTC');
    expect(formatting.date('2026-09-25T10:00:00', 'time')).toContain('10:00');
    expect(formatting.date(null)).toBe('');
    expect(formatting.date('not a date')).toBe('');
    expect(formatting.date('')).toBe('');
  });

  it('formats numbers with the separators of the active language and passes text through untouched', () => {
    const formatting = create();

    expect(formatting.number(1234567.891, { maximumFractionDigits: 1 })).toBe('1,234,567.9');
    expect(formatting.number('42')).toBe('42');
    expect(formatting.number('n/a')).toBe('n/a');
    expect(formatting.number(null)).toBe('');
  });

  it('formats money in the users currency, or in one the amount is really in', () => {
    const formatting = create();

    expect(formatting.money(1234.5)).toBe('$1,234.50');

    values['Appearance.Currency'] = 'JOD';
    expect(formatting.money(1234.5)).toContain('JOD');
    expect(formatting.money(10, 'EUR')).toContain('€');
  });

  it('falls back to dollars for a currency code it cannot use instead of failing', () => {
    const formatting = create();
    values['Appearance.Currency'] = 'ZZ';

    expect(formatting.money(5)).toBe('$5.00');
  });

  it('is available in templates as pipes that follow the language', () => {
    create();
    values['Appearance.Timezone'] = 'UTC';

    TestBed.runInInjectionContext(() => {
      expect(new LocalNumberPipe().transform(1234.567, 1)).toBe('1,234.6');
      expect(new LocalCurrencyPipe().transform(2, 'USD')).toBe('$2.00');
      expect(new LocalDatePipe().transform('2026-09-25T10:00:00', 'time')).toContain('10:00');

      language.set('ar');
      expect(new LocalNumberPipe().transform(1234)).toBe(new Intl.NumberFormat('ar').format(1234));
      expect(new LocalDatePipe().transform('2026-09-25T10:00:00', 'long')).toMatch(/[؀-ۿ]/);
    });
  });
});
