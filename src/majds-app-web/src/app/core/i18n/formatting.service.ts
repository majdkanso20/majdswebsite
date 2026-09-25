import { Injectable, inject } from '@angular/core';
import { AppSettingsService } from '../services/app-settings.service';
import { LocalizationService } from './localization.service';

export type DateStyle = 'short' | 'medium' | 'long' | 'date' | 'time';

const HAS_ZONE = /(?:[zZ]|[+-]\d{2}:?\d{2})$/;

/**
 * Formats dates, numbers and money for the active language and the user's own time zone and currency (F-Localization FR-I18N-004).
 * The server stores every timestamp in UTC and sends it without a zone marker, so a bare ISO string is read as UTC before it is shown
 * in the user's time zone. Formatting follows the language (digits, separators, month names, order); an unusable time zone or currency
 * falls back to UTC and USD rather than failing (FR-I18N-005).
 */
@Injectable({ providedIn: 'root' })
export class FormattingService {
  private readonly l10n = inject(LocalizationService);
  private readonly settings = inject(AppSettingsService);

  /** The user's time zone (Appearance.Timezone: their own choice, else the application's). */
  timeZone(): string {
    const zone = this.settings.get('Appearance.Timezone', 'UTC') || 'UTC';
    try {
      new Intl.DateTimeFormat('en', { timeZone: zone });
      return zone;
    } catch {
      return 'UTC';
    }
  }

  currencyCode(): string {
    return this.settings.get('Appearance.Currency', 'USD') || 'USD';
  }

  /** Reads a timestamp from the server (a bare ISO string is UTC) or any Date or number. Null when it is not a date. */
  parse(value: string | number | Date | null | undefined): Date | null {
    if (value === null || value === undefined || value === '') return null;
    const date = typeof value === 'string' ? new Date(HAS_ZONE.test(value) ? value : value + 'Z') : new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
  }

  date(value: string | number | Date | null | undefined, style: DateStyle = 'medium'): string {
    const date = this.parse(value);
    if (!date) return '';

    const options: Intl.DateTimeFormatOptions = { timeZone: this.timeZone() };
    switch (style) {
      case 'short':
        Object.assign(options, { dateStyle: 'short', timeStyle: 'short' });
        break;
      case 'long':
        Object.assign(options, { dateStyle: 'long', timeStyle: 'short' });
        break;
      case 'date':
        Object.assign(options, { dateStyle: 'medium' });
        break;
      case 'time':
        Object.assign(options, { timeStyle: 'short' });
        break;
      default:
        Object.assign(options, { dateStyle: 'medium', timeStyle: 'medium' });
    }

    return new Intl.DateTimeFormat(this.l10n.language(), options).format(date);
  }

  number(value: number | string | null | undefined, options?: Intl.NumberFormatOptions): string {
    if (value === null || value === undefined || value === '') return '';
    const n = typeof value === 'number' ? value : Number(value);
    return Number.isFinite(n) ? new Intl.NumberFormat(this.l10n.language(), options).format(n) : String(value);
  }

  /** An amount of money in the user's currency, or in <paramref>currency</paramref> when the amount is in a specific one. */
  money(value: number | string | null | undefined, currency?: string): string {
    if (value === null || value === undefined || value === '') return '';
    const n = typeof value === 'number' ? value : Number(value);
    if (!Number.isFinite(n)) return String(value);

    try {
      return new Intl.NumberFormat(this.l10n.language(), { style: 'currency', currency: currency ?? this.currencyCode() }).format(n);
    } catch {
      return new Intl.NumberFormat(this.l10n.language(), { style: 'currency', currency: 'USD' }).format(n);
    }
  }
}
