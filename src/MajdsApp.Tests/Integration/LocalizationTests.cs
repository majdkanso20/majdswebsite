using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

public record LanguageRow(string Code, string Label, bool Rtl, bool IsDefault);
public record ResourcesRow(string Culture, Dictionary<string, string> Messages);

/// <summary>F-Localization end to end: the language a request is answered in, and what is translated.</summary>
public partial class LocalizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static bool HasArabic(string? text) => text is not null && ArabicLetters().IsMatch(text);

    [GeneratedRegex(@"[؀-ۿ]")]
    private static partial Regex ArabicLetters();

    private static async Task<(HttpStatusCode Status, string? Message, string[] Errors)> GetAsync(ApiClient client, string url, string? acceptLanguage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (acceptLanguage is not null) request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);
        return await ReadAsync(await client.Http.SendAsync(request));
    }

    private static async Task<(HttpStatusCode Status, string? Message, string[] Errors)> PostAsync(ApiClient client, string url, object body, string? acceptLanguage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = System.Net.Http.Json.JsonContent.Create(body) };
        if (acceptLanguage is not null) request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);
        return await ReadAsync(await client.Http.SendAsync(request));
    }

    private static async Task<(HttpStatusCode Status, string? Message, string[] Errors)> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (!text.StartsWith('{')) return (response.StatusCode, null, []);
        using var doc = JsonDocument.Parse(text);
        var message = doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        var errors = doc.RootElement.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array
            ? e.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : [];
        return (response.StatusCode, message, errors);
    }

    // ---- Accept-Language ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_error_message_comes_back_in_the_language_the_client_asks_for_with_its_values_intact()
    {
        var plain = await factory.SignInAsync("l10n.plain1@example.com");

        var english = await GetAsync(plain, "/api/users/list?page=1&pageSize=5", null);
        var arabic = await GetAsync(plain, "/api/users/list?page=1&pageSize=5", "ar");

        english.Status.Should().Be(HttpStatusCode.Forbidden);
        english.Message.Should().Be("Missing permission 'Users.View'.");
        arabic.Status.Should().Be(HttpStatusCode.Forbidden);
        arabic.Message.Should().Be("الصلاحية 'Users.View' مفقودة.");                                       // AC-I18N-1
    }

    [Fact]
    public async Task The_clients_preference_order_and_regional_forms_are_respected()
    {
        var plain = await factory.SignInAsync("l10n.plain2@example.com");

        (await GetAsync(plain, "/api/users/list?page=1&pageSize=5", "ar-JO,en;q=0.5")).Message.Should().Be("الصلاحية 'Users.View' مفقودة.");
        (await GetAsync(plain, "/api/users/list?page=1&pageSize=5", "en;q=1, ar;q=0.4")).Message.Should().Be("Missing permission 'Users.View'.");
        (await GetAsync(plain, "/api/users/list?page=1&pageSize=5", "fr,ar;q=0.8")).Message.Should().Be("الصلاحية 'Users.View' مفقودة.");   // first one we speak
    }

    [Fact]
    public async Task A_language_nobody_translated_falls_back_to_English_instead_of_going_blank()
    {
        var plain = await factory.SignInAsync("l10n.plain3@example.com");

        var response = await GetAsync(plain, "/api/users/list?page=1&pageSize=5", "fr-FR");

        response.Message.Should().Be("Missing permission 'Users.View'.");                                  // AC-I18N-2
    }

    [Fact]
    public async Task Not_found_and_conflict_and_business_rule_errors_are_translated_too()
    {
        var admin = await factory.SignInAsync("l10n.admin1@example.com", "Admin");

        (await GetAsync(admin, "/api/jobs/queue?page=1&pageSize=5&status=Banana", "ar")).Errors.Should().ContainSingle().Which.Should().MatchRegex(@"[؀-ۿ]");
        (await PostAsync(admin, "/api/exports/start", new { source = "nope", format = "csv" }, "ar")).Message.Should().Be("لا يوجد تصدير باسم 'nope'.");
        (await PostAsync(admin, "/api/plugins/rollback", new { pluginId = "Not.There" }, "ar")).Errors.Should().ContainSingle().Which.Should().Contain("Not.There");
    }

    [Fact]
    public async Task Validation_messages_from_the_validators_are_translated_by_their_own_language_resources()
    {
        var admin = await factory.SignInAsync("l10n.admin2@example.com", "Admin");

        var english = await PostAsync(admin, "/api/users/create", new { email = "not-an-email", password = "abcdef", roles = Array.Empty<string>() }, null);
        var arabic = await PostAsync(admin, "/api/users/create", new { email = "not-an-email", password = "abcdef", roles = Array.Empty<string>() }, "ar");

        english.Status.Should().Be(HttpStatusCode.BadRequest);
        english.Errors.Should().NotBeEmpty().And.OnlyContain(e => !HasArabic(e));
        arabic.Status.Should().Be(HttpStatusCode.BadRequest);
        arabic.Message.Should().Be("فشل التحقق.");
        arabic.Errors.Should().NotBeEmpty().And.OnlyContain(e => HasArabic(e));
    }

    // ---- the user's saved language -----------------------------------------------------------------------------------------

    [Fact]
    public async Task With_no_language_asked_for_the_users_saved_language_decides_and_an_explicit_request_still_wins()
    {
        var user = await factory.SignInAsync("l10n.saved@example.com");
        await user.PostAsync("/api/settings/update-mine", new { items = new[] { new { name = "General.DefaultLanguage", value = "ar" } } });

        (await GetAsync(user, "/api/users/list?page=1&pageSize=5", null)).Message.Should().Be("الصلاحية 'Users.View' مفقودة.");     // saved
        (await GetAsync(user, "/api/users/list?page=1&pageSize=5", "en")).Message.Should().Be("Missing permission 'Users.View'.");  // explicit
    }

    [Fact]
    public async Task The_application_default_language_applies_to_someone_who_chose_nothing()
    {
        var admin = await factory.SignInAsync("l10n.admin3@example.com", "Admin");
        var newcomer = await factory.SignInAsync("l10n.newcomer@example.com");
        await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "General.DefaultLanguage", value = "ar" } } });

        try
        {
            (await GetAsync(newcomer, "/api/users/list?page=1&pageSize=5", null)).Message.Should().Be("الصلاحية 'Users.View' مفقودة.");
        }
        finally
        {
            await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "General.DefaultLanguage", value = "en" } } });
        }
    }

    // ---- the settings that drive formatting ---------------------------------------------------------------------------------

    [Fact]
    public async Task A_user_can_choose_a_currency_and_the_client_reads_it_back_while_bad_values_are_refused()
    {
        var user = await factory.SignInAsync("l10n.currency@example.com");

        (await user.PostAsync("/api/settings/update-mine", new { items = new[] { new { name = "Appearance.Currency", value = "JOD" } } })).Status.Should().Be(HttpStatusCode.OK);
        (await user.GetAsync<Dictionary<string, string>>("/api/settings/public")).Data!["Appearance.Currency"].Should().Be("JOD");

        var badCurrency = await user.PostAsync("/api/settings/update-mine", new { items = new[] { new { name = "Appearance.Currency", value = "dollars" } } });
        badCurrency.Status.Should().Be(HttpStatusCode.BadRequest);
        badCurrency.Errors.Should().Contain(e => e.Contains("valid currency code"));

        (await user.PostAsync("/api/settings/update-mine", new { items = new[] { new { name = "General.DefaultLanguage", value = "fr" } } }))
            .Errors.Should().Contain(e => e.Contains("not a supported language"));
        (await user.PostAsync("/api/settings/update-mine", new { items = new[] { new { name = "Appearance.Timezone", value = "Not/AZone" } } }))
            .Errors.Should().Contain(e => e.Contains("not a known time zone"));
    }

    [Fact]
    public async Task The_same_rules_apply_when_an_administrator_sets_the_application_wide_value()
    {
        var admin = await factory.SignInAsync("l10n.admin5@example.com", "Admin");

        (await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Appearance.Currency", value = "12" } } })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "General.DefaultLanguage", value = "de" } } })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Appearance.Timezone", value = "Nowhere/Land" } } })).Status.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- notifications are written in each recipient's language --------------------------------------------------------------

    [Fact]
    public async Task A_notification_is_written_in_its_recipients_language_not_the_senders()
    {
        var admin = await factory.SignInAsync("l10n.admin4@example.com", "Admin");
        var arabicReader = await factory.SignInAsync("l10n.reader.ar@example.com");
        var englishReader = await factory.SignInAsync("l10n.reader.en@example.com");
        await arabicReader.PostAsync("/api/settings/update-mine", new { items = new[] { new { name = "General.DefaultLanguage", value = "ar" } } });

        foreach (var email in new[] { "l10n.reader.ar@example.com", "l10n.reader.en@example.com" })
        {
            var target = (await admin.GetAsync<PagedData<UserRow>>($"/api/users/list?page=1&pageSize=5&filter={email}")).Data!.Items.Single();
            await admin.PostAsync("/api/users/update", new { userId = target.Id, fullName = "Reader", phoneNumber = (string?)null, roles = new[] { "User", "Admin" } });   // a real change, so they are told
        }

        var arabicNotes = (await arabicReader.GetAsync<PagedData<NotificationWithLink>>("/api/notifications/list?page=1&pageSize=20")).Data!.Items;
        var englishNotes = (await englishReader.GetAsync<PagedData<NotificationWithLink>>("/api/notifications/list?page=1&pageSize=20")).Data!.Items;

        arabicNotes.Should().Contain(n => n.Title == "تغيّرت صلاحيات وصولك" && n.Message == "حدّث مسؤول أدوارك.");
        englishNotes.Should().Contain(n => n.Title == "Your access changed" && n.Message == "An administrator updated your roles.");
    }

    // ---- the endpoints (FR-I18N-003) -------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_languages_endpoint_lists_what_can_be_chosen_for_signed_in_users_only()
    {
        var user = await factory.SignInAsync("l10n.langs@example.com");

        var languages = (await user.GetAsync<List<LanguageRow>>("/api/localization/languages")).Data!;

        languages.Should().Contain(l => l.Code == "en" && !l.Rtl && l.IsDefault);
        languages.Should().Contain(l => l.Code == "ar" && l.Rtl && !l.IsDefault);
        (await factory.Anonymous().GetAsync<List<LanguageRow>>("/api/localization/languages")).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_resources_endpoint_is_open_and_returns_the_translations_for_one_culture()
    {
        var anonymous = factory.Anonymous();

        var arabic = (await anonymous.GetAsync<ResourcesRow>("/api/localization/resources?culture=ar")).Data!;
        arabic.Culture.Should().Be("ar");
        arabic.Messages.Should().ContainKey("Job not found.").WhoseValue.Should().Be("المهمة غير موجودة.");

        (await anonymous.GetAsync<ResourcesRow>("/api/localization/resources?culture=en")).Data!.Messages.Should().BeEmpty();
        (await anonymous.GetAsync<ResourcesRow>("/api/localization/resources?culture=fr")).Data!.Messages.Should().BeEmpty();
        (await anonymous.GetAsync<ResourcesRow>("/api/localization/resources")).Status.Should().Be(HttpStatusCode.OK);
    }
}
