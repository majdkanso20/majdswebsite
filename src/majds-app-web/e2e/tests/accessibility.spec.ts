import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { signInQuickly } from './support';

/**
 * Automated accessibility checks (WCAG 2.1 A/AA rules, including colour contrast) on the pages people use most, in the light and the dark theme.
 * An automated scan finds a share of the problems, not all of them, so passing does not replace a manual review; it stops the common regressions.
 */
const pages = [
  { name: 'dashboard', path: '/dashboard' },
  { name: 'users', path: '/administration/users' },
  { name: 'roles', path: '/administration/roles' },
  { name: 'settings', path: '/administration/settings' },
  { name: 'plugins', path: '/administration/plugins' },
  { name: 'audit log', path: '/administration/audit-log' },
  { name: 'background jobs', path: '/administration/jobs' },
  { name: 'files', path: '/files' },
  { name: 'my account', path: '/account' }
];

async function scan(page: import('@playwright/test').Page) {
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
  return results.violations.map((v) => `${v.id} (${v.impact}): ${v.help} — ${v.nodes.length} element(s), e.g. ${v.nodes[0]?.target.join(' ')}`);
}

for (const scheme of ['light', 'dark'] as const) {
  test.describe(`accessibility, ${scheme} theme`, () => {
    test.use({ colorScheme: scheme });

    for (const { name, path } of pages) {
      test(`${name} has no detectable WCAG A/AA violations`, async ({ page, request }) => {
        await signInQuickly(page, request);
        await page.goto(path);
        await page.locator('main').waitFor();
        await page.waitForLoadState('networkidle');

        expect(await scan(page)).toEqual([]);
      });
    }
  });
}

test('the login page has no detectable WCAG A/AA violations', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Email').waitFor();

  expect(await scan(page)).toEqual([]);
});
