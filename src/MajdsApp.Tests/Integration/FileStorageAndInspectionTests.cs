using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MajdsApp.Modules.Files;
using MajdsApp.SharedKernel.Files;
using MajdsApp.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>An in-memory store, standing in for a cloud blob provider a module would add.</summary>
public class MemoryFileStorage : IFileStorage
{
    public static readonly ConcurrentDictionary<string, byte[]> Blobs = new();

    public async Task SaveAsync(string storedName, Stream content, CancellationToken ct)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        Blobs[storedName] = copy.ToArray();
    }

    public Stream Open(string storedName) => new MemoryStream(Blobs[storedName]);
    public void Delete(string storedName) => Blobs.TryRemove(storedName, out _);
    public IEnumerable<(string Name, DateTime LastWriteUtc)> Enumerate() => Blobs.Keys.Select(k => (k, DateTime.UtcNow)).ToList();
}

public class MemoryFileStorageProvider : IFileStorageProvider
{
    public string Name => "Memory";
    public IFileStorage Create(IServiceProvider services) => new MemoryFileStorage();
}

public class MemoryStoreFactory : ApiFactory
{
    public MemoryStoreFactory() : base(new Dictionary<string, string> { ["Files:Storage:Provider"] = "Memory" }, null) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddSingleton<IFileStorageProvider, MemoryFileStorageProvider>());
    }
}

/// <summary>F-Files FR-FILE-001/002: the store is chosen by configuration, and uploads are judged by their content, not by their name or the client's claim.</summary>
public class FileStorageAndInspectionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private record Uploaded(Guid Id, string FileName, string ContentType, long Size);

    private static async Task<(HttpStatusCode Status, Uploaded? File, string Body)> UploadAsync(ApiClient client, string fileName, byte[] bytes, string clientContentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(clientContentType);
        form.Add(file, "file", fileName);
        var response = await client.Http.PostAsync("/api/files/upload", form);
        var body = await response.Content.ReadAsStringAsync();
        Uploaded? uploaded = null;
        if (response.IsSuccessStatusCode)
            uploaded = JsonDocument.Parse(body).RootElement.GetProperty("data").Deserialize<Uploaded>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return (response.StatusCode, uploaded, body);
    }

    // ---- inspecting the content --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("photo.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, true, "image/png")]
    [InlineData("photo.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, true, "image/jpeg")]
    [InlineData("doc.pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, true, "application/pdf")]
    [InlineData("sheet.xlsx", new byte[] { 0x50, 0x4B, 0x03, 0x04 }, true, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("notes.txt", new byte[] { 0x68, 0x69 }, true, "text/plain")]
    [InlineData("data.bin", new byte[] { 0x01, 0x02 }, true, "application/octet-stream")]
    [InlineData("photo.png", new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, false, "application/octet-stream")]        // a Windows program named as a picture
    [InlineData("notes.txt", new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, false, "application/octet-stream")]   // a program named as text
    [InlineData("notes.txt", new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, false, "application/octet-stream")]   // a Linux program
    [InlineData("run.txt", new byte[] { 0x23, 0x21, 0x2F, 0x62 }, false, "application/octet-stream")]     // a script with a shebang
    [InlineData("doc.pdf", new byte[] { 0x68, 0x69, 0x21 }, false, "application/pdf")]                   // text named as a PDF
    public void The_bytes_decide_what_a_file_is(string name, byte[] header, bool allowed, string contentType)
    {
        var result = FileContentInspector.Inspect(name, header);

        result.Allowed.Should().Be(allowed);
        result.ContentType.Should().Be(contentType);
        if (!allowed) result.Problem.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task The_content_type_stored_comes_from_the_content_not_from_what_the_client_said()
    {
        var admin = await factory.SignInAsync("fi.admin1@example.com", "Admin");

        var real = await UploadAsync(admin, "logo.png", Png, "text/html");                       // the client lies about a real PNG
        var text = await UploadAsync(admin, "readme.txt", "hello"u8.ToArray(), "application/x-msdownload");

        real.Status.Should().Be(HttpStatusCode.OK);
        real.File!.ContentType.Should().Be("image/png");
        text.File!.ContentType.Should().Be("text/plain");
    }

    [Fact]
    public async Task A_program_is_refused_whatever_it_is_called_and_nothing_is_left_behind()
    {
        var admin = await factory.SignInAsync("fi.admin2@example.com", "Admin");
        var program = Encoding.ASCII.GetBytes("MZ").Concat(new byte[64]).ToArray();

        foreach (var name in new[] { "harmless.png", "harmless.txt", "harmless.pdf", "harmless.bin" })
        {
            var result = await UploadAsync(admin, name, program, "image/png");
            result.Status.Should().Be(HttpStatusCode.BadRequest, name);
            result.Body.Should().Contain("was not accepted");
        }

        (await admin.GetAsync<PagedData<Uploaded>>("/api/files/list?page=1&pageSize=50")).Data!.Items.Should().NotContain(f => f.FileName.StartsWith("harmless"));
    }

    [Fact]
    public async Task A_file_named_as_a_picture_must_be_one()
    {
        var admin = await factory.SignInAsync("fi.admin3@example.com", "Admin");

        var result = await UploadAsync(admin, "fake.png", "just text"u8.ToArray(), "image/png");

        result.Status.Should().Be(HttpStatusCode.BadRequest);
        result.Body.Should().Contain("not a PNG file");
    }

    // ---- choosing the store ------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_disk_store_is_the_default()
    {
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IFileStorage>().Should().BeOfType<FileStorage>();
    }

    [Fact]
    public async Task A_store_a_module_added_is_chosen_by_configuration_and_holds_the_bytes_instead_of_the_disk()
    {
        using var memory = new MemoryStoreFactory();
        var admin = await memory.SignInAsync("fi.admin4@example.com", "Admin");

        var uploaded = await UploadAsync(admin, "in-memory.txt", "kept elsewhere"u8.ToArray(), "text/plain");
        var download = await admin.Http.GetAsync($"/api/files/download?fileId={uploaded.File!.Id}");

        uploaded.Status.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsStringAsync()).Should().Be("kept elsewhere");
        MemoryFileStorage.Blobs.Values.Should().Contain(b => Encoding.UTF8.GetString(b) == "kept elsewhere");
    }

    [Fact]
    public void A_provider_nobody_registered_stops_the_application_from_starting_and_lists_the_choices()
    {
        using var unknown = new ApiFactory(new Dictionary<string, string> { ["Files:Storage:Provider"] = "Floppy" }, null);

        var start = () => unknown.CreateClient();

        var failure = start.Should().Throw<Exception>().Which.ToString();
        failure.Should().Contain("Floppy").And.Contain("Disk");
    }
}
