using FluentAssertions;
using MajdsApp.Tests.Support;
using Microsoft.Extensions.Options;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>P2 FR-MOD-004: each module's configuration is bound from its own section and checked when the application starts.</summary>
public class ModuleOptionsTests
{
    private sealed class BadConfigFactory(params (string Key, string Value)[] settings)
        : ApiFactory(settings.ToDictionary(s => s.Key, s => s.Value), null);

    private static void ShouldRefuseToStart(BadConfigFactory factory, params string[] mentions)
    {
        using (factory)
        {
            var start = () => factory.CreateClient();

            // Either the options validator (a value out of range) or the binder (a value that is not even the right kind, such as "maybe" for a true/false setting) stops the start.
            var failure = start.Should().Throw<Exception>().Which;
            var text = failure.ToString();
            foreach (var mention in mentions) text.Should().Contain(mention);
        }
    }

    [Theory]
    [InlineData("RateLimiting:AuthPermitLimit", "0", "RateLimiting:AuthPermitLimit")]
    [InlineData("RateLimiting:WindowSeconds", "-5", "RateLimiting:WindowSeconds")]
    [InlineData("Email:Smtp:Port", "70000", "Email:Smtp:Port")]
    [InlineData("Email:Smtp:FromEmail", "not-an-address", "Email:Smtp:FromEmail")]
    [InlineData("Docs:Access", "Everybody", "Docs:Access")]
    [InlineData("Jobs:Worker:Enabled", "nope", "Jobs")]
    [InlineData("Metrics:Enabled", "maybe", "Metrics")]
    [InlineData("Plugins:Trust:RequireSignature", "sometimes", "Plugins:Trust")]
    [InlineData("Plugins:Trust:Allowed:0:Sha256", "not-a-checksum", "Plugins:Trust:Allowed:0:Sha256")]
    [InlineData("Plugins:Trust:Signers:acme", "not a key", "Plugins:Trust:Signers:acme")]
    public void A_wrong_value_stops_the_application_from_starting_and_names_the_setting(string key, string value, string mention)
    {
        ShouldRefuseToStart(new BadConfigFactory((key, value)), mention);
    }

    [Fact]
    public void The_default_configuration_starts()
    {
        using var factory = new ApiFactory();

        factory.Invoking(f => f.CreateClient()).Should().NotThrow();
    }

    [Fact]
    public void A_correct_value_of_the_same_kind_starts()
    {
        using var factory = new BadConfigFactory(("RateLimiting:WindowSeconds", "30"), ("Email:Smtp:Port", "2525"), ("Docs:Access", "open"),
            ("Jobs:Worker:Enabled", "false"), ("Metrics:Enabled", "true"), ("Plugins:Trust:RequireAllowList", "false"));

        factory.Invoking(f => f.CreateClient()).Should().NotThrow();
    }
}
