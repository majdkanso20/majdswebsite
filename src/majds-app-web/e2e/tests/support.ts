import { APIRequestContext, Page, expect } from '@playwright/test';
import { ADMIN, API } from '../playwright.config';

/** Signs in through the login form, the way a person does, and waits for the dashboard. */
export async function signInThroughTheForm(page: Page, email = ADMIN.email, password = ADMIN.password): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/dashboard/);
}

/** Puts a signed-in session in the browser without going through the form, for tests about something else. */
export async function signInQuickly(page: Page, request: APIRequestContext): Promise<string> {
  const token = await accessToken(request);
  // The chosen language is saved on the account, so put it back to English: one test's choice must not leak into the next.
  await request.post(`${API}/api/settings/update-mine`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { items: [{ name: 'General.DefaultLanguage', value: 'en' }] }
  });
  await page.addInitScript(([t, email]) => {
    localStorage.setItem('majds-app.accessToken', t);
    localStorage.setItem('majds-app.userEmail', email);
  }, [token, ADMIN.email] as const);
  return token;
}

export async function accessToken(request: APIRequestContext, email = ADMIN.email, password = ADMIN.password): Promise<string> {
  const response = await request.post(`${API}/api/identity/login`, { data: { email, password } });
  expect(response.ok(), 'the administrator can sign in through the API').toBeTruthy();
  return (await response.json()).accessToken as string;
}

/** Calls an API endpoint as the administrator and returns the ResponseDto `data`. */
export async function api<T>(request: APIRequestContext, token: string, method: 'GET' | 'POST', path: string, body?: unknown): Promise<T> {
  const response = await request.fetch(`${API}/api/${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}` },
    data: method === 'POST' ? (body ?? {}) : undefined
  });
  expect(response.ok(), `${method} ${path} → ${response.status()} ${await response.text()}`).toBeTruthy();
  return (await response.json()).data as T;
}
