using System.IO.Compression;
using System.Security.Cryptography;
using FluentAssertions;
using FluentValidation;
using MajdsApp.SharedKernel.Plugins;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MajdsApp.Tests.Plugins;

/// <summary>P5 FR-PLUG-036: packages can be signed by a publisher, and the platform verifies the signature against the keys it trusts.</summary>
[Collection(PluginHostCollection.Name)] // stages the sample plugin, whose assembly is briefly loaded to check it
public class PluginSignatureTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "majds-plugin-sig-" + Guid.NewGuid().ToString("N"));
    private readonly ECDsa _publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public PluginSignatureTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        _publisher.Dispose();
        _stranger.Dispose();
        try { Directory.Delete(_folder, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static string PublicKey(ECDsa key) => Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

    private PluginHostOptions Options(bool requireSignature = false) =>
        new(_folder, false, [], requireSignature, new Dictionary<string, string> { ["acme"] = PublicKey(_publisher) });

    private StagedPlugin Stage(byte[] package, PluginHostOptions options) => PluginInstaller.Stage(new MemoryStream(package), null, options);

    [Fact]
    public void A_package_signed_by_a_trusted_publisher_is_accepted_and_says_who_signed_it()
    {
        var signed = PluginSignature.Sign(PluginPackages.Create(), _publisher, "acme");

        var staged = Stage(signed, Options(requireSignature: true));

        staged.SignedBy.Should().Be("acme");
        File.Exists(Path.Combine(_folder, ".pending", staged.Manifest.Id, PluginSignature.FileName)).Should().BeFalse("the signature is checked, not installed");
    }

    [Fact]
    public void An_unsigned_package_is_accepted_unless_signatures_are_required()
    {
        Stage(PluginPackages.Create(), Options()).SignedBy.Should().BeNull();
    }

    [Fact]
    public void When_signatures_are_required_an_unsigned_package_is_refused()
    {
        var act = () => Stage(PluginPackages.Create(), Options(requireSignature: true));

        act.Should().Throw<ValidationException>().WithMessage("*only installs signed plugins*");
        Directory.Exists(Path.Combine(_folder, ".pending")).Should().BeFalse();
    }

    [Fact]
    public void A_package_changed_after_it_was_signed_is_refused_even_when_signatures_are_optional()
    {
        var signed = PluginSignature.Sign(PluginPackages.Create(), _publisher, "acme");
        var tampered = new MemoryStream();
        using (var input = new ZipArchive(new MemoryStream(signed), ZipArchiveMode.Read))
        using (var output = new ZipArchive(tampered, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                using var from = entry.Open();
                using var to = output.CreateEntry(entry.FullName).Open();
                from.CopyTo(to);
            }
            PluginPackages.Add(output, "backend/extra.json", "{}"u8.ToArray());
        }

        var act = () => Stage(tampered.ToArray(), Options());

        act.Should().Throw<ValidationException>().WithMessage("*does not match its contents*");
    }

    [Fact]
    public void A_signature_from_a_key_the_platform_does_not_trust_is_refused()
    {
        var signedByStranger = PluginSignature.Sign(PluginPackages.Create(), _stranger, "mallory");

        var act = () => Stage(signedByStranger, Options());

        act.Should().Throw<ValidationException>().WithMessage("*'mallory'*not a trusted publisher*");
    }

    [Fact]
    public void A_signature_made_with_another_key_under_a_trusted_name_is_refused()
    {
        var forged = PluginSignature.Sign(PluginPackages.Create(), _stranger, "acme");

        var act = () => Stage(forged, Options());

        act.Should().Throw<ValidationException>().WithMessage("*does not match its contents*");
    }

    [Fact]
    public void A_malformed_signature_file_is_refused()
    {
        var broken = PluginPackages.Create(extra: zip => PluginPackages.Add(zip, PluginSignature.FileName, "not json"u8.ToArray()));

        var act = () => Stage(broken, Options());

        act.Should().Throw<ValidationException>().WithMessage("*signature file is not valid*");
    }

    [Fact]
    public void The_trusted_publishers_are_read_from_configuration()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Plugins:Trust:RequireSignature"] = "true",
            ["Plugins:Trust:Signers:acme"] = PublicKey(_publisher)
        }).Build();

        var options = PluginHostOptions.FromConfiguration(config, _folder);

        options.RequireSignature.Should().BeTrue();
        options.TrustedSigners.Should().ContainKey("acme").WhoseValue.Should().Be(PublicKey(_publisher));
    }
}
