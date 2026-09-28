using System.Net;
using FluentAssertions;
using MajdsApp.Modules.Localization;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>F-Localization FR-I18N-001: the texts ASP.NET Identity writes (password rules, taken names) are translated too, several at a time.</summary>
public class IdentityMessageTranslationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void Several_problems_joined_in_one_message_are_translated_one_by_one()
    {
        var catalog = new MessageCatalog();

        var arabic = catalog.Translate("Passwords must be at least 8 characters.; Passwords must have at least one digit ('0'-'9').", "ar");

        arabic.Should().Be("يجب ألا تقل كلمة المرور عن 8 حرفاً.; يجب أن تحتوي كلمة المرور على رقم واحد على الأقل ('0' إلى '9').");
        catalog.Translate("Username 'a@b.c' is already taken.; Email 'a@b.c' is already taken.", "ar").Should().Contain("مستخدم بالفعل").And.NotContain("already taken");
        catalog.Translate("Something nobody translated; and another", "ar").Should().Be("Something nobody translated; and another");
    }

    [Fact]
    public async Task A_weak_password_is_refused_in_the_language_of_the_request()
    {
        var admin = await factory.SignInAsync("idm.admin@example.com", "Admin");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/create")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { email = "idm.weak@example.com", password = "abcdefg", roles = Array.Empty<string>() })
        };
        request.Headers.AcceptLanguage.ParseAdd("ar");

        var response = await admin.Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().MatchRegex("[؀-ۿ]").And.NotContain("Passwords must");
    }

    [Fact]
    public async Task A_model_binding_error_is_translated_too_not_just_the_validation_summary()
    {
        var admin = await factory.SignInAsync("idm.admin2@example.com", "Admin");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/list?page=not-a-number");
        request.Headers.AcceptLanguage.ParseAdd("ar");

        var response = await admin.Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // ASP.NET's own "value is not valid" message, not just the "Validation failed." summary, comes back in Arabic (F-Localization FR-I18N-001).
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().MatchRegex("[؀-ۿ]").And.NotContain("is not valid for");
    }
}
