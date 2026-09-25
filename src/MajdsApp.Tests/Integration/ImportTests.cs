using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

public record ImportErrorRow(int Row, string Reason);
public record ImportSummary(int Total, int Succeeded, int Failed, List<ImportErrorRow> Errors);

/// <summary>F-Export import: per-row validation with a summary, and downloadable templates.</summary>
public class ImportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<(HttpStatusCode Status, Envelope<ImportSummary>? Body)> UploadAsync(ApiClient client, string fileName, byte[] content)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);

        var response = await client.Http.PostAsync("/api/users/import", form);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.StartsWith('{') ? JsonSerializer.Deserialize<Envelope<ImportSummary>>(text, Json) : null); // a 401 has no body
    }

    private static Task<(HttpStatusCode Status, Envelope<ImportSummary>? Body)> UploadCsvAsync(ApiClient client, string csv) =>
        UploadAsync(client, "users.csv", Encoding.UTF8.GetBytes(csv));

    private static async Task<List<string>> EmailsAsync(ApiClient admin, string filter) =>
        (await admin.GetAsync<PagedData<UserRow>>($"/api/users/list?page=1&pageSize=100&filter={filter}")).Data!.Items.Select(u => u.Email).ToList();

    [Fact]
    public async Task Valid_rows_are_imported_and_invalid_rows_are_reported_with_their_row_and_reason()
    {
        var admin = await factory.SignInAsync("imp.admin1@example.com", "Admin");
        var csv = string.Join("\r\n",
            "Email,Full name,Roles,Password",
            "imp.good1@example.com,Good One,User,Passw0rd!x",   // row 2: fine
            "not-an-email,Bad Email,,Passw0rd!x",                // row 3: invalid email
            "imp.good2@example.com,Good Two,Nonexistent,Passw0rd!x", // row 4: unknown role
            "imp.good3@example.com,Good Three,,abc",             // row 5: password too short
            "imp.good4@example.com,Good Four,\"Admin; User\",",  // row 6: fine, generated password, two roles
            "imp.good1@example.com,Duplicate,,Passw0rd!x") + "\r\n"; // row 7: already imported above

        var (status, body) = await UploadCsvAsync(admin, csv);

        status.Should().Be(HttpStatusCode.OK);
        var result = body!.Data!;
        result.Total.Should().Be(6);
        result.Succeeded.Should().Be(2);
        result.Failed.Should().Be(4);
        result.Errors.Select(e => e.Row).Should().Equal(3, 4, 5, 7);
        result.Errors.Single(e => e.Row == 4).Reason.Should().Contain("Unknown role 'Nonexistent'");
        result.Errors.Single(e => e.Row == 5).Reason.Should().Contain("6");                       // the create rule's minimum length
        result.Errors.Single(e => e.Row == 7).Reason.Should().Contain("imp.good1@example.com");   // already taken

        var created = await EmailsAsync(admin, "imp.good");
        created.Should().BeEquivalentTo("imp.good1@example.com", "imp.good4@example.com");        // valid rows in, invalid rows out (AC-EXP-2)
        (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=10&filter=imp.good4")).Data!.Items.Single()
            .Roles.Should().BeEquivalentTo("Admin", "User");
    }

    [Fact]
    public async Task An_Excel_file_with_columns_in_any_order_imports_too()
    {
        var admin = await factory.SignInAsync("imp.admin2@example.com", "Admin");
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Data");
        sheet.Cell(1, 1).Value = "Full name";
        sheet.Cell(1, 2).Value = "Email";
        sheet.Cell(2, 1).Value = "Excel Person";
        sheet.Cell(2, 2).Value = "imp.excel@example.com";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var (status, body) = await UploadAsync(admin, "users.xlsx", stream.ToArray());

        status.Should().Be(HttpStatusCode.OK);
        body!.Data!.Succeeded.Should().Be(1);
        (await EmailsAsync(admin, "imp.excel")).Should().Equal("imp.excel@example.com");
    }

    [Fact]
    public async Task A_user_imported_without_a_password_exists_but_cannot_sign_in_with_a_guessed_one()
    {
        var admin = await factory.SignInAsync("imp.admin3@example.com", "Admin");
        await UploadCsvAsync(admin, "Email\r\nimp.nopass@example.com\r\n");

        var response = await factory.Anonymous().Http.PostAsJsonAsync("/api/identity/login", new { email = "imp.nopass@example.com", password = "Test1234!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await EmailsAsync(admin, "imp.nopass")).Should().Contain("imp.nopass@example.com");
    }

    [Fact]
    public async Task A_file_the_importer_cannot_use_is_rejected_before_any_row_is_imported()
    {
        var admin = await factory.SignInAsync("imp.admin4@example.com", "Admin");

        var noEmailColumn = await UploadCsvAsync(admin, "Name\r\nAda\r\n");
        noEmailColumn.Status.Should().Be(HttpStatusCode.BadRequest);
        noEmailColumn.Body!.Errors.Should().Contain(e => e.Contains("Email"));

        (await UploadAsync(admin, "users.txt", Encoding.UTF8.GetBytes("Email\na@b.co"))).Status.Should().Be(HttpStatusCode.BadRequest);
        (await UploadAsync(admin, "users.csv", [])).Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Importing_needs_the_create_permission()
    {
        var plain = await factory.SignInAsync("imp.plain@example.com");

        (await UploadCsvAsync(plain, "Email\r\nimp.nope@example.com\r\n")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await UploadCsvAsync(factory.Anonymous(), "Email\r\na@b.co\r\n")).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_templates_carry_the_expected_columns_and_no_data_rows()
    {
        var admin = await factory.SignInAsync("imp.admin5@example.com", "Admin");

        var csv = await admin.Http.GetAsync("/api/users/import-template");
        csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync()).Should().Contain("Email,Full name,Roles,Password");

        var xlsx = await admin.Http.GetAsync("/api/users/import-template?format=xlsx");
        using var workbook = new XLWorkbook(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync()));
        workbook.Worksheet("Data").RowsUsed().Should().ContainSingle();                            // headers only: nothing to import by accident
        workbook.Worksheet("Data").Row(1).Cells(1, 4).Select(c => c.GetString()).Should().Equal("Email", "Full name", "Roles", "Password");
        workbook.Worksheet("Instructions").Cell(2, 2).GetString().Should().Be("Yes");              // Email is required

        (await admin.Http.GetAsync("/api/users/import-template?format=pdf")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_downloaded_template_filled_in_imports_cleanly()
    {
        var admin = await factory.SignInAsync("imp.admin6@example.com", "Admin");
        var template = Encoding.UTF8.GetString(await (await admin.Http.GetAsync("/api/users/import-template")).Content.ReadAsByteArrayAsync()).TrimStart('﻿');

        var (_, body) = await UploadCsvAsync(admin, template + "imp.template@example.com,Template User,User,Passw0rd!x\r\n");

        body!.Data!.Succeeded.Should().Be(1);
        body.Data.Failed.Should().Be(0);
    }
}
