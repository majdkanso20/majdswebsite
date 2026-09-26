# majds-app-web

The Angular 22 frontend: standalone components, signals, zoneless change detection, Angular Material 3. It is skinnable, mobile-first, translatable (English and Arabic with RTL) and an installable PWA in production builds.

## Run

```bash
npm install
npm start          # http://localhost:4200, expects the API on http://localhost:5156
npm test           # vitest (ng test)
npm run lint:all   # ESLint (TypeScript + templates) and Stylelint
npm run build      # production build (service worker, budgets)
```

The API address is `apiBaseUrl` in `src/environments/environment.ts` (development) and `environment.production.ts` (`/api`, i.e. the same origin behind a reverse proxy). The API must list this app's origin in `Spa:AllowedOrigins`.

## Structure

```
src/app/
  core/       singletons: auth, guards, interceptors, session/permission/feature/settings services, i18n
  shared/     reusable presentational components: data-grid, empty/error/loading state, global search, active filter
  theme/      design tokens, Material theme, one file per skin, ThemeService
  layout/     the app shell: responsive drawer, header, grouped navigation, MenuService
  features/   administration/ (users, roles, settings, jobs, features, audit log, plugins), account/, auth/, files/,
              dashboard/, plugins/ (generic screen for schema-driven plugins)
```

Every feature is lazy loaded and talks to the API through a typed service. New pages reuse the shell, the data grid, the `*hasPermission` directive and the interceptors; nothing re-implements paging, permission hiding or error toasts.

## Rules the code base enforces

| Rule | Enforced by |
|---|---|
| No inline templates or inline styles in components | ESLint `component-max-inline-declarations` |
| No hard-coded colors (hex, `rgb()`, named) outside `theme/` | Stylelint `color-no-hex`, `function-disallowed-list` |
| Accessible templates | `angular-eslint` template accessibility rules |
| Every visual value comes from a token | convention: use `var(--mat-sys-*)` and `--app-*` |

## Theming and skins (FR-UI-002/003/006)

- `theme/_tokens.scss` defines the Material 3 palette and the `--app-*` tokens (spacing scale, radius, tap target, motion, shell sizes). Components consume tokens only.
- A **skin** is one file in `theme/skins/` that overrides tokens under `html[data-skin='<name>']`. Included: Ocean, Forest, Sunset, Violet (plus the default Azure). Users pick one from the header theme menu; the choice, and light/dark/system, persist in `localStorage`.
- To add a skin: create `theme/skins/_<name>.scss`, `@forward` it in `theme/_skins.scss`, and add it to `SKINS` in `theme.service.ts`.

## Navigation and plugins

`MenuService` is the single menu registry. Entries carry an optional `permission` and `feature`; the same `PermissionService` powers `permissionGuard` and `*hasPermission`, so navigation and route protection cannot drift. A group (`children`) hides itself when nothing inside it is visible.

Plugin menu entries and routes come from `GET /api/plugins/manifest`, loaded before the first navigation and again whenever an administrator enables or disables a plugin, so entries appear and disappear without a reload. Plugin screens use one generic page that renders the plugin's own `ui-schema`.

## Localization

English text is the translation key (`{{ 'Save' | translate }}`); a missing key shows the English. Arabic strings are in `public/i18n/ar.json`. A user's language and time zone are stored as User-scope settings on the server, and the header language switch saves them. Add a language by adding `public/i18n/<code>.json` and an entry in `LANGUAGES`.

## Mobile and PWA

- Layouts are mobile-first; components reflow with **container queries** (the data grid becomes labelled cards in a narrow container).
- Icon buttons and buttons are at least 44x44 px; focus is always visible; there is a skip-to-content link; `prefers-reduced-motion` and safe-area insets are honoured.
- Production builds include a web manifest, icons and an Angular service worker that caches the app shell (`ngsw-config.json`). API responses are deliberately not cached. Registration is disabled in `ng serve`. Test installability in a normal browser against a production build.

## Errors and sessions

- `authInterceptor` adds the bearer token and returns to `/login` on a 401. `errorInterceptor` shows translated messages for 403, 429, an unreachable server and 5xx; field-level errors (400/404/409) are shown by the screen that made the call, using the `errors` array from the response envelope.
- Notifications arrive over SignalR (`NotificationsService`); the connection is made after sign-in and re-fetches on reconnect.

## Testing

Specs sit next to the code (`*.spec.ts`). Present coverage: menu filtering and plugin replacement, theme and skins, both interceptors, the login page, the data grid and the error state. Add a spec with any new shared component or service.

## Browser end-to-end tests

`npm run e2e` runs `e2e/tests` in a real Chrome against the real API on a throwaway database. It starts both servers itself on ports of its own (API 5299, app 4299), so it never touches a development database or a running dev server, and the API creates its first administrator from `Bootstrap:AdminEmail` / `Bootstrap:AdminPassword`, so there is nothing to set up by hand. Set `E2E_BROWSER_CHANNEL=msedge` to use Edge. `npm run e2e:headed` shows the browser. On a failure, screenshots and traces are kept in `test-results/` (open one with `npx playwright show-trace`).

They cover signing in and out, the Arabic right-to-left layout and its persistence after a reload, the phone-width header, a plugin's own screen mounted in a shadow root, and deleting a role that has users. `accessibility.spec.ts` runs an axe WCAG 2.1 A/AA scan (contrast included) on the main pages in the light and the dark theme, and `responsive.spec.ts` checks that no page scrolls sideways at 320px in English or Arabic. Tests that need the sample Tasks plugin skip themselves when it is not built into `plugins/`. CI runs them in the `e2e` job.
