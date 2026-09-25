# MajdsApp.Tests

Backend automated tests (xunit + FluentAssertions): 232 tests here plus 34 runtime-plugin tests in `MajdsApp.Tests.Plugins`, under a minute together.

```bash
dotnet test MajdsApp.slnx                                   # everything, as CI runs it
dotnet test src/MajdsApp.Tests
dotnet test src/MajdsApp.Tests --filter "FullyQualifiedName~AuditTests"      # one class
```

## Two kinds

**Unit tests** (`Unit/`) exercise pure logic: audit redaction, encryption of secrets, the response envelope, paging clamps, the permission and setting registries, and rejection of broken plugin folders.

**Integration tests** (`Integration/`) host the real application in-process with `WebApplicationFactory<Program>`. Each test class gets its own `ApiFactory`, which:

- creates a throwaway SQLite database and applies the project's real migrations,
- runs in the `Testing` environment, so user-secrets, SMTP and Google credentials from a developer machine are never used,
- uses its own empty plugins folder (the plugin tests copy the sample plugin's build output into it),
- raises the rate limits so ordinary tests are not throttled (the rate-limit tests lower them).

Tests sign in over HTTP like a real client (`factory.SignInAsync("a@b.co", "Admin")`) and assert on both the status code and the `ResponseDto` envelope through `ApiClient`.

| Class | Covers |
|---|---|
| `SecurityTests`, `RateLimitTests` | deny-by-default, public endpoints, hub auth, security headers, login, throttling |
| `DashboardTests`, `DashboardHandlerTests` | permission-filtered widgets, data shapes, per-user layout, refused widgets never run |
| `ExportTests`, `ExportWriterTests` | CSV, Excel and PDF, filters, formula safety, permissions |
| `ImportTests`, `ImportReaderTests` | CSV and Excel parsing, per-row errors, templates, permissions |
| `BackgroundExportTests` | queued exports end to end (worker, Files, notification link), failure, limits, ownership, cleanup |
| `BackgroundJobTests`, `JobScheduleTests` | persisted queue, retry with backoff, restart recovery, user context, cron and retry rules, monitoring, cleanup jobs |
| `LocalizationTests`, `MessageCatalogTests` | language selection, translated errors, notifications per recipient, endpoints, source scan for untranslated messages |
| `AdministrationTests` | permissions, users, roles, paging, last-administrator protection, cache refresh |
| `SettingsTests` | user > application > default, encrypted write-only secrets |
| `AuditTests` | metadata, property changes, redaction, refusals, failures, filters, append-only |
| `NotificationTests` | delivery, privacy, per-type opt-out, permission to send |
| `AccountTests` | forgot/reset password, registration, change password, own-record rule |
| `NoPluginsTests` | a host with no plugins starts and lists none |
| `MajdsApp.Tests.Plugins` (separate project) | runtime plugin discovery, permissions, menu, CRUD, enable/disable; package install, upgrade, rollback, uninstall, trust policy, permission cleanup |

## Writing a new integration test

```csharp
public class WidgetTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_user_without_permission_is_refused()
    {
        var plain = await factory.SignInAsync("plain@example.com");           // role "User": no permissions
        var response = await plain.GetAsync<PagedData<Widget>>("/api/widgets/list?page=1&pageSize=5");

        response.Status.Should().Be(HttpStatusCode.Forbidden);
        response.Body!.Message.Should().Contain("Widgets.View");
    }
}
```

Per the platform's Definition of Done, every feature needs a happy path, a permission-denied case and a validation-failure case.

## Why the plugin tests are a separate project

EF Core builds and caches its model once per process from the assemblies loaded at that moment. A plugin's entity is only in the model if the plugin was loaded before the first `DbContext` use, so plugin tests running beside tests that build the model without the plugin failed intermittently (found by running the suite repeatedly). A separate test project is a separate process, which makes the result deterministic, as in a real deployment. It shares the harness by linking `Support/*.cs`.

## Notes

- Tests use SQLite (the provider the app uses today), not Testcontainers/SQL Server.
- Not covered yet: browser end-to-end tests, two-factor and Google sign-in, file upload/download, SignalR delivery.

Frontend tests live in `src/majds-app-web` (`npm test`).
