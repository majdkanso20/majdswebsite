# SRS v2.1 traceability audit — Majd's Platform

Audited against `Application-Template-SRS_2.md` on 2026-09-24. Method: read-only code inspection; nothing was built, run or load-tested for this audit. "DONE" means the code was seen. Where an item was inferred rather than read, the evidence says so.

## Scorecard (201 functional requirements)

| Status | Count |
|---|---|
| DONE | 91 |
| PARTIAL | 91 |
| MISSING | 19 |

| Group | Total | Done | Partial | Missing |
|---|---|---|---|---|
| P1 API conventions | 8 | 5 | 3 | 0 |
| P2 Modules | 8 | 3 | 4 | 1 |
| P3 Repository/UoW | 7 | 2 | 5 | 0 |
| P4 Cross-cutting | 7 | 2 | 2 | 3 |
| P5 Plugins | 42 | 11 | 23 | 8 |
| U1 Skinnable UI | 8 | 4 | 4 | 0 |
| U2 Mobile-first | 9 | 4 | 5 | 0 |
| U3 App shell | 8 | 5 | 3 | 0 |
| F-Authorization | 8 | 5 | 3 | 0 |
| F-Users | 8 | 5 | 3 | 0 |
| F-Roles | 5 | 3 | 2 | 0 |
| F-Account | 6 | 4 | 2 | 0 |
| F-Settings | 6 | 5 | 1 | 0 |
| F-Audit | 6 | 5 | 1 | 0 |
| F-Notifications | 9 | 3 | 5 | 1 |
| F-Files | 6 | 2 | 3 | 1 |
| F-Localization | 6 | 1 | 4 | 1 |
| F-Errors | 5 | 3 | 2 | 0 |
| F-Data | 6 | 1 | 5 | 0 |
| F-Export | 5 | 4 | 1 | 0 |
| F-Dashboard | 4 | 4 | 0 | 0 |
| F-Background-Jobs | 5 | 0 | 4 | 1 |
| F-Health | 4 | 1 | 2 | 1 |
| F-ApiDocs | 4 | 1 | 2 | 1 |
| F-Caching | 3 | 0 | 2 | 1 |
| F-Features | 4 | 4 | 0 | 0 |
| F-Search | 4 | 4 | 0 | 0 |

## P1 — API conventions
- DONE: 001 (GET/POST only), 002, 005 (`ApiControllerBase`), 006 (status-code filter), 007 (model-state + exception middleware).
- PARTIAL: 003 routes mostly follow `/api/{module}/{action}` but `search`, `roles/permissions/*`, `session/permissions`, `permissions/tree` differ. 004 CSV export, file download, profile picture, external login and the Identity API return raw bodies. 008 schema is inferred by Swashbuckle, no `ProducesResponseType`.

## P2 — Modules
- DONE: 001 `IFeatureModule`, 005 application parts, 006 EF configs discovered.
- PARTIAL: 002 host still lists module assemblies by hand. 003 Scrutor scan covers service markers, jobs, search; EF configs applied separately. 007 no Contracts projects; Users/Roles reference Authorization directly, Account references Files. 008 removing a module still needs edits in `Program.cs` and the csproj.
- MISSING: 004 no per-module options with validate-on-start.

## P3 — Repository / Unit of Work
- DONE: 004 `IUnitOfWork`/`EfUnitOfWork`, 006 scoped registration.
- PARTIAL: 001–003, 007: `IRepository`, `IReadRepository`, specifications exist but only the Diagnostics module uses them; every other handler injects `ApplicationDbContext`. 005 audit stamping done, soft delete only enforced in the repository, no domain events.

## P4 — Cross-cutting
- DONE: 001, 002 (behavior pipeline: logging, performance, validation, authorization, feature, caching, transaction, audit).
- PARTIAL: 003 markers used, but `ICacheableQuery` is implemented by no request. 005 correlation, exception and security-headers middleware; no response-wrapping middleware.
- MISSING: 004 decorators (Scrutor `Decorate`), 006 Polly, 007 Mapster/AutoMapper (DTOs hand-mapped).

## P5 — Plugins (42)
- DONE (11): 007 module + controllers registered, 008 per-plugin failure isolation with persisted `LastError`, 009 host-version check (`minHostVersion`/`maxHostVersion` in the manifest, checked when a package is uploaded and again at load; an incompatible plugin is refused with the reason), 021 menu ingested only for enabled plugins, 025 permissions appear in role editor, 026 grantable to roles and users, 027 enforced by the same authorization pipeline, 028 uninstall cleanup (the plugin's permissions are removed from every role and user that held them and permission caches are refreshed; tested), 038 upgrade and rollback (a newer package upgrades, the previous version is kept and can be restored; compatibility is checked), 039 every install, upgrade, rollback, uninstall, enable and disable is an audited command gated by `Plugins.Manage`, 042 the plugin list shows declared permissions with a link to the roles screen (it does not pre-select the plugin's permissions).
- PARTIAL (23): 001, 002 (manifest is a small subset), 003, 004, 005 (startup scan, and staged installs are applied at start), 006 (collectible context, never unloaded), 011 (prefixed table; migration lives in the host), 012, 015 (schema-driven renderer instead of Native Federation), 017, 020, 022, 024, 029, 030, 032 (a package is fully verified, staged and applied at start with an undo if it cannot be applied, so the host is never left half-installed; plugin database migrations are not part of this), 033 (API blocked at once; assembly stays loaded), 034 (uninstall stops the plugin at once, removes its menu, permissions and registry entry, and deletes its files at the next start; removing its data on explicit confirmation is not offered, data is always kept), 035, 036 (SHA-256 checksum the publisher can supply, the package's checksum is reported, and an allow-list of id + checksum in `Plugins:Trust`; there is no digital signature), 037, 040 (install/upload, enable, disable, uninstall, upgrade and rollback are in the UI; per-plugin settings are not), 041 (load errors and the declared platform range are shown; no fuller compatibility report).
- MISSING (8): 010 dependency resolution, 013 per-plugin settings, 014 frontend bundles, 016 static asset serving, 018 CSS isolation, 019 shared-dependency check, 023 plugin localization, 031 lifecycle hooks.
- Not verified live: FR-PLUG-022 route guard on the dynamic route (permission guard is attached in code); FR-PLUG-037 with a throwing plugin.

## U1 — Skinnable UI (updated 2026-09-24)
- DONE: 001 no component uses an inline template or inline styles (five were externalized; ESLint now fails the build if one returns, verified with a deliberately bad component). 003 M3 theming. 006 named skins: Azure (default), Ocean, Forest, Sunset, Violet, each one token file under `theme/skins/`, switchable at runtime from the header and persisted (verified: the primary color changes app-wide). 008 lint rules: ESLint (external templates, template accessibility) and Stylelint (no hex, `rgb()` or named colors outside `theme/`); both pass and both were shown to catch violations.
- PARTIAL: 002 tokens now cover spacing, radius, tap target and motion, and the only hard-coded color is the QR background token, but older component styles still use raw pixel values. 004 every component is OnPush (Angular 22 default, and declared), but pages still mix logic and markup. 005 the data grid takes template overrides and the empty/error states accept projected content; not every shared component does. 007 English literals remain in some TypeScript (snackbar messages are translated at display time).

## U2 — Mobile-first (updated 2026-09-24)
- DONE: 001 `@container` queries on the data grid and dashboard (verified: at 320px each row becomes a labelled card, no sideways scrolling). 004 44px minimum for icon buttons and buttons (verified: smallest of 30 buttons is 44px). 006 dark mode, reduced motion, and safe-area insets now effective (`viewport-fit=cover` added). 008 responsive drawer.
- PARTIAL: 002 320px verified on the Users page only. 003 fluid heading sizes and logical properties in new code; older code still fixed. 005 no native `<dialog>`, lazy images or View Transitions. 007 PWA: web manifest, icons, and an Angular service worker with an offline app shell are built into the production output (`ngsw.json` verified), but service worker registration could not be tested because this embedded browser refuses all service workers, including a trivial test one. 009 lazy routes and budgets (initial 705 kB); no Lighthouse run.

## U3 — App shell (updated 2026-09-24)
- DONE: 001, 002 nested menu groups (Administration), 004, 006, 008 plugin menu entries follow enable/disable live without a reload (verified).
- PARTIAL: 003 no separate settings entry in the user menu. 005 a shared error-state component now exists, but loading/empty/error do not all support template overrides. 007 skip link, labelled navigation and visible focus are in place (skip link focus verified; its appear-on-focus styling could not be, the browser pane never reports focus); no contrast audit.

## F-Authorization
- DONE: 001, 002, 005, 006 (role-permission and user-role edits both refresh the cache; tested), 007 (last administrator protected on delete, deactivate and role removal; tested).
- PARTIAL: 003 direct user grants/denies are computed but there is no API or UI to set them. 004 done in the behavior, no action filter. 008 discovery by assembly scan only; disabled plugins' permissions still listed.

## F-Users / F-Roles
- Users DONE: 003, 004, 005, 006, 007 (self-delete, last-admin delete/deactivate and last-admin role removal are all refused; tested). PARTIAL: 001 (no status filter), 002 (password only, no emailed set-password link), 008 (min length hard-coded to 6).
- Roles DONE: 001, 002, 005. PARTIAL: 003 (default-role auto-assignment not confirmed), 004 (blocks deletion, no reassignment).

## F-Account
- DONE: 001, 002, 005 (notification preferences matrix), 006. PARTIAL: 003 (no remove; type check trusts the client), 004 (2FA, language and time zone are stored per user; time zone is not yet applied to date display).

## F-Settings (updated 2026-09-24)
- DONE: 001 definitions declare whether a user may override them (`AllowUserOverride`); 002 precedence User > Application > code default (verified through the API: an application change reached one user but not the user who had overridden it); 003 typed getters plus a permission-guarded Application update and a self-scoped `settings/my` and `settings/update-mine` for the User scope (validates that the setting allows user override and, for time zone, that it exists); 004 caching with invalidation for both scopes.
- DONE: 006 sensitive settings are encrypted at rest (ASP.NET Data Protection, stored as `enc:...`), masked on every read (only `hasValue` is returned), changed write-only (blank keeps, explicit clear removes), and refused at User scope. Verified: database row is ciphertext, the secret appears in no API response, non-admins get 403, and the stored SMTP password is what the mail sender uses.
- PARTIAL: 005 admin page groups by category, now including Email and Appearance; no Notifications group.
- Now used by: the user's language and time zone (Account > My preferences, and the header language switch). Not yet applied: dates are still formatted in the browser's zone, not the saved time zone.

## F-Audit (updated 2026-09-24)
- DONE: 001 every audited action records user, action, UTC time, HTTP method and path, duration, client IP, browser, outcome and error (verified). 002 property-level before/after values for each entity an audited action created, changed or deleted, stored in `AuditEntityChanges` / `AuditPropertyChanges` (verified for a role rename and a user delete). 003 paged viewer with text, date-range and outcome filters, a detail endpoint and dialog, and an export that honours the same filters. 004 redaction: credential-like fields (password, hash, token, stamp, secret, and similar) and encrypted setting values are replaced with `[redacted]` in both parameters and change values (verified: a created user's password appears nowhere). 005 append-only: there is no modify or delete endpoint (404s verified) and the save interceptor refuses to update or delete audit rows; retention is the only deletion path.
- PARTIAL: 006 refused and failed actions are now recorded (verified: a permission denial and a duplicate-role failure appear with their outcomes, and the failure row survives the rolled-back transaction). Denials issued by ASP.NET before the request reaches a handler, such as a missing or invalid token, are not recorded.
- Limits: only `IAuditableCommand`s carry data changes; bulk `ExecuteUpdate`/`ExecuteDelete` statements bypass change tracking; notification, job-run and audit tables are excluded on purpose; client IP is the direct connection address (no forwarded-header handling); entries written before this change have no new fields.

## F-Notifications (updated 2026-09-24)
- DONE: 005 SignalR hub `/hubs/notifications` pushes to the user in real time (verified live: toast and badge without reload). 006 per-type x channel preferences with an Account matrix, honored by the dispatcher (verified: opted-out type not delivered). 007 email goes through a queue with retry and exponential backoff, then Failed after 3 attempts (verified: rows retried against the rejecting Gmail server).
- PARTIAL: 001 dispatcher is `NotificationPublisher` (same `IUserNotificationPublisher` interface, now typed) rather than a separate `INotificationDispatcher`. 002 two channels (in-app, email); no SMS or push, and no shared `INotificationChannel` interface yet. 004 type stored on the notification; delivery status tracked for queued channels only, no severity or payload. 009 new channel still needs dispatcher edits.
- PARTIAL (008): email provider settings (host, port, user, encrypted password, from) are editable in Settings with a "Send test email" action; SMS/push providers do not exist.
- MISSING: 003 localized templates (messages are plain strings).
- Note: real email delivery is blocked by the rejected Gmail credentials; the retry path is proven, a successful send is not.

## F-Files
- DONE: 003, 004. PARTIAL: 001 (concrete disk class, no `IFileStorage`, no cloud), 002 (blocked-extension list, size cap, no content sniffing), 005 (streamed, not chunked). MISSING: 006 soft delete and cleanup job.

## F-Localization
- DONE: 003 runtime language switch (not re-verified in the running app). PARTIAL: 001 frontend keys only; 004 browser-locale dates, no user timezone or currency; 005 fallback to English; 006 RTL direction set, logical properties used sparingly. MISSING: 002 backend localization and `Accept-Language`.

## F-Errors
- DONE: 001, 002, 004 (a central interceptor handles 401, 403, 429, unreachable server and 5xx with a translated message, and leaves field errors to each screen). PARTIAL: 003 Serilog is referenced and enriched but never configured as the logger; 005 no sinks configured.

## F-Data
- DONE: 002. PARTIAL: 001 column filters ad hoc, 003 DB-side paging but not tied to the repository and unknown sort keys are ignored, 004 cap is a constant (100), 005 grid has no filter UI, 006 permission checks live in consumers.

## F-Export / F-Dashboard / F-Background-Jobs
- Export: DONE 001 (CSV, Excel and PDF from one renderer, `TabularExport`, so all formats hold the same rows; users and audit exports take the list's filters and share its filter code), 002 (server-generated PDF, landscape A4, paginated, capped at 2,000 rows and says so). Formats: `?format=csv|xlsx|pdf`; unknown values are a 400. DONE 003 (import from CSV or Excel: per-row validation through the same `CreateUserCommand` as the create endpoint, valid rows imported, invalid rows reported with row number and reason, whole-file problems such as a missing column rejected up front; users import is wired end to end with a reusable dialog), 005 (downloadable CSV and Excel templates, headers only, plus an Instructions sheet). PARTIAL 004: exports run as background jobs (`ExportJob` queue, hosted worker, atomic claim, restart recovery, retention cleanup), the file is delivered through F-Files and the user gets a notification that links to My exports (AC-EXP-3); imports still run during the request (5,000-row limit), so the requirement is not fully met. Only users have an import so far; other resources reuse `TabularReader`, `ImportRunner` and `ImportTemplate`.
- Dashboard: DONE 001-004. Widgets implement `IDashboardWidget` and register from any module; the dashboard lists those the caller may see and each tile loads its own data (deferred until in view). Baseline widgets: users KPI, unread-notifications KPI, failed-actions KPI, 7-day activity chart, recent-activity feed. Layout (order, hidden) is a per-user setting, `Dashboard.Layout`. Tests: `DashboardTests`, `DashboardHandlerTests`, `dashboard.spec.ts`.
- Jobs: PARTIAL 001 (interval jobs only), 002 (run history persisted, no retry/backoff), 003 (view + run-now, no retry/delete), 005 (audit and notification cleanup, no temp-file job). MISSING 004 job parameters/user context.

## F-Health / F-ApiDocs / F-Caching
- Health: DONE 004. PARTIAL 001 (DB and file storage only), 003 (correlation id, no trace propagation). MISSING 002 metrics.
- ApiDocs: DONE 002 (bearer definition; no security requirement added, UI attach not verified). PARTIAL 001, 004 (development-only gate). MISSING 003 versioning.
- Caching: PARTIAL 001 (no cache abstraction; `IMemoryCache` used directly), 003 (caches exist; invalidation not fully read). MISSING 002 Redis.

## F-Features / F-Search
- Features: all 4 DONE (per-user override is optional in the spec and absent).
- Search: all 4 DONE (providers, permission filtering, debounced grouped UI).

## Non-functional requirements (updated 2026-09-24)
- DONE: NFR-SEC-1 deny-by-default (fallback authorization policy; verified anonymous 401 on unmarked endpoints, Identity `/manage/*` still protected), NFR-SEC-4 rate limiting (auth endpoints 10/min per IP, exports and search 30/min per user, global 600/min; verified 429 with the standard envelope and `Retry-After`), NFR-SEC-6 no stack traces to clients, NFR-MAINT-1 modular projects, NFR-OBS-1 correlation and user id in logs.
- PARTIAL: NFR-SEC-2 HTTPS redirect, config-driven CORS, HSTS (non-development only) and security headers (nosniff, frame deny, referrer policy, permissions policy, COOP, strict CSP; verified on responses). Remaining: the Angular host serves its own headers, which are not configured; HTTPS-only is not enforced in development. NFR-SEC-3 (sort allow-list only partly checked), SEC-5 (settings secrets encrypted at rest; SMTP config secrets still live in user-secrets), PERF-1..4 (nothing measured), SCALE-1 (SQLite and in-memory caches; limiter counters are per instance), OBS-2 (health only), A11Y-1, PRIV-1, PRIV-2.
- MISSING: NFR-SCALE-2 Redis backplane for SignalR (hub itself exists), NFR-MAINT-3 CI and .NET analyzers (frontend ESLint and Stylelint now exist).

## Definition of Done — tests (updated 2026-09-24)
- **Backend: 121 automated tests** in `src/MajdsApp.Tests` (114) and `src/MajdsApp.Tests.Plugins` (7, a separate process because EF caches its model per process), run with `dotnet test MajdsApp.slnx` (about 10 seconds). A GitHub Actions workflow (`.github/workflows/ci.yml`) runs them, and the frontend lint, tests and production build, on every push and pull request. 53 unit tests (redaction, secrets encryption, permission and settings registries, paging, response envelope, plugin manifest rejection) and 68 integration tests that host the real application in-process on a throwaway SQLite database built from the project's own migrations.
- **Covered end to end:** deny-by-default and hardening headers, login and deactivated accounts, rate limiting, roles/users/permissions (with permission-denied and validation-failure cases), settings scopes and encrypted secrets, audit trail (changes, redaction, refusals, failures, filters, append-only), notifications and per-type opt-out, forgot/reset password and registration, and a real runtime plugin load (the sample Tasks plugin is built separately, dropped into a temporary plugins folder, and exercised through its API, permissions, menu and enable/disable).
- **Frontend: 37 automated tests** (vitest via `ng test`): menu filtering and live plugin replacement, theme and skin persistence, the auth and error interceptors, the login page, the data grid (template overrides, card-layout labels, empty state) and the error state. Run with `npm test` in `src/majds-app-web`.
- **Bugs the tests found and that are now fixed:** (1) removing the Administrator role from the last administrator by editing the user was allowed (FR-USER-007); (2) a user's cached permissions were not refreshed after their roles changed, so a newly granted permission did not apply until the cache expired (FR-AUTHZ-006, AC-AUTHZ-2).
- **Not covered:** browser end-to-end tests (Playwright), the two-factor and Google sign-in flows, file upload and download, background jobs, CSV export contents, SignalR delivery, accessibility and Lighthouse checks, Testcontainers/SQL Server (tests use SQLite, the same provider the app uses today), and a CI pipeline that runs them.
- **Documentation:** every project now has a README (24 in total: root, platform projects, all 15 modules, the sample plugin, the plugins folder, the tests, the frontend and the Razor site). Each module README lists its endpoints, permissions, settings, tables, jobs and configuration keys. A CI pipeline now exists (see above).

## Outside the SRS but blocking
- Gmail SMTP credentials are rejected (535), so no email works: registration confirmation, admin reset and forgot-password. Regenerate the app password.
- The SRS's P1 note says `Errors` stays `[JsonIgnore]`; this project deliberately serializes it, which the same note lists as an allowed revision, so validation messages reach the UI.

## Order the gaps were closed, and what is left
Done (2026-09-24): notifications with SignalR and preferences; user-scope and encrypted settings; security hardening (deny-by-default, rate limiting, headers, HSTS); audit trail; frontend rules, skins, PWA build and container queries; automated tests; READMEs.

Still open, roughly by value:
1. P5 remainder: digital signatures, per-plugin settings and database schema, lifecycle hooks, dependency resolution, applying changes without a restart.
2. Browser end-to-end tests, a CI pipeline, and tests for two-factor, Google sign-in, files, jobs and SignalR delivery.
3. F-Export (background imports), generic repository adoption (P3), Polly and Mapster (P4).
4. SMS and push notification channels, localized templates, backend localization, Redis cache and SignalR backplane, OpenTelemetry metrics.
5. Working SMTP credentials (currently rejected), so email can be demonstrated end to end.
