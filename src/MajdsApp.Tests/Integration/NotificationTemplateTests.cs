using FluentAssertions;
using MajdsApp.Data;
using MajdsApp.Modules.Localization;
using MajdsApp.Modules.Notifications;
using MajdsApp.SharedKernel.Localization;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>F-Notifications FR-NOTIF-003: notification content comes from templates per channel and type, in the recipient's language.</summary>
public class NotificationTemplateTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static NotificationTemplateRenderer Renderer(params INotificationTemplate[] templates) =>
        new(new MessageCatalog(), templates);

    private sealed class ReportTemplate : INotificationTemplate
    {
        public string Type => "Report";
        public NotificationChannel Channel => NotificationChannel.Email;
        public RenderedNotification Render(NotificationContent content, string culture, IMessageCatalog catalog) =>
            new("Report: " + content.Title, content.Message, "<b>custom</b>");
    }

    [Fact]
    public void In_app_content_is_plain_text_in_the_recipients_language()
    {
        var english = Renderer().Render(NotificationChannel.InApp, "General", new("Your access changed", "An administrator updated your roles."), "en");
        var arabic = Renderer().Render(NotificationChannel.InApp, "General", new("Your access changed", "An administrator updated your roles."), "ar");

        english.Should().Be(new RenderedNotification("Your access changed", "An administrator updated your roles."));
        arabic.Title.Should().Be("تغيّرت صلاحيات وصولك");
        arabic.HtmlBody.Should().BeNull();
    }

    [Fact]
    public void Email_gets_a_layout_whose_direction_follows_the_language_and_whose_text_is_encoded()
    {
        var english = Renderer().Render(NotificationChannel.Email, "General", new("Hello <b>", "x & y"), "en");
        var arabic = Renderer().Render(NotificationChannel.Email, "General", new("Your access changed", "An administrator updated your roles."), "ar-JO");

        english.HtmlBody.Should().Contain("dir=\"ltr\"").And.Contain("Hello &lt;b&gt;").And.Contain("x &amp; y").And.NotContain("<b>");
        arabic.HtmlBody.Should().Contain("dir=\"rtl\"").And.Contain("تغيّرت صلاحيات وصولك");
    }

    [Fact]
    public void A_template_for_a_type_and_channel_is_used_for_exactly_that_and_nothing_else()
    {
        var renderer = Renderer(new ReportTemplate());
        var content = new NotificationContent("Monthly", "Ready");

        renderer.Render(NotificationChannel.Email, "Report", content, "en").Should().Be(new RenderedNotification("Report: Monthly", "Ready", "<b>custom</b>"));
        renderer.Render(NotificationChannel.InApp, "Report", content, "en").Title.Should().Be("Monthly");          // another channel: the default
        renderer.Render(NotificationChannel.Email, "General", content, "en").HtmlBody.Should().Contain("<h2>Monthly</h2>"); // another type: the default
    }

    [Fact]
    public void Security_email_adds_a_translated_what_to_do_line()
    {
        var renderer = Renderer(new SecurityEmailTemplate());
        var content = new NotificationContent("Your access changed", "An administrator updated your roles.");

        renderer.Render(NotificationChannel.Email, NotificationTypes.Security, content, "en").HtmlBody.Should().Contain("If this was not you, contact your administrator at once.");
        renderer.Render(NotificationChannel.Email, NotificationTypes.Security, content, "ar").HtmlBody.Should().Contain("إذا لم تكن أنت");
        renderer.Render(NotificationChannel.Email, NotificationTypes.Account, content, "en").HtmlBody.Should().NotContain("not you");
    }

    [Fact]
    public async Task A_queued_email_carries_the_body_rendered_for_its_recipient()
    {
        var admin = await factory.SignInAsync("tpl.admin@example.com", "Admin");
        await factory.SignInAsync("tpl.ar@example.com");
        var recipient = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5&filter=tpl.ar@example.com")).Data!.Items.Single();
        var arabicReader = await factory.SignInAsync("tpl.ar@example.com");
        await arabicReader.PostAsync("/api/settings/update-mine", new { items = new[] { new { name = "General.DefaultLanguage", value = "ar" } } });

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IUserNotificationPublisher>()
                .PublishAsync(recipient.Id, "Your access changed", "An administrator updated your roles.", NotificationTypes.Security);

        using var check = factory.Services.CreateScope();
        var delivery = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<NotificationDelivery>()
            .Where(d => d.UserId == recipient.Id && d.Channel == NotificationChannel.Email).OrderByDescending(d => d.Id).FirstAsync();
        delivery.Title.Should().Be("تغيّرت صلاحيات وصولك");
        delivery.Body.Should().Contain("dir=\"rtl\"").And.Contain("إذا لم تكن أنت");
    }
}
