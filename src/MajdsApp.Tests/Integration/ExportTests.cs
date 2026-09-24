using System.Net;
using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>F-Export: CSV, Excel and PDF for the users list and the audit log, honoring the list's filters.</summary>
public class ExportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<(HttpResponseMessage Response, byte[] Bytes)> DownloadAsync(ApiClient client, string url)
    {
        var response = await client.Http.GetAsync(url);
        return (response, await response.Content.ReadAsByteArrayAsync());
    }

    private static List<string> Column(byte[] xlsx, int column)
    {
        using var workbook = new XLWorkbook(new MemoryStream(xlsx));
        var sheet = workbook.Worksheet(1);
        return sheet.RowsUsed().Skip(1).Select(r => r.Cell(column).GetString()).ToList();
    }

    [Fact]
    public async Task Users_export_as_Excel_contains_the_headers_and_rows()
    {
        var admin = await factory.SignInAsync("exp.admin@example.com", "Admin");
        await factory.CreateUserAsync("exp.one@example.com");

        var (response, bytes) = await DownloadAsync(admin, "/api/users/export?format=xlsx");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("users.xlsx");
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        workbook.Worksheet(1).Row(1).Cells(1, 6).Select(c => c.GetString())
            .Should().Equal("Email", "Full name", "Phone number", "Roles", "Active", "Created (UTC)");
        Column(bytes, 1).Should().Contain(["exp.admin@example.com", "exp.one@example.com"]);
    }

    [Fact]
    public async Task Users_export_as_PDF_is_a_real_PDF_document()
    {
        var admin = await factory.SignInAsync("exp.pdf@example.com", "Admin");

        var (response, bytes) = await DownloadAsync(admin, "/api/users/export?format=pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        bytes.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public async Task Users_export_as_CSV_is_still_the_default()
    {
        var admin = await factory.SignInAsync("exp.csv@example.com", "Admin");

        var (response, bytes) = await DownloadAsync(admin, "/api/users/export");

        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        Encoding.UTF8.GetString(bytes).Should().Contain("Email,Full name").And.Contain("exp.csv@example.com");
    }

    [Fact]
    public async Task Only_rows_matching_the_active_filters_are_exported()
    {
        var admin = await factory.SignInAsync("filter.admin@example.com", "Admin");
        await factory.CreateUserAsync("filter.match@example.com");
        await factory.CreateUserAsync("filter.inactive@example.com", active: false);

        var text = Column((await DownloadAsync(admin, "/api/users/export?format=xlsx&filter=filter.match")).Bytes, 1);
        text.Should().Equal("filter.match@example.com");

        var inactive = Column((await DownloadAsync(admin, "/api/users/export?format=xlsx&isActive=false")).Bytes, 1);
        inactive.Should().Contain("filter.inactive@example.com").And.NotContain("filter.match@example.com");

        var admins = Column((await DownloadAsync(admin, "/api/users/export?format=xlsx&role=Admin")).Bytes, 1);
        admins.Should().Contain("filter.admin@example.com").And.NotContain("filter.match@example.com");
    }

    [Fact]
    public async Task An_unknown_format_is_rejected_not_silently_replaced()
    {
        var admin = await factory.SignInAsync("exp.bad@example.com", "Admin");

        var (response, bytes) = await DownloadAsync(admin, "/api/users/export?format=docx");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Encoding.UTF8.GetString(bytes).Should().Contain("not a supported export format");
    }

    [Fact]
    public async Task Exporting_needs_the_same_permission_as_viewing()
    {
        var plain = await factory.SignInAsync("exp.plain@example.com");

        (await DownloadAsync(plain, "/api/users/export?format=xlsx")).Response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await DownloadAsync(plain, "/api/audit/export?format=pdf")).Response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await DownloadAsync(factory.Anonymous(), "/api/users/export")).Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_audit_log_exports_in_every_format_and_honors_the_outcome_filter()
    {
        var admin = await factory.SignInAsync("exp.audit@example.com", "Admin");
        var plain = await factory.SignInAsync("exp.audit.plain@example.com");
        await plain.GetAsync<object>("/api/users/list?page=1&pageSize=1"); // a refused call leaves a Forbidden audit entry

        var xlsx = await DownloadAsync(admin, "/api/audit/export?format=xlsx&outcome=Forbidden");
        Column(xlsx.Bytes, 4).Should().NotBeEmpty().And.OnlyContain(outcome => outcome == "Forbidden");

        Encoding.ASCII.GetString((await DownloadAsync(admin, "/api/audit/export?format=pdf")).Bytes, 0, 5).Should().Be("%PDF-");
        Encoding.UTF8.GetString((await DownloadAsync(admin, "/api/audit/export")).Bytes).Should().Contain("When (UTC),Action");
    }
}
