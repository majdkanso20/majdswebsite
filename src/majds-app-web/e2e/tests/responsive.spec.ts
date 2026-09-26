import { expect, test } from '@playwright/test';
import { signInQuickly } from './support';

/** U2 mobile-first: at the narrowest supported width (320px) no page may scroll sideways, in either text direction. */
const pages = [
  '/dashboard',
  '/administration/users',
  '/administration/roles',
  '/administration/settings',
  '/administration/plugins',
  '/administration/audit-log',
  '/administration/jobs',
  '/administration/features',
  '/files',
  '/exports',
  '/account'
];

for (const direction of ['English', 'Arabic'] as const) {
  test.describe(`320px wide, ${direction}`, () => {
    test.use({ viewport: { width: 320, height: 640 } });

    for (const path of pages) {
      test(`${path} does not scroll sideways`, async ({ page, request }) => {
        const token = await signInQuickly(page, request);
        if (direction === 'Arabic') {
          await page.addInitScript(() => localStorage.setItem('majds-app.language', 'ar'));
        }
        await page.goto(path);
        await page.locator('main').waitFor();
        await page.waitForLoadState('networkidle');

        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
        expect(overflow, `${path} is ${overflow}px wider than the screen`).toBeLessThanOrEqual(0);
        expect(token).toBeTruthy();
      });
    }
  });
}
