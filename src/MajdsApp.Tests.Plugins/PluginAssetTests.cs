using System.Net;
using FluentAssertions;
using MajdsApp.SharedKernel.Plugins;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Plugins;

/// <summary>P5 FR-PLUG-016: a plugin's pre-built frontend files are served under a namespaced path, for an enabled plugin only.</summary>
[Collection(PluginHostCollection.Name)]
public class PluginAssetTests(PluginHostFactory factory)
{
    private const string Id = "MajdsApp.Plugins.Tasks";

    [Fact]
    public async Task A_file_is_served_with_its_content_type_and_revalidation_headers_without_signing_in()
    {
        var response = await factory.Anonymous().Http.GetAsync($"/plugins/{Id}/main.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/javascript");
        response.Headers.CacheControl!.NoCache.Should().BeTrue();
        response.Headers.ETag.Should().NotBeNull();
        (await response.Content.ReadAsStringAsync()).Should().Contain("MajdsApp.Plugins.Tasks");
    }

    [Fact]
    public async Task A_second_request_with_the_etag_is_answered_not_modified()
    {
        var http = factory.Anonymous().Http;
        var etag = (await http.GetAsync($"/plugins/{Id}/styles.css")).Headers.ETag!;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/plugins/{Id}/styles.css");
        request.Headers.IfNoneMatch.Add(etag);

        (await http.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task A_nested_file_is_served_under_the_hosts_strict_content_security_policy()
    {
        var response = await factory.Anonymous().Http.GetAsync($"/plugins/{Id}/assets/icon.svg");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().StartWith("default-src 'none'"); // an image or page opened directly can run and load nothing
    }

    [Theory]
    [InlineData("/plugins/MajdsApp.Plugins.Tasks/missing.js")]
    [InlineData("/plugins/Nobody.Here/main.js")]
    [InlineData("/plugins/MajdsApp.Plugins.Tasks/../plugin.json")]
    [InlineData("/plugins/MajdsApp.Plugins.Tasks/%2e%2e/plugin.json")]
    [InlineData("/plugins/MajdsApp.Plugins.Tasks/..%2fplugin.json")]
    [InlineData("/plugins/MajdsApp.Plugins.Tasks/%2e%2e%2f%2e%2e%2fappsettings.json")]
    [InlineData("/plugins/MajdsApp.Plugins.Tasks/main.dll")]
    [InlineData("/plugins/MajdsApp.Plugins.Tasks/")]
    public async Task Anything_outside_the_frontend_folder_or_of_another_kind_is_not_found(string url)
    {
        var response = await factory.Anonymous().Http.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_disabled_plugin_serves_nothing_and_serves_again_when_enabled()
    {
        var admin = await factory.SignInAsync("pa.admin@example.com", "Admin");
        var http = factory.Anonymous().Http;
        try
        {
            await admin.PostAsync("/api/plugins/set-enabled", new { pluginId = Id, enabled = false });
            (await http.GetAsync($"/plugins/{Id}/main.js")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await admin.PostAsync("/api/plugins/set-enabled", new { pluginId = Id, enabled = true });
        }

        (await http.GetAsync($"/plugins/{Id}/main.js")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void Resolve_refuses_a_path_that_climbs_out_of_the_frontend_folder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "majds-plugin-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "frontend"));
        File.WriteAllText(Path.Combine(folder, "frontend", "ok.js"), "1");
        File.WriteAllText(Path.Combine(folder, "secret.json"), "{}");
        var plugin = new LoadedPlugin(new PluginManifest("p", "p", "1.0.0", "t", "T", []), null, null, null, folder);

        PluginAssets.Resolve(plugin, "ok.js").Should().NotBeNull();
        PluginAssets.Resolve(plugin, "../secret.json").Should().BeNull();
        PluginAssets.Resolve(plugin, @"..\secret.json").Should().BeNull();
        PluginAssets.Resolve(plugin, "sub/../../secret.json").Should().BeNull();
        PluginAssets.Resolve(plugin, "C:/Windows/win.ini").Should().BeNull();
    }

    [Fact]
    public void A_package_may_carry_frontend_files_of_the_served_kinds_and_nothing_else_there()
    {
        var folder = Path.Combine(Path.GetTempPath(), "majds-plugin-assets-pkg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var options = new PluginHostOptions(folder, false, []);
            var withFrontend = PluginPackages.Create(extra: zip => PluginPackages.Add(zip, "frontend/extra/app.js", "1"u8.ToArray()));
            PluginInstaller.Stage(new MemoryStream(withFrontend), null, options).Manifest.Id.Should().Be(Id);

            var withExe = PluginPackages.Create(extra: zip => PluginPackages.Add(zip, "frontend/run.exe", "1"u8.ToArray()));
            var refused = () => PluginInstaller.Stage(new MemoryStream(withExe), null, new PluginHostOptions(Path.Combine(folder, "second"), false, []));
            refused.Should().Throw<FluentValidation.ValidationException>().WithMessage("*not allowed*");
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
