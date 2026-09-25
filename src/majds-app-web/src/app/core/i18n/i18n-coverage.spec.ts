import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

/**
 * F-Localization FR-I18N-001 / AC-I18N-2: every piece of user-facing text in a template is a translation key, and every key has an
 * Arabic entry. English is the key, so a missing entry would show English in the Arabic UI; this test fails instead, listing what to add.
 * Texts that are built at runtime (template literals with values) cannot be found here and are translated where they are shown.
 */
describe('translation coverage', () => {
  const appDir = join(process.cwd(), 'src', 'app');
  const arabic = JSON.parse(readFileSync(join(process.cwd(), 'public', 'i18n', 'ar.json'), 'utf-8')) as Record<string, string>;

  function files(dir: string, extension: string): string[] {
    return readdirSync(dir).flatMap((name) => {
      const path = join(dir, name);
      if (statSync(path).isDirectory()) return files(path, extension);
      return path.endsWith(extension) && !path.endsWith('.spec.ts') ? [path] : [];
    });
  }

  const unescape = (text: string) => text.replace(/\\'/g, "'").replace(/\\"/g, '"');

  /** Quoted strings inside an expression, for example `cond ? 'A' : 'B'`. */
  function quoted(expression: string): string[] {
    return [...expression.matchAll(/'((?:[^'\\]|\\.)+)'|"((?:[^"\\]|\\.)+)"/g)].map((m) => unescape(m[1] ?? m[2]));
  }

  function keysIn(path: string): string[] {
    const source = readFileSync(path, 'utf-8');
    const keys: string[] = [];

    if (path.endsWith('.html')) {
      // {{ 'Text' | translate }} and {{ (cond ? 'A' : 'B') | translate }}
      for (const m of source.matchAll(/\{\{([^}]*?)\|\s*translate\s*\}\}/g)) keys.push(...quoted(m[1]));
      // [attr]="'Text' | translate"
      for (const m of source.matchAll(/="([^"]*?)\|\s*translate\s*"/g)) keys.push(...quoted(m[1]));
      // Plain attributes that shared components translate themselves.
      for (const m of source.matchAll(/\s(?:message|emptyMessage|label|placeholder)="([^"{}]+)"/g)) keys.push(m[1]);
    } else {
      // Messages shown through the (translating) snack bar and other TS-side literals passed to it.
      for (const m of source.matchAll(/snackBar\.open\(\s*'((?:[^'\\]|\\.)+)'/g)) keys.push(unescape(m[1]));
      for (const m of source.matchAll(/(?:header|label|title|emptyMessage|message):\s*'((?:[^'\\]|\\.)+)'/g)) keys.push(unescape(m[1]));
      // l10n.translate('Key {0}', value) and l10n.translate(cond ? 'A {0}' : 'B {0}', value)
      for (const m of source.matchAll(/l10n\.translate\(([^)]*)\)/g)) keys.push(...quoted(m[1]));
    }

    return keys.map((k) => k.trim()).filter((k) => k.length > 0);
  }

  it('has an Arabic entry for every text the interface shows', () => {
    const missing = new Map<string, string>();
    for (const file of [...files(appDir, '.html'), ...files(appDir, '.ts')]) {
      for (const key of keysIn(file)) {
        // Numbers, symbols and bare identifiers are not text a person reads.
        if (!/[A-Za-z]{2}/.test(key) || key in arabic) continue;
        if (/^[\w./:#-]+$/.test(key) && !/[A-Z][a-z]|\s/.test(key)) continue;
        if (!missing.has(key)) missing.set(key, relative(appDir, file));
      }
    }

    const list = [...missing].map(([key, file]) => `  "${key}"  (${file})`).join('\n');
    expect(missing.size, `Add these to public/i18n/ar.json:\n${list}`).toBe(0);
  });

  it('never builds a dialog or snackbar message by joining pieces, because word order differs between languages', () => {
    const offenders: string[] = [];
    for (const file of files(appDir, '.ts')) {
      const source = readFileSync(file, 'utf-8');
      // confirm(`...${x}...`) and snackBar.open(`...${x}...`): use a parameterized key such as translate('Delete {0}?', x) instead.
      for (const m of source.matchAll(/(?:confirm|snackBar\.open)\(\s*`[^`]*\$\{/g)) offenders.push(`${relative(appDir, file)}: ${m[0].slice(0, 60)}`);
    }

    expect(offenders, 'Use l10n.translate("Text {0}", value) instead:\n' + offenders.join('\n')).toEqual([]);
  });

  it('has no Arabic entry that is empty', () => {
    const empty = Object.entries(arabic).filter(([, value]) => value.trim().length === 0).map(([key]) => key);

    expect(empty).toEqual([]);
  });
});
