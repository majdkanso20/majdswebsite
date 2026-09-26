using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>F-Errors FR-ERR-003/005: Serilog is the application's logger, writing to the sinks named in configuration, with the correlation id on every line.</summary>
public class LoggingTests
{
    private static string ReadLog(string folder)
    {
        var file = Directory.GetFiles(folder, "majds-*.log").Single();
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return new StreamReader(stream).ReadToEnd();
    }

    private sealed class LoggedFactory(string folder, string level = "Information", bool file = true) : ApiFactory(new Dictionary<string, string>
    {
        ["Logging:File:Path"] = folder, ["Logging:Level"] = level, ["Logging:File:Enabled"] = file.ToString(), ["Logging:Console"] = "false"
    }, null);

    [Fact]
    public async Task Log_lines_are_written_to_a_daily_file_with_the_requests_correlation_id()
    {
        var folder = Path.Combine(Path.GetTempPath(), "majds-logs-" + Guid.NewGuid().ToString("N"));
        using (var factory = new LoggedFactory(folder))
        {
            var admin = await factory.SignInAsync("log.admin@example.com", "Admin");
            var response = await admin.Http.GetAsync("/api/settings/my");   // handled by a MediatR request, whose start is logged
            var correlation = response.Headers.GetValues("X-Correlation-Id").Single();

            await Task.Delay(200);
            var log = ReadLog(folder);

            log.Should().Contain(correlation);
            log.Should().MatchRegex(@"\[(INF|WRN|ERR)\]");
        }
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void The_minimum_level_filters_what_is_written()
    {
        var folder = Path.Combine(Path.GetTempPath(), "majds-logs-" + Guid.NewGuid().ToString("N"));
        using (var factory = new LoggedFactory(folder, level: "Fatal"))
        {
            factory.CreateClient();
            Thread.Sleep(200);
        }

        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "majds-*.log") : [];
        (files.Length == 0 || !ReadLog(folder).Contains("[INF]")).Should().BeTrue();
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void The_file_sink_can_be_turned_off()
    {
        var folder = Path.Combine(Path.GetTempPath(), "majds-logs-" + Guid.NewGuid().ToString("N"));
        using (var factory = new LoggedFactory(folder, file: false))
            factory.CreateClient();

        Directory.Exists(folder).Should().BeFalse();
    }

    [Fact]
    public void A_wrong_level_or_retention_stops_the_application_from_starting()
    {
        using var badLevel = new ApiFactory(new Dictionary<string, string> { ["Logging:Level"] = "Loud" }, null);
        badLevel.Invoking(f => f.CreateClient()).Should().Throw<Exception>().Which.ToString().Should().Contain("Logging:Level");

        using var badRetention = new ApiFactory(new Dictionary<string, string> { ["Logging:File:RetainedFiles"] = "0" }, null);
        badRetention.Invoking(f => f.CreateClient()).Should().Throw<Exception>().Which.ToString().Should().Contain("Logging:File:RetainedFiles");
    }
}
