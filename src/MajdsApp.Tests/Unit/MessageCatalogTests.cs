using System.Text.RegularExpressions;
using FluentAssertions;
using MajdsApp.Modules.Localization;
using Xunit;

namespace MajdsApp.Tests.Unit;

public partial class MessageCatalogTests
{
    private static readonly MessageCatalog Catalog = new();

    [Fact]
    public void A_message_with_a_translation_is_translated()
    {
        Catalog.Translate("Job not found.", "ar").Should().Be("المهمة غير موجودة.");
    }

    [Fact]
    public void A_template_matches_any_value_and_the_value_is_carried_into_the_translation()
    {
        Catalog.Translate("Missing permission: Users.View", "ar").Should().Be("الصلاحية مفقودة: Users.View");
        Catalog.Translate("A role named 'Auditors' already exists.", "ar").Should().Be("يوجد دور باسم 'Auditors' بالفعل.");
        Catalog.Translate("The file has 9,000 rows; the limit is 5,000. Split it into smaller files.", "ar")
            .Should().Contain("9,000").And.Contain("5,000");
    }

    [Fact]
    public void Templates_with_several_values_keep_each_value_in_its_place()
    {
        Catalog.Translate("This package is not on the approved list (id Acme.X, SHA-256 abc123). Ask an administrator to approve it in Plugins:Trust.", "ar")
            .Should().Contain("Acme.X").And.Contain("abc123");
        Catalog.Translate("The job 'send-report' failed after 3 attempts: smtp down", "ar")
            .Should().Contain("send-report").And.Contain("3").And.Contain("smtp down");
    }

    [Theory]
    [InlineData("ar")]
    [InlineData("AR")]
    [InlineData("ar-JO")]
    [InlineData("ar_SA")]
    public void Regional_and_differently_cased_culture_names_mean_the_same_language(string culture) =>
        Catalog.Translate("Job not found.", culture).Should().Be("المهمة غير موجودة.");

    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData(null)]
    public void English_and_unsupported_languages_get_the_text_unchanged(string? culture) =>
        Catalog.Translate("Job not found.", culture!).Should().Be("Job not found.");

    [Fact]
    public void Text_with_no_translation_is_never_blanked_or_altered()
    {
        Catalog.Translate("Something nobody translated.", "ar").Should().Be("Something nobody translated.");
        Catalog.Translate("", "ar").Should().Be("");
    }

    [Fact]
    public void A_specific_template_is_not_swallowed_by_a_more_general_one()
    {
        // Both start "Missing permission": each must land on its own translation.
        Catalog.Translate("Missing permission 'Files.View'.", "ar").Should().Be("الصلاحية 'Files.View' مفقودة.");
        Catalog.Translate("Missing permission: Files.View", "ar").Should().Be("الصلاحية مفقودة: Files.View");
    }

    [Fact]
    public void The_resources_are_available_per_culture_and_empty_for_english()
    {
        Catalog.GetResources("ar").Should().ContainKey("Job not found.");
        Catalog.GetResources("ar-JO").Should().ContainKey("Job not found.");
        Catalog.GetResources("en").Should().BeEmpty();
        Catalog.GetResources("fr").Should().BeEmpty();
    }

    [Fact]
    public void Every_language_on_offer_is_described_with_its_direction()
    {
        Catalog.Languages.Should().Contain(l => l.Code == "en" && !l.Rtl);
        Catalog.Languages.Should().Contain(l => l.Code == "ar" && l.Rtl);
        Catalog.Languages[0].Code.Should().Be("en"); // the default comes first
    }

    [Fact]
    public void Every_placeholder_in_a_source_text_appears_in_its_translation()
    {
        foreach (var (english, arabic) in Catalog.GetResources("ar"))
        {
            var expected = Placeholder().Matches(english).Select(m => m.Value).Distinct().OrderBy(v => v);
            var actual = Placeholder().Matches(arabic).Select(m => m.Value).Distinct().OrderBy(v => v);
            actual.Should().Equal(expected, $"the translation of \"{english}\" must use the same placeholders");
        }
    }

    // ---- nothing the server says can be missing from the catalog -------------------------------------------------------

    [Fact]
    public void Every_message_the_server_writes_has_an_Arabic_translation()
    {
        var known = Catalog.GetResources("ar").Keys.ToHashSet();
        var missing = new SortedSet<string>();

        foreach (var file in SourceFiles())
        {
            var source = File.ReadAllText(file);

            foreach (Match m in ExceptionMessage().Matches(source)) Check(m.Groups[1].Value, m.Groups[2].Value);
            foreach (Match m in ValidatorMessage().Matches(source)) Check(m.Groups[1].Value, m.Groups[2].Value);
            foreach (Match m in EmailText().Matches(source)) Check("", m.Groups[1].Value);

            foreach (Match call in NotificationCall().Matches(source))
                foreach (Match literal in StringLiteral().Matches(call.Value))
                {
                    var text = literal.Groups[2].Value;
                    if (text is "Admin" or "General" || text.Length == 0 || text.StartsWith('/')) continue;   // a role name, a type and an in-app route: not text for people
                    Check(literal.Groups[1].Value, text);
                }
        }

        missing.Should().BeEmpty("every server message needs an entry in MajdsApp.Modules.Localization/Resources/ar.json; add these:\n" + string.Join("\n", missing));

        void Check(string interpolated, string raw)
        {
            var key = Normalize(interpolated, raw);
            if (key.Length > 0 && !known.Contains(key)) missing.Add(key);
        }
    }

    private static string Normalize(string interpolated, string raw)
    {
        var text = raw.Replace("\\\"", "\"").Replace("\\'", "'");
        if (interpolated != "$") return text;
        var i = 0;
        return Interpolation().Replace(text, _ => "{" + i++ + "}");
    }

    private static IEnumerable<string> SourceFiles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MajdsApp.slnx"))) dir = dir.Parent;
        var src = Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root not found."), "src");

        // The platform's projects only: not the tests, not the separate Razor site, not build output.
        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(f =>
        {
            var relative = Path.GetRelativePath(src, f).Replace('\\', '/');
            var top = relative.Split('/')[0];
            return !relative.Contains("/bin/") && !relative.Contains("/obj/")
                && top != "MajdsApp" && top != "MajdsApp.Tests" && top != "MajdsApp.Tests.Plugins" && top != "majds-app-web";
        });
    }

    [GeneratedRegex("""new\s+(?:[\w.]*\.)?(?:Validation|NotFound|Forbidden|Conflict|UnauthorizedApp)Exception\(\s*(\$?)"((?:[^"\\]|\\.)*)"(?=\s*[,)])""", RegexOptions.Singleline)]
    private static partial Regex ExceptionMessage();

    [GeneratedRegex(@"\.WithMessage\(\s*(\$?)""((?:[^""\\]|\\.)*)""", RegexOptions.Singleline)]
    private static partial Regex ValidatorMessage();

    [GeneratedRegex("""\bt\.T\("((?:[^"\\]|\\.)*)"\)""")]
    private static partial Regex EmailText();

    [GeneratedRegex("""Publish(?:ToRole|ToAll)?Async\([^;]*;""", RegexOptions.Singleline)]
    private static partial Regex NotificationCall();

    [GeneratedRegex(@"(\$?)""((?:[^""\\]|\\.)*)""")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"\{[^{}]+\}")]
    private static partial Regex Interpolation();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();
}
