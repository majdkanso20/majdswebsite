using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using MajdsApp.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Tests.Support;

/// <summary>
/// Hosts the real application in-process against a throwaway SQLite database (created by applying the
/// project's actual migrations), so integration tests exercise the same pipeline production runs:
/// authentication, deny-by-default authorization, MediatR behaviors, audit, and the real handlers.
/// Every factory gets its own database file and plugin folder, so test classes never share state.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Test1234!";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "majds-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string> _settings;

    public string PluginsDirectory => Path.Combine(_root, "plugins");

    public ApiFactory() : this(null, null)
    {
    }

    // Not public: xunit requires a class fixture to expose exactly one public constructor.
    internal ApiFactory(Dictionary<string, string>? extraSettings, Action<string>? preparePluginsFolder)
    {
        Directory.CreateDirectory(PluginsDirectory);
        preparePluginsFolder?.Invoke(PluginsDirectory);

        var connectionString = $"DataSource={Path.Combine(_root, "test.db")}";
        _settings = new Dictionary<string, string>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Plugins:Directory"] = PluginsDirectory,
            // Generous by default so ordinary tests are never throttled; the rate-limit tests lower these.
            ["RateLimiting:AuthPermitLimit"] = "10000",
            ["RateLimiting:ExpensivePermitLimit"] = "10000",
            ["RateLimiting:GlobalPermitLimit"] = "100000"
        };
        if (extraSettings is not null)
            foreach (var (key, value) in extraSettings) _settings[key] = value;

        MigrateDatabase(connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" (not "Development") so user-secrets, SMTP and Google credentials from the developer's
        // machine are never picked up; UseSetting because Program reads some keys before the host is built.
        builder.UseEnvironment("Testing");
        foreach (var (key, value) in _settings) builder.UseSetting(key, value);
    }

    private static void MigrateDatabase(string connectionString)
    {
        // Module assemblies are discovered by the DbContext through the AppDomain, so load them all first.
        foreach (var dll in Directory.GetFiles(AppContext.BaseDirectory, "MajdsApp.Modules.*.dll"))
            Assembly.LoadFrom(dll);

        // The snapshot includes the sample plugin's table (its migration lives in the host, a documented
        // simplification of P5 FR-PLUG-011), so a run without that plugin looks like "pending changes".
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        using var db = new ApplicationDbContext(options);
        db.Database.Migrate();
    }

    public async Task<ApplicationUser> CreateUserAsync(string email, string role = "User", bool active = true)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true, FullName = email.Split('@')[0], IsActive = active
        };
        var created = await users.CreateAsync(user, Password);
        if (!created.Succeeded) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);
        return user;
    }

    /// <summary>Creates the user if needed, signs in over HTTP like a real client, and returns a client carrying the bearer token.</summary>
    public async Task<ApiClient> SignInAsync(string email, string role = "User")
    {
        using (var scope = Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await users.FindByEmailAsync(email) is null) await CreateUserAsync(email, role);
        }

        var http = CreateClient();
        var response = await http.PostAsJsonAsync("/api/identity/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<LoginResult>())!.AccessToken;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new ApiClient(http);
    }

    public ApiClient Anonymous() => new(CreateClient());

    private record LoginResult(string AccessToken);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        // A loaded plugin assembly stays locked until the process ends (Windows); a leftover temp folder is harmless.
        try { Directory.Delete(_root, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
