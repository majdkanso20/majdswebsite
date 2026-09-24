# MajdsApp.Tests

Backend automated tests (xunit + FluentAssertions). 121 tests, about five seconds.

```bash
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
| `AdministrationTests` | permissions, users, roles, paging, last-administrator protection, cache refresh |
| `SettingsTests` | user > application > default, encrypted write-only secrets |
| `AuditTests` | metadata, property changes, redaction, refusals, failures, filters, append-only |
| `NotificationTests` | delivery, privacy, per-type opt-out, permission to send |
| `AccountTests` | forgot/reset password, registration, change password, own-record rule |
| `PluginTests`, `NoPluginsTests` | runtime plugin discovery, permissions, menu, CRUD, enable/disable |

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

## Notes

- Tests use SQLite (the provider the app uses today), not Testcontainers/SQL Server.
- The sample plugin project is built by a `ReferenceOutputAssembly="false"` project reference so it is never loaded into the test process unless a test copies it into a plugins folder.
- Not covered yet: browser end-to-end tests, two-factor and Google sign-in, file upload/download, background jobs, CSV contents, SignalR delivery.

Frontend tests live in `src/majds-app-web` (`npm test`).
