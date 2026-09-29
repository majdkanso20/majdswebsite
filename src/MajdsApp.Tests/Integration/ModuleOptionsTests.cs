using FluentAssertions;
using MajdsApp.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
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
    public void Choosing_AzureBlob_storage_without_a_connection_string_stops_the_application_from_starting()
    {
        // Cache:Redis:ConnectionString works the same way: only checked when its provider is the one actually selected.
        ShouldRefuseToStart(new BadConfigFactory(("Files:Storage:Provider", "AzureBlob")), "Files:Storage:AzureBlob:ConnectionString");
    }

    [Fact]
    public void Choosing_AzureBlob_storage_with_no_container_name_stops_the_application_from_starting()
    {
        ShouldRefuseToStart(new BadConfigFactory(
            ("Files:Storage:Provider", "AzureBlob"), ("Files:Storage:AzureBlob:ConnectionString", "UseDevelopmentStorage=true"), ("Files:Storage:AzureBlob:ContainerName", "")),
            "Files:Storage:AzureBlob:ContainerName");
    }

    [Fact]
    public void AzureBlob_storage_is_chosen_by_configuration_and_needs_no_live_account_to_start()
    {
        // No connection is opened until the first file is actually read or written, so this proves the wiring
        // without a real Azure account (the same approach the Redis cache test uses).
        using var factory = new BadConfigFactory(
            ("Files:Storage:Provider", "AzureBlob"), ("Files:Storage:AzureBlob:ConnectionString", "UseDevelopmentStorage=true"));

        factory.Invoking(f => f.CreateClient()).Should().NotThrow();
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<MajdsApp.SharedKernel.Files.IFileStorage>().Should().BeOfType<MajdsApp.Modules.Files.AzureBlobFileStorage>();
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
