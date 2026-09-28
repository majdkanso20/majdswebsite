import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { ADMIN } from '../playwright.config';
import { accessToken, api, signInQuickly, signInThroughTheForm } from './support';

test.describe('signing in', () => {
  test('a wrong password says so and stays on the login page', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Email').fill(ADMIN.email);
    await page.getByLabel('Password').fill('not-the-password');
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.locator('.login-form__error')).toBeVisible();
    await expect(page).toHaveURL(/\/login/);
  });

  test('the administrator signs in and sees the dashboard and the administration menu', async ({ page }) => {
    await signInThroughTheForm(page);

    const nav = page.getByRole('navigation', { name: 'Main navigation' });
    await expect(nav.getByRole('link', { name: 'Dashboard' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Users' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Roles' })).toBeVisible();
  });

  test('a signed-out visitor sent to a protected page is taken to the login page', async ({ page }) => {
    await page.goto('/administration/users');

    await expect(page).toHaveURL(/\/login/);
  });
});

test.describe('language and layout', () => {
  test('switching to Arabic flips the page to right to left, puts the menu on the right, and is remembered after a reload', async ({ page, request }) => {
    await signInQuickly(page, request);
    await page.goto('/dashboard');

    await page.getByRole('button', { name: 'Language' }).click();
    await page.getByRole('menuitem', { name: 'العربية' }).click();

    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
    const nav = page.getByRole('navigation').first();
    await expect(nav).toContainText('لوحة التحكم');

    // The sidebar sits on the right in a right-to-left page, and the content does not run underneath it. The layout settles a moment after the
    // language switches (Material re-reads the direction), so measure until it has rather than the instant the attribute changes.
    await expect
      .poll(async () => {
        const sidebar = await page.locator('mat-sidenav').boundingBox();
        const content = await page.locator('main').boundingBox();
        return sidebar!.x > 640 && content!.x + content!.width <= sidebar!.x + 2;
      })
      .toBe(true);

    await page.reload();   // used to leave a blank page when a language other than English was remembered
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.getByRole('navigation').first()).toContainText('لوحة التحكم');

    await page.getByRole('button', { name: 'Language' }).click();
    await page.getByRole('menuitem', { name: 'English' }).click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
  });

  test('the header does not overflow on a phone-sized screen', async ({ page, request }) => {
    await signInQuickly(page, request);
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/dashboard');

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(0);
  });
});

test.describe('plugins', () => {
  test('a plugin ships its own screen, which the shell mounts in a shadow root with the plugin\'s data', async ({ page, request }) => {
    const token = await signInQuickly(page, request);
    const plugins = await api<{ id: string; isEnabled: boolean }[]>(request, token, 'GET', 'plugins/list');
    test.skip(!plugins.some((p) => p.id === 'MajdsApp.Plugins.Tasks' && p.isEnabled), 'the sample Tasks plugin is not built into the plugins folder');
    await api(request, token, 'POST', 'tasks/create', { title: 'Written by the browser test', description: 'd', status: 'Todo', dueDate: null });

    await page.goto('/tasks-overview');

    const element = page.locator('.plugin-element-page__host').locator('majds-tasks-overview');
    await expect(element).toBeAttached();
    await expect(element.getByText('Tasks overview')).toBeVisible();
    await expect(element.getByText('Written by the browser test')).toBeVisible();
    // The plugin's own stylesheet was loaded inside the shadow root, not into the application.
    expect(await page.locator('.plugin-element-page__host').evaluate((host) => host.shadowRoot?.querySelector('link[rel=stylesheet]') !== null)).toBe(true);
    expect(await page.locator('head link[href*="/plugins/"]').count()).toBe(0);
  });
});

test.describe('roles', () => {
  test('deleting a role that has users asks which role takes them over, and they keep access', async ({ page, request }) => {
    const token = await signInQuickly(page, request);
    const suffix = Date.now().toString(36);
    const doomed = `E2eDoomed${suffix}`;
    const heir = `E2eHeir${suffix}`;
    await api(request, token, 'POST', 'roles/create', { name: doomed, displayName: doomed, isDefault: false });
    await api(request, token, 'POST', 'roles/create', { name: heir, displayName: heir, isDefault: false });
    const member = `e2e.member.${suffix}@example.com`;
    await api(request, token, 'POST', 'users/create', { email: member, password: 'E2e-Member1!', roles: ['User', doomed] });

    await page.goto('/administration/roles');
    const row = page.getByRole('row', { name: new RegExp(doomed) });
    await expect(row).toBeVisible();
    await row.getByRole('button', { name: 'Delete' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('1 users have this role');
    await dialog.getByRole('combobox').click({ force: true }); // the floating label sits over the field; a person clicking there opens it too
    await page.getByRole('option', { name: heir }).click();
    // The page behind an open dialog is hidden from the accessibility tree, so "no such row" is true at once: wait for the deletion itself.
    const deleted = page.waitForResponse((r) => r.url().includes('/roles/delete'));
    await dialog.getByRole('button', { name: 'Move users and delete' }).click();
    expect((await deleted).ok()).toBe(true);

    await expect(dialog).toHaveCount(0);
    await expect(page.getByRole('row', { name: new RegExp(doomed) })).toHaveCount(0);
    const users = await api<{ items: { email: string; roles: string[] }[] }>(request, await accessToken(request), 'GET', `users/list?page=1&pageSize=5&filter=${member}`);
    expect(users.items[0].roles).toContain(heir);
    expect(users.items[0].roles).not.toContain(doomed);
  });
});

test.describe('users', () => {
  test('the status selector shows only active or only deactivated accounts', async ({ page, request }) => {
    const token = await signInQuickly(page, request);
    const suffix = Date.now().toString(36);
    const sleeper = `e2e.inactive.${suffix}@example.com`;
    const id = await api<string>(request, token, 'POST', 'users/create', { email: sleeper, password: 'E2e-Sleeper1!', roles: ['User'] });
    await api(request, token, 'POST', 'users/set-activation', { userId: id, isActive: false });

    await page.goto('/administration/users');
    await page.getByRole('radio', { name: 'Inactive' }).click();
    await expect(page.getByRole('row', { name: new RegExp(sleeper) })).toBeVisible();
    await expect(page.getByRole('row', { name: new RegExp(ADMIN.email) })).toHaveCount(0);

    await page.getByRole('radio', { name: 'Active', exact: true }).click();
    await expect(page.getByRole('row', { name: new RegExp(ADMIN.email) })).toBeVisible();
    await expect(page.getByRole('row', { name: new RegExp(sleeper) })).toHaveCount(0);

    await page.getByRole('radio', { name: 'All' }).click();
    await expect(page.getByRole('row', { name: new RegExp(sleeper) })).toBeVisible();
  });
});

test.describe('notification delivery mode', () => {
  const setMode = (request: import('@playwright/test').APIRequestContext, token: string, mode: string) =>
    api(request, token, 'POST', 'settings/update', { items: [{ name: 'Notifications.DeliveryMode', value: mode }] });

  test('a banner says when notifications are not going to their real recipients, follows the setting live, and is readable', async ({ page, request }) => {
    const token = await signInQuickly(page, request);
    try {
      await setMode(request, token, 'Dev');
      await page.goto('/dashboard');
      const banner = page.getByRole('status').filter({ hasText: 'notifications are not sent' });
      await expect(banner).toBeVisible();
      await expect(banner).toContainText('Email');
      expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze()).violations.map((v) => v.id)).toEqual([]);

      await setMode(request, token, 'Test');
      await page.reload();
      await expect(page.getByRole('status').filter({ hasText: 'go only to the test recipient' })).toBeVisible();

      // Changing it on the settings page updates the banner without a reload.
      await page.goto('/administration/settings');
      await page.getByLabel('Delivery mode', { exact: true }).fill('Prod');
      await page.getByRole('button', { name: 'Save' }).click();
      await expect(page.getByRole('status').filter({ hasText: 'notifications' })).toHaveCount(0);
    } finally {
      await setMode(request, token, 'Prod');
    }
  });
});
