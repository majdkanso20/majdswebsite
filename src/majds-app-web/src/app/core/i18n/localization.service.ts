import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface Language {
  code: string;
  label: string;
  rtl: boolean;
}

export const LANGUAGES: readonly Language[] = [
  { code: 'en', label: 'English', rtl: false },
  { code: 'ar', label: 'العربية', rtl: true }
];

const STORAGE_KEY = 'majds-app.language';

/**
 * Runtime UI localization (F-Localization). English text is the translation key, so a missing
 * entry (or missing file) simply renders the English source — nothing breaks for untranslated strings.
 * A user's own choice (localStorage) beats the admin-configured General.DefaultLanguage setting.
 */
@Injectable({ providedIn: 'root' })
export class LocalizationService {
  private readonly http = inject(HttpClient);
  private readonly dictionary = signal<Readonly<Record<string, string>>>({});

  readonly language = signal('en');

  constructor() {
    void this.set(this.stored() ?? 'en', false);
  }

  /** Applies the admin default only when the user hasn't picked a language themselves. */
  async applyDefault(code: string): Promise<void> {
    if (!this.stored() && this.isSupported(code) && code !== this.language()) {
      await this.set(code, false);
    }
  }

  async set(code: string, remember = true): Promise<void> {
    if (!this.isSupported(code)) return;

    let entries: Record<string, string> = {};
    if (code !== 'en') {
      try {
        entries = await firstValueFrom(this.http.get<Record<string, string>>(`/i18n/${code}.json`));
      } catch {
        entries = {};
      }
    }

    this.dictionary.set(entries);
    this.language.set(code);

    const rtl = LANGUAGES.find((l) => l.code === code)?.rtl ?? false;
    document.documentElement.lang = code;
    document.documentElement.dir = rtl ? 'rtl' : 'ltr';

    if (remember) {
      try {
        localStorage.setItem(STORAGE_KEY, code);
      } catch {
        // storage unavailable — choice just won't persist
      }
    }
  }

  translate(key: string): string {
    return this.dictionary()[key] ?? key;
  }

  private isSupported(code: string): boolean {
    return LANGUAGES.some((l) => l.code === code);
  }

  private stored(): string | null {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  }
}
