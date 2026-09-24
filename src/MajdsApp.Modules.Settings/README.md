# MajdsApp.Modules.Settings

**Settings (F-Settings)** — SRS FR-SET-001..006

Runtime configuration without a redeploy, with two scopes. A setting is declared in code (`SettingDefinition`: name, group, type, default, whether it may be shown to the client, whether a user may override it, whether it is sensitive). Resolution order is **User > Application > code default**.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/settings/list`
- `GET /api/settings/my`
- `GET /api/settings/public`
- `POST /api/settings/update`
- `POST /api/settings/update-mine`

## Permissions

`Settings.View` and `Settings.Edit` for the application scope. `GET /api/settings/public`, `GET /api/settings/my` and `POST /api/settings/update-mine` need only sign-in and act on the caller.

Declared here: `Settings.Edit`, `Settings.View`.

## Settings defined

- `Appearance.Timezone` — Time zone
- `Email.FromEmail` — From address
- `Email.SmtpHost` — SMTP host
- `Email.SmtpPassword` — SMTP password
- `Email.SmtpPort` — SMTP port
- `Email.SmtpUsername` — SMTP username
- `General.ApplicationName` — Application name
- `General.DefaultLanguage` — Default language
- `General.SupportEmail` — Support email
- `Security.AllowSelfRegistration` — Allow users to self-register
- `Security.DefaultUserSessionTimeoutMinutes` — Session timeout (minutes)

## Data

Tables: `SettingUserValues`, `SettingValues`. Migrations live in `MajdsApp.Core`.

## Notes

- User overrides are stored only when they differ from the application value, so users keep following later application changes. Only definitions with `allowUserOverride` accept one (language and time zone today); sensitive settings never do.
- **Sensitive settings** (for example `Email.SmtpPassword`) are encrypted with ASP.NET Core Data Protection and stored as `enc:...`. They are write-only through the API: reads return only whether a value is set, a blank value keeps it, and `clear: true` removes it. Configure a persistent key ring in production or stored secrets become unreadable.
- Email settings override the `Email:Smtp:*` configuration when filled in, so the mail server and password can be rotated from the UI.
- Effective values are cached for five minutes and invalidated on change.

## Configuration keys

- Add a setting by declaring a `SettingDefinition` in any module; it appears in the Settings screen automatically.

## Tests

Covered by the integration tests in `src/MajdsApp.Tests` (run `dotnet test src/MajdsApp.Tests`).
