using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

public record ImportErrorRow(int Row, string Reason);
public record ImportJobRow(Guid Id, string Source, string Title, string FileName, string Status, int Total, int Succeeded, List<ImportErrorRow>? Errors, string? Error);

/// <summary>F-Export FR-EXP-003/004: imports run as a background job, with per-row validation and a summary, and downloadable templates.</summary>
public class ImportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<ApiResult<ImportJobRow>> StartAsync(ApiClient client, string fileName, byte[] content, string source = "users")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return await SendAsync(client, source, form);
    }

    private static async Task<ApiResult<ImportJobRow>> SendAsync(ApiClient client, string source, HttpContent content)
    {
        var response = await client.Http.PostAsync($"/api/imports/start?source={source}", content);
        var text = await response.Content.ReadAsStringAsync();
        return new ApiResult<ImportJobRow>(response.StatusCode,
            text.StartsWith('{') ? System.Text.Json.JsonSerializer.Deserialize<Envelope<ImportJobRow>>(text, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) : null,
            response);
    }

    private static Task<ApiResult<ImportJobRow>> StartCsvAsync(ApiClient client, string csv, string source = "users") =>
        StartAsync(client, "users.csv", Encoding.UTF8.GetBytes(csv), source);

    private static async Task<ImportJobRow> WaitForAsync(ApiClient client, Guid jobId, params string[] finished)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var job = (await client.GetAsync<List<ImportJobRow>>("/api/imports/list")).Data!.Single(j => j.Id == jobId);
            if (finished.Contains(job.Status)) return job;
            await Task.Delay(300);
        }

        throw new TimeoutException($"Import {jobId} did not reach {string.Join("/", finished)} in time.");
    }

    private static async Task<ImportJobRow> RunAsync(ApiClient client, string csv)
    {
        var started = await StartCsvAsync(client, csv);
        started.Status.Should().Be(HttpStatusCode.OK, string.Join("; ", started.Errors));
        return await WaitForAsync(client, started.Data!.Id, "Completed", "Failed");
    }

    private static async Task<List<string>> EmailsAsync(ApiClient admin, string filter) =>
        (await admin.GetAsync<PagedData<UserRow>>($"/api/users/list?page=1&pageSize=100&filter={filter}")).Data!.Items.Select(u => u.Email).ToList();

    [Fact]
    public async Task Queues_immediately_and_runs_valid_rows_in_while_invalid_rows_are_reported_with_row_and_reason()
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

        var started = await StartCsvAsync(admin, csv);
        started.Status.Should().Be(HttpStatusCode.OK);
        started.Data!.Status.Should().Be("Pending");           // returns straight away, work is queued, same shape as a background export

        var done = await WaitForAsync(admin, started.Data.Id, "Completed", "Failed");

        done.Status.Should().Be("Completed");
        done.Total.Should().Be(6);
        done.Succeeded.Should().Be(2);
        done.Errors!.Select(e => e.Row).Should().Equal(3, 4, 5, 7);
        done.Errors.Single(e => e.Row == 4).Reason.Should().Contain("Unknown role 'Nonexistent'");
        done.Errors.Single(e => e.Row == 5).Reason.Should().Contain("6");                       // the create rule's minimum length
        done.Errors.Single(e => e.Row == 7).Reason.Should().Contain("imp.good1@example.com");   // already taken

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

        var started = await StartAsync(admin, "users.xlsx", stream.ToArray());
        started.Status.Should().Be(HttpStatusCode.OK);
        var done = await WaitForAsync(admin, started.Data!.Id, "Completed", "Failed");

        done.Status.Should().Be("Completed");
        done.Succeeded.Should().Be(1);
        (await EmailsAsync(admin, "imp.excel")).Should().Equal("imp.excel@example.com");
    }

    [Fact]
    public async Task A_user_imported_without_a_password_exists_but_cannot_sign_in_with_a_guessed_one()
    {
        var admin = await factory.SignInAsync("imp.admin3@example.com", "Admin");
        await RunAsync(admin, "Email\r\nimp.nopass@example.com\r\n");

        var response = await factory.Anonymous().Http.PostAsJsonAsync("/api/identity/login", new { email = "imp.nopass@example.com", password = "Test1234!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await EmailsAsync(admin, "imp.nopass")).Should().Contain("imp.nopass@example.com");
    }

    [Fact]
    public async Task A_file_the_importer_cannot_use_fails_the_job_before_any_row_is_imported()
    {
        var admin = await factory.SignInAsync("imp.admin4@example.com", "Admin");

        var noEmailColumn = await RunAsync(admin, "Name\r\nAda\r\n");
        noEmailColumn.Status.Should().Be("Failed");
        noEmailColumn.Error.Should().Contain("Email");

        // Queuing itself refuses an empty upload straight away — there is no file to run a job on.
        (await StartAsync(admin, "users.csv", [])).Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unrecognized_file_extension_still_fails_only_the_job_not_the_request()
    {
        var admin = await factory.SignInAsync("imp.admin7@example.com", "Admin");

        var started = await StartAsync(admin, "users.txt", Encoding.UTF8.GetBytes("Email\na@b.co"));
        started.Status.Should().Be(HttpStatusCode.OK);       // queuing does not parse the file; that is the worker's job
        var job = await WaitForAsync(admin, started.Data!.Id, "Completed", "Failed");
        job.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task Importing_needs_the_create_permission()
    {
        var plain = await factory.SignInAsync("imp.plain@example.com");

        (await StartCsvAsync(plain, "Email\r\nimp.nope@example.com\r\n")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await StartCsvAsync(factory.Anonymous(), "Email\r\na@b.co\r\n")).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_template_carries_the_expected_columns_and_no_data_rows()
    {
        var admin = await factory.SignInAsync("imp.admin5@example.com", "Admin");

        var csv = await admin.Http.GetAsync("/api/imports/template?source=users");
        csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync()).Should().Contain("Email,Full name,Roles,Password");

        var xlsx = await admin.Http.GetAsync("/api/imports/template?source=users&format=xlsx");
        using var workbook = new XLWorkbook(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync()));
        workbook.Worksheet("Data").RowsUsed().Should().ContainSingle();                            // headers only: nothing to import by accident
        workbook.Worksheet("Data").Row(1).Cells(1, 4).Select(c => c.GetString()).Should().Equal("Email", "Full name", "Roles", "Password");
        workbook.Worksheet("Instructions").Cell(2, 2).GetString().Should().Be("Yes");              // Email is required

        (await admin.Http.GetAsync("/api/imports/template?source=users&format=pdf")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.Http.GetAsync("/api/imports/template?source=nope")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_downloaded_template_filled_in_imports_cleanly()
    {
        var admin = await factory.SignInAsync("imp.admin6@example.com", "Admin");
        var template = Encoding.UTF8.GetString(await (await admin.Http.GetAsync("/api/imports/template?source=users")).Content.ReadAsByteArrayAsync()).TrimStart('﻿');

        var done = await RunAsync(admin, template + "imp.template@example.com,Template User,User,Passw0rd!x\r\n");

        done.Status.Should().Be("Completed");
        done.Succeeded.Should().Be(1);
        (done.Errors?.Count ?? 0).Should().Be(0);
    }
}
