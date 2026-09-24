using System.Net;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

public record SettingRow(string Name, string Group, string Value, string DefaultValue, bool IsSensitive, bool HasValue);
public record AuditRow(int Id, string Action, string? UserName, string Outcome, bool Succeeded, int DurationMs, string? ClientIp, string? HttpMethod, string? Url);
public record AuditPropertyRow(string Property, string? Original, string? New);
public record AuditChangeRow(string EntityType, string EntityId, string ChangeType, List<AuditPropertyRow> Properties);
public record AuditDetail(int Id, string Action, string Outcome, string? Parameters, string? Error, string? ClientBrowser, List<AuditChangeRow> Changes);
public record NotificationRow(int Id, string Type, string Title, string Message, bool IsRead);
public record SubscriptionRow(string Type, bool InApp, bool Email);

/// <summary>F-Settings (scopes, secrets), F-Audit and F-Notifications end to end.</summary>
public class SettingsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private Task<ApiClient> AdminAsync() => factory.SignInAsync("admin@example.com", "Admin");

    private static async Task<Dictionary<string, string>> PublicAsync(ApiClient client) =>
        (await client.GetAsync<Dictionary<string, string>>("/api/settings/public")).Data!;

    [Fact]
    public async Task A_users_own_value_beats_the_application_value_which_beats_the_default()
    {
        var admin = await AdminAsync();
        var omar = await factory.SignInAsync("omar@example.com");

        (await PublicAsync(omar))["General.DefaultLanguage"].Should().Be("en");                                   // code default

        await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "General.DefaultLanguage", value = "ar" } } });
        (await PublicAsync(omar))["General.DefaultLanguage"].Should().Be("ar");                                   // Application override

        await omar.PostAsync("/api/settings/update-mine", new { items = new[] { new { name = "General.DefaultLanguage", value = "en" } } });
        (await PublicAsync(omar))["General.DefaultLanguage"].Should().Be("en");                                   // User override
        (await PublicAsync(admin))["General.DefaultLanguage"].Should().Be("ar");                                  // others unaffected (AC-SET-1)
    }

    [Fact]
    public async Task A_user_cannot_override_a_setting_that_is_application_only()
    {
        var omar = await factory.SignInAsync("omar2@example.com");

        var response = await omar.PostAsync("/api/settings/update-mine",
            new { items = new[] { new { name = "Security.AllowSelfRegistration", value = "false" } } });

        response.Status.Should().Be(HttpStatusCode.BadRequest);
        response.Errors.Should().Contain(e => e.Contains("cannot be set per user"));
    }

    [Fact]
    public async Task An_unknown_time_zone_is_rejected()
    {
        var omar = await factory.SignInAsync("omar3@example.com");

        var response = await omar.PostAsync("/api/settings/update-mine",
            new { items = new[] { new { name = "Appearance.Timezone", value = "Mars/Base" } } });

        response.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Changing_application_settings_requires_the_permission()
    {
        var plain = await factory.SignInAsync("plain.settings@example.com");

        (await plain.GetAsync<List<SettingRow>>("/api/settings/list")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await plain.PostAsync("/api/settings/update", new { items = new[] { new { name = "General.ApplicationName", value = "x" } } }))
            .Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_sensitive_setting_is_write_only_and_encrypted_at_rest()
    {
        var admin = await AdminAsync();
        const string secret = "Sup3r-Secret-Marker-42";

        var saved = await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Email.SmtpPassword", value = secret } } });
        var listing = await admin.GetAsync<List<SettingRow>>("/api/settings/list");
        var raw = await admin.Http.GetStringAsync("/api/settings/list");
        var publicRaw = await admin.Http.GetStringAsync("/api/settings/public");
        var password = listing.Data!.Single(s => s.Name == "Email.SmtpPassword");

        saved.Status.Should().Be(HttpStatusCode.OK);
        password.IsSensitive.Should().BeTrue();
        password.HasValue.Should().BeTrue();
        password.Value.Should().BeEmpty();                                   // AC-SET-2: masked on read
        raw.Should().NotContain(secret);
        publicRaw.Should().NotContain(secret);

        await using var scope = new SqlProbe(factory);
        (await scope.StoredValueAsync("Email.SmtpPassword")).Should().StartWith("enc:").And.NotContain(secret);
    }

    [Fact]
    public async Task A_blank_value_keeps_a_secret_and_an_explicit_clear_removes_it()
    {
        var admin = await AdminAsync();
        await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Email.SmtpPassword", value = "keep-me" } } });

        await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Email.SmtpPassword", value = "" } } });
        (await admin.GetAsync<List<SettingRow>>("/api/settings/list")).Data!.Single(s => s.Name == "Email.SmtpPassword").HasValue.Should().BeTrue();

        await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Email.SmtpPassword", value = "", clear = true } } });
        (await admin.GetAsync<List<SettingRow>>("/api/settings/list")).Data!.Single(s => s.Name == "Email.SmtpPassword").HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task A_type_mismatch_is_rejected()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Security.DefaultUserSessionTimeoutMinutes", value = "soon" } } });

        response.Status.Should().Be(HttpStatusCode.BadRequest);
    }
}

public class AuditTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private Task<ApiClient> AdminAsync() => factory.SignInAsync("admin@example.com", "Admin");

    [Fact]
    public async Task A_successful_change_records_who_where_how_long_and_what_changed()
    {
        var admin = await AdminAsync();
        var role = await admin.PostAsync<string>("/api/roles/create", new { name = "Ops", displayName = "Ops", isDefault = false });

        await admin.PostAsync("/api/roles/update", new { roleId = role.Data, displayName = "Operations", isDefault = false });

        var latest = (await admin.GetAsync<PagedData<AuditRow>>("/api/audit/list?page=1&pageSize=1&sort=createdAt:desc")).Data!.Items.Single();
        var detail = (await admin.GetAsync<AuditDetail>($"/api/audit/get?id={latest.Id}")).Data!;

        latest.Action.Should().Be("UpdateRoleCommand");
        latest.Outcome.Should().Be("Success");
        latest.HttpMethod.Should().Be("POST");
        latest.Url.Should().Be("/api/roles/update");
        latest.UserName.Should().Be("admin@example.com");
        latest.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        detail.Changes.Should().ContainSingle(c => c.EntityType == "ApplicationRole" && c.ChangeType == "Updated")
            .Which.Properties.Should().Contain(p => p.Property == "DisplayName" && p.Original == "Ops" && p.New == "Operations");
    }

    [Fact]
    public async Task Secrets_never_reach_the_audit_trail()
    {
        var admin = await AdminAsync();
        await admin.PostAsync<string>("/api/users/create", new { email = "audit.secret@example.com", password = "Sup3r-Secret-Marker-42", roles = Array.Empty<string>() });

        var ids = (await admin.GetAsync<PagedData<AuditRow>>("/api/audit/list?page=1&pageSize=5&sort=createdAt:desc")).Data!.Items.Select(i => i.Id);
        var everything = string.Concat(await Task.WhenAll(ids.Select(id => admin.Http.GetStringAsync($"/api/audit/get?id={id}"))));

        everything.Should().NotContain("Sup3r-Secret-Marker-42");
        everything.Should().Contain("[redacted]");
    }

    [Fact]
    public async Task A_refused_request_is_recorded_as_Forbidden_with_the_caller()
    {
        var admin = await AdminAsync();
        var plain = await factory.SignInAsync("refused@example.com");

        (await plain.GetAsync<PagedData<object>>("/api/users/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.Forbidden);

        var entries = (await admin.GetAsync<PagedData<AuditRow>>("/api/audit/list?page=1&pageSize=10&outcome=Forbidden")).Data!.Items;
        entries.Should().Contain(e => e.Action == "ListUsersQuery" && e.UserName == "refused@example.com" && !e.Succeeded);
    }

    [Fact]
    public async Task A_failed_command_is_recorded_even_though_its_work_was_rolled_back()
    {
        var admin = await AdminAsync();

        await admin.PostAsync<string>("/api/roles/create", new { name = "Admin", displayName = "duplicate", isDefault = false });

        var failures = (await admin.GetAsync<PagedData<AuditRow>>("/api/audit/list?page=1&pageSize=10&outcome=Conflict")).Data!.Items;
        failures.Should().Contain(e => e.Action == "CreateRoleCommand" && !e.Succeeded);
    }

    [Fact]
    public async Task The_viewer_filters_by_outcome_and_date_range()
    {
        var admin = await AdminAsync();
        await admin.PostAsync<string>("/api/roles/create", new { name = "Filtered", displayName = "f", isDefault = false });
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        int Total(ApiResult<PagedData<AuditRow>> r) => r.Data!.TotalCount;

        Total(await admin.GetAsync<PagedData<AuditRow>>($"/api/audit/list?page=1&pageSize=1&from={today}&to={today}")).Should().BeGreaterThan(0);
        Total(await admin.GetAsync<PagedData<AuditRow>>("/api/audit/list?page=1&pageSize=1&from=2001-01-01&to=2001-01-02")).Should().Be(0);
        Total(await admin.GetAsync<PagedData<AuditRow>>("/api/audit/list?page=1&pageSize=1&outcome=Success")).Should().BeGreaterThan(0);
        Total(await admin.GetAsync<PagedData<AuditRow>>("/api/audit/list?page=1&pageSize=1&outcome=Failure")).Should().Be(0);
    }

    [Theory]
    [InlineData("PUT", "/api/audit/1")]
    [InlineData("DELETE", "/api/audit/1")]
    [InlineData("POST", "/api/audit/update")]
    [InlineData("POST", "/api/audit/delete")]
    public async Task There_is_no_endpoint_that_modifies_or_deletes_audit_records(string method, string url)
    {
        var admin = await AdminAsync();

        var response = await admin.Http.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed); // AC-AUDIT-3
    }

    [Fact]
    public async Task Viewing_the_audit_log_needs_its_own_permission()
    {
        var plain = await factory.SignInAsync("noaudit@example.com");

        (await plain.GetAsync<PagedData<AuditRow>>("/api/audit/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.Forbidden);
    }
}

public class NotificationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private Task<ApiClient> AdminAsync() => factory.SignInAsync("admin@example.com", "Admin");

    private static async Task<int> UnreadAsync(ApiClient client) => (await client.GetAsync<int>("/api/notifications/unread-count")).Data;

    [Fact]
    public async Task A_notification_reaches_its_recipient_and_can_be_marked_read()
    {
        var admin = await AdminAsync();
        var user = await factory.SignInAsync("recipient@example.com");
        var recipientId = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=50&filter=recipient@")).Data!.Items.Single(u => u.Email == "recipient@example.com").Id;

        await admin.PostAsync("/api/notifications/send", new { userId = recipientId, title = "Hello", message = "World" });

        (await UnreadAsync(user)).Should().Be(1);
        (await user.GetAsync<PagedData<NotificationRow>>("/api/notifications/list?page=1&pageSize=5")).Data!.Items
            .Should().ContainSingle(n => n.Title == "Hello" && n.Type == "General" && !n.IsRead);

        await user.PostAsync("/api/notifications/mark-read", new { ids = (int[]?)null });
        (await UnreadAsync(user)).Should().Be(0);
    }

    [Fact]
    public async Task Notifications_are_private_to_their_recipient()
    {
        var admin = await AdminAsync();
        var alice = await factory.SignInAsync("alice.n@example.com");
        var bob = await factory.SignInAsync("bob.n@example.com");
        var aliceId = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=50&filter=alice.n@")).Data!.Items.Single().Id;

        await admin.PostAsync("/api/notifications/send", new { userId = aliceId, title = "Only for Alice", message = "private" });

        (await UnreadAsync(alice)).Should().Be(1);
        (await UnreadAsync(bob)).Should().Be(0);
    }

    [Fact]
    public async Task Opting_out_of_a_channel_for_a_type_stops_that_delivery_but_other_types_still_arrive()
    {
        var admin = await AdminAsync();
        var user = await factory.SignInAsync("optout@example.com");
        var userId = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=50&filter=optout@")).Data!.Items.Single().Id;

        var defaults = (await user.GetAsync<List<SubscriptionRow>>("/api/notifications/subscriptions/get")).Data!;
        defaults.Should().OnlyContain(s => s.InApp && s.Email);                     // everything on until the user chooses (FR-NOTIF-006)

        await user.PostAsync("/api/notifications/subscriptions/update", new { items = new[] { new { type = "General", inApp = false, email = false } } });
        await admin.PostAsync("/api/notifications/send", new { userId, title = "General news", message = "muted" });
        (await UnreadAsync(user)).Should().Be(0);                                    // AC-NOTIF-2: the opted-out type is not delivered in-app

        // The choice is per type: General is now off, the others are untouched.
        var after = (await user.GetAsync<List<SubscriptionRow>>("/api/notifications/subscriptions/get")).Data!;
        after.Single(s => s.Type == "General").InApp.Should().BeFalse();
        after.Where(s => s.Type != "General").Should().OnlyContain(s => s.InApp && s.Email);
    }

    [Fact]
    public async Task Only_holders_of_the_send_permission_can_send()
    {
        var user = await factory.SignInAsync("nosend@example.com");

        (await user.PostAsync("/api/notifications/send", new { userId = (string?)null, title = "x", message = "y" }))
            .Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_title_is_required()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsync("/api/notifications/send", new { userId = (string?)null, title = "", message = "y" });

        response.Status.Should().Be(HttpStatusCode.BadRequest);
    }
}
