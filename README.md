# Majd's Platform

[![CI](https://github.com/majdkanso20/majdswebsite/actions/workflows/ci.yml/badge.svg)](https://github.com/majdkanso20/majdswebsite/actions/workflows/ci.yml)

A reusable foundation for business web applications: a **.NET 10 Web API** built from self-registering feature modules, and an **Angular 22 / Angular Material** frontend that is skinnable and mobile-first. It provides sign-in, users, roles and permissions, settings, an audit trail, notifications, files, background jobs, search and runtime-loadable plugins, so a new application only has to add its own business features.

It implements the requirements in the *Application Template SRS* (v2.1). What is done, partial and missing, requirement by requirement, is tracked in [SRS-TRACEABILITY.md](SRS-TRACEABILITY.md).

> The original ASP.NET Identity Razor Pages site in `src/MajdsApp` is kept, and runs independently of the platform. It shares no project reference with the API or the Angular app.

## Quick start

Prerequisites: .NET 10 SDK and Node.js 22+.

```bash
# 1. API (http://localhost:5156, Swagger at /swagger in Development)
dotnet run --project src/MajdsApp.Api --launch-profile http

# 2. Web app (http://localhost:4200) — in a second terminal
cd src/majds-app-web
npm install
npm start
```

On Windows, `start-demo.bat` starts both and opens the browser. [DEMO.md](DEMO.md) is a 12-minute walkthrough with the demo accounts.

The SQLite database (`src/MajdsApp/app.db`) is created by migrations:

```bash
dotnet ef database update --project src/MajdsApp.Core --startup-project src/MajdsApp.Api
```

## Repository layout

| Path | What it is |
|---|---|
| `src/MajdsApp.Api` | The API host: a thin composition root that loads modules and plugins, wires authentication and the request pipeline. |
| `src/MajdsApp.SharedKernel` | The platform backbone: response envelope, pipeline behaviors, module and plugin infrastructure, security, paging, audit and notification abstractions. |
| `src/MajdsApp.Core` | The shared `DbContext`, the user and role entities, and all EF migrations. |
| `src/MajdsApp.Modules.*` | One project per feature (users, roles, settings, audit, notifications, files, ...). Each self-registers. |
| `src/MajdsApp.Plugins.Tasks` | A sample **runtime plugin** with its own permissions, menu entry and CRUD API. Not referenced by the host. |
| `plugins/` | The folder the host scans at startup for plugin packages. |
| `src/majds-app-web` | The Angular frontend. |
| `src/MajdsApp.Tests`, `src/MajdsApp.Tests.Plugins` | Backend unit and integration tests, and the runtime-plugin tests. |
| `.github/workflows` | Continuous integration. |
| `src/MajdsApp` | The original Razor Pages identity site (2FA, Google and Microsoft sign-in). Independent of everything above. |

Every project has its own README describing its endpoints, permissions, settings and configuration.

## How it fits together

1. **Modules.** A feature is a project implementing `IFeatureModule`. The host lists it in one place (`Program.cs`); its handlers, validators, controllers, permissions, settings and entity configurations are then discovered by convention. Nothing else in the host changes.
2. **One request pipeline.** Every use case is a MediatR request wrapped, outermost first, by logging, timing, failure audit, validation, authorization, feature flags, caching, transaction, then audit. Handlers contain only use-case logic. Behavior is opted into with markers: `[RequiresPermission]`, `[RequiresFeature]`, `IAuditableCommand`, `ITransactionalCommand`.
3. **One API contract.** Controllers expose only `GET` and `POST`, and every action returns `ResponseDto<T>` (`code`, `message`, `data`, `errors`). Validation problems, refusals and unexpected errors are mapped to that envelope centrally.
4. **Deny by default.** Every endpoint requires a signed-in user unless it explicitly says otherwise. Permissions, not role names, are checked in code; the `Admin` role holds all of them.
5. **Plugins.** A package dropped into `plugins/` is loaded at startup into its own `AssemblyLoadContext`; its permissions appear in the role editor, its menu entry in the navigation, and an administrator can enable or disable it live. See [src/MajdsApp.Plugins.Tasks](src/MajdsApp.Plugins.Tasks/README.md) and [plugins/README.md](plugins/README.md).

## Configuration

Set through `appsettings*.json`, environment variables (`Section__Key`) or user-secrets. Secrets never belong in source control; the API and the Razor site share one user-secrets store.

| Key | Purpose | Default |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | SQLite database | `DataSource=../MajdsApp/app.db;Cache=Shared` |
| `Spa:AllowedOrigins` | Origins allowed by CORS and by the external-login redirect | `["http://localhost:4200"]` |
| `Plugins:Directory` | Where plugin packages are discovered | `<repo>/plugins` |
| `RateLimiting:AuthPermitLimit` / `ExpensivePermitLimit` / `GlobalPermitLimit` / `WindowSeconds` | Requests per window for auth endpoints (per IP), exports and search (per user), and everything (per user) | `10` / `30` / `600` / `60` |
| `Email:Smtp:Host` `Port` `Username` `Password` `FromEmail` `FromName` | Outgoing mail. Can be overridden at runtime from the Settings screen (the password there is stored encrypted). | Gmail, port 587 |
| `Authentication:Google:ClientId` `ClientSecret` | Enables "Continue with Google" | unset (off) |
| `Authentication:Microsoft:ClientId` `ClientSecret` | Enables "Continue with Microsoft" | unset (off) |
| `WebApp:BaseUrl` | Where admin-initiated password-reset links point (the Razor site) | `http://localhost:5132` |
| `Cache:Provider` | `Memory` (one server), `Redis` (shared by every server) or `Distributed` (a distributed cache held in this process, for testing the shared path) | `Memory` |
| `Cache:Redis:ConnectionString`, `Cache:KeyPrefix` | Redis address; a prefix so several applications can share one Redis | unset, `majds:` |
| `Metrics:Token` / `Metrics:AllowAnonymous` / `Metrics:Enabled` | `/metrics` needs this bearer token; with none it is open only in Development (or when anonymous access is allowed); `false` turns it off | unset |
| `Docs:Enabled` / `Docs:Access` | Outside Development the API docs (`/swagger`) are off unless `Docs:Enabled` is `true`; `Docs:Access` is `Permission` (needs `Docs.View`), `Authenticated` or `Open` | off, `Permission` |
| `Jobs:Scheduler:Enabled` / `Jobs:Worker:Enabled` | Turn the recurring-job scheduler or the queue worker off on a server | on |
| `MediatR:LicenseKey` | Required for production use of MediatR | unset |

Runtime behavior that administrators change without a redeploy (application name, session timeout, self-registration, upload size, mail server, retention, feature flags) is under **Settings** and **Features** in the app.

## Testing

```bash
dotnet test MajdsApp.slnx             # backend: unit + in-process integration tests, plus the plugin tests
cd src/majds-app-web && npm test      # frontend: vitest
npm run lint:all                      # ESLint (TypeScript and templates) + Stylelint
```

The integration tests host the real application against a throwaway SQLite database built from the project's migrations, so they exercise the same pipeline production uses.

**Continuous integration:** `.github/workflows/ci.yml` runs the backend build and both backend test projects, and the frontend lint, unit tests and production build, on every push to `main` and every pull request. Run the same checks locally before pushing.

## Security notes

- Sign-in issues an opaque bearer token; the session length is the *Session timeout* setting.
- Responses carry hardening headers (CSP, `nosniff`, frame denial, referrer and permissions policies); HSTS is enabled outside Development.
- Sensitive settings are encrypted with ASP.NET Core Data Protection. **In production configure a persistent key ring**, or stored secrets become unreadable when keys change.
- The audit log records who did what, from where, the outcome, redacted parameters and property-level changes, and is append-only.
- Plugin code runs inside the API process and is only semi-trusted. Install plugins only from sources you trust; signature checking is not implemented.

## Known limits

The honest list, kept current in [SRS-TRACEABILITY.md](SRS-TRACEABILITY.md): plugin upload/install and signing, per-plugin schema isolation, SMS and push notification channels, Redis backplane, import from Excel/CSV, background exports, and browser end-to-end tests.
