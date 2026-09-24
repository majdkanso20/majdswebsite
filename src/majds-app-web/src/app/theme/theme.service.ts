import { Injectable, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark' | 'system';

/** Named skins (U1 FR-UI-006). Each is a token file in theme/skins/; 'default' is the palette in _tokens.scss. */
export const SKINS = [
  { id: 'default', label: 'Azure' },
  { id: 'ocean', label: 'Ocean' },
  { id: 'forest', label: 'Forest' },
  { id: 'sunset', label: 'Sunset' },
  { id: 'violet', label: 'Violet' }
] as const;

export type SkinId = (typeof SKINS)[number]['id'];

const MODE_KEY = 'majds-app.theme';
const SKIN_KEY = 'majds-app.skin';

/**
 * Applies the active color scheme (light/dark/system) and skin, and persists the user's explicit
 * choices (U3 FR-SHELL-004, AC-SHELL-2, U1 FR-UI-006). Scheme uses the CSS `color-scheme` property so
 * Angular Material's M3 tokens switch automatically; a skin swaps the palette by setting
 * <html data-skin> — no duplicate theme definitions and no component changes.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly mode = signal<ThemeMode>(this.readStoredMode());
  readonly skin = signal<SkinId>(this.readStoredSkin());

  constructor() {
    this.apply(this.mode());
    this.applySkin(this.skin());
  }

  setMode(mode: ThemeMode): void {
    this.mode.set(mode);
    this.apply(mode);
    this.store(MODE_KEY, mode);
  }

  setSkin(skin: SkinId): void {
    this.skin.set(skin);
    this.applySkin(skin);
    this.store(SKIN_KEY, skin);
  }

  private apply(mode: ThemeMode): void {
    document.body.classList.remove('theme-light', 'theme-dark', 'theme-system');
    document.body.classList.add(`theme-${mode}`);
  }

  private applySkin(skin: SkinId): void {
    if (skin === 'default') delete document.documentElement.dataset['skin'];
    else document.documentElement.dataset['skin'] = skin;
  }

  private store(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      // Private browsing / storage disabled: the choice just won't persist across reloads.
    }
  }

  private readStoredMode(): ThemeMode {
    try {
      const stored = localStorage.getItem(MODE_KEY);
      if (stored === 'light' || stored === 'dark' || stored === 'system') {
        return stored;
      }
    } catch {
      // Fall through to default.
    }
    return 'system';
  }

  private readStoredSkin(): SkinId {
    try {
      const stored = localStorage.getItem(SKIN_KEY);
      if (SKINS.some((s) => s.id === stored)) return stored as SkinId;
    } catch {
      // Fall through to default.
    }
    return 'default';
  }
}
