import { Direction, Directionality } from '@angular/cdk/bidi';
import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseDto } from '../models/response-dto';

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
  private readonly directionality = inject(Directionality);
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
    if (code !== 'en') void this.addPluginTexts(code);

    const rtl = LANGUAGES.find((l) => l.code === code)?.rtl ?? false;
    document.documentElement.lang = code;
    document.documentElement.dir = rtl ? 'rtl' : 'ltr';
    this.tellMaterial(rtl ? 'rtl' : 'ltr');

    if (remember) {
      try {
        localStorage.setItem(STORAGE_KEY, code);
      } catch {
        // storage unavailable — choice just won't persist
      }
    }
  }

  /**
   * A plugin ships its own translations (its menu label, its messages). The server merges them into what it serves for a language, so they are
   * added here without ever replacing what the application's own file already says. It runs after the language has switched and never blocks it:
   * if the request fails the plugin's text simply stays in English.
   */
  private async addPluginTexts(code: string): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.http.get<ResponseDto<{ messages: Record<string, string> }>>(`${environment.apiBaseUrl}/localization/resources`, { params: { culture: code } })
      );
      if (this.language() === code && response.data?.messages) {
        this.dictionary.update((own) => ({ ...response.data!.messages, ...own }));
      }
    } catch {
      // the plugin texts stay in English
    }
  }

  /**
   * The text for <paramref>key</paramref> in the active language, with <c>{0}</c>, <c>{1}</c> ... filled from <paramref>args</paramref>.
   * Write a message once as a parameterized key ("Delete {0}?") instead of joining translated fragments, because word order differs
   * between languages. A missing translation falls back to the English key, so nothing is ever blank (AC-I18N-2).
   */
  translate(key: string, ...args: (string | number)[]): string {
    const text = this.dictionary()[key] ?? key;
    return args.length === 0 ? text : text.replace(/\{(\d+)\}/g, (match, index: string) => String(args[Number(index)] ?? match));
  }

  /**
   * Angular Material and the CDK read the page direction once, when the service is created, and do not watch the <html dir> attribute.
   * After a runtime language switch they would still think the page is left to right: the sidenav would move to the right while the content
   * kept its left margin and slid underneath it. Updating the shared Directionality (and announcing the change) makes them re-lay-out.
   */
  private tellMaterial(direction: Direction): void {
    if (this.directionality.value === direction) return;
    this.directionality.valueSignal.set(direction);
    this.directionality.change.emit(direction);
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
