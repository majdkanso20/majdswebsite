import { TestBed } from '@angular/core/testing';
import { SKINS, ThemeService } from './theme.service';

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    delete document.documentElement.dataset['skin'];
    document.body.className = '';
    TestBed.resetTestingModule();
  });

  it('starts on the system scheme and the default skin', () => {
    const theme = TestBed.inject(ThemeService);

    expect(theme.mode()).toBe('system');
    expect(theme.skin()).toBe('default');
    expect(document.body.classList).toContain('theme-system');
    expect(document.documentElement.dataset['skin']).toBeUndefined();
  });

  it('applies and persists an explicit light or dark choice (AC-SHELL-2)', () => {
    TestBed.inject(ThemeService).setMode('dark');

    expect(document.body.classList).toContain('theme-dark');
    expect(document.body.classList).not.toContain('theme-system');
    expect(localStorage.getItem('majds-app.theme')).toBe('dark');

    TestBed.resetTestingModule();
    expect(TestBed.inject(ThemeService).mode()).toBe('dark'); // survives a "reload"
  });

  it('switches skins at runtime through one attribute and remembers the choice (FR-UI-006)', () => {
    const theme = TestBed.inject(ThemeService);

    theme.setSkin('ocean');
    expect(document.documentElement.dataset['skin']).toBe('ocean');

    theme.setSkin('default');
    expect(document.documentElement.dataset['skin']).toBeUndefined();

    theme.setSkin('violet');
    TestBed.resetTestingModule();
    expect(TestBed.inject(ThemeService).skin()).toBe('violet');
    expect(document.documentElement.dataset['skin']).toBe('violet');
  });

  it('ignores an unknown stored skin', () => {
    localStorage.setItem('majds-app.skin', 'neon-nonsense');

    expect(TestBed.inject(ThemeService).skin()).toBe('default');
  });

  it('offers a default plus the named skins', () => {
    expect(SKINS.map((s) => s.id)).toEqual(['default', 'ocean', 'forest', 'sunset', 'violet']);
  });
});
