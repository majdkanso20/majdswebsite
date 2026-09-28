import { defineConfig } from '@playwright/test';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

// Servers start from the Angular project folder, whatever folder the tests were launched from.
const appFolder = resolve(__dirname, '..');

/**
 * Browser end-to-end tests: a real browser drives the real Angular app against the real API, on a throwaway database.
 *
 * `npm run e2e` starts both servers itself, on ports of its own (API 5299, app 4299) so it never touches a development database or
 * a running dev server. The API creates its first administrator from configuration (Bootstrap:*), so nothing has to be set up by hand.
 * It drives the installed Google Chrome (no browser download); set E2E_BROWSER_CHANNEL=msedge to use Edge instead.
 */
export const API = 'http://localhost:5299';
export const APP = 'http://localhost:4299';
export const ADMIN = { email: 'e2e.admin@example.com', password: 'E2e-Admin1!' };

const database = join(tmpdir(), `majds-e2e-${Date.now()}.db`);

export default defineConfig({
  testDir: './tests',
  fullyParallel: false, // the tests share one database and one administrator
  workers: 1,
  retries: 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [['list']],
  outputDir: '../test-results',
  use: {
    baseURL: APP,
    channel: process.env['E2E_BROWSER_CHANNEL'] ?? 'chrome',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    viewport: { width: 1280, height: 800 }
  },
  webServer: [
    {
      // Built into its own folder so a development API that is already running (and locking its own build output) never gets in the way.
      command: `dotnet run --project ../MajdsApp.Api --no-launch-profile --urls http://localhost:5299 --property:UseArtifactsOutput=true --property:ArtifactsPath=${join(appFolder, '..', '..', '.artifacts', 'e2e')}`,
      cwd: appFolder,
      stdout: 'pipe',
      stderr: 'pipe',
      url: `${API}/health/live`,
      // Locally, servers left running by an earlier run are reused (they are on ports of their own); a CI run always starts fresh.
      reuseExistingServer: !process.env['CI'],
      timeout: 180_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ConnectionStrings__DefaultConnection: `DataSource=${database}`,
        Database__MigrateOnStart: 'true',
        Bootstrap__AdminEmail: ADMIN.email,
        Bootstrap__AdminPassword: ADMIN.password,
        Spa__AllowedOrigins__0: APP,
        Jobs__Scheduler__Enabled: 'false',
        Logging__File__Enabled: 'false',
        RateLimiting__AuthPermitLimit: '1000',
        RateLimiting__ExpensivePermitLimit: '1000',
        RateLimiting__GlobalPermitLimit: '10000'
      }
    },
    {
      command: 'npx ng serve --configuration e2e --port 4299',
      cwd: appFolder,
      url: APP,
      // Locally, servers left running by an earlier run are reused (they are on ports of their own); a CI run always starts fresh.
      reuseExistingServer: !process.env['CI'],
      timeout: 240_000
    }
  ]
});
