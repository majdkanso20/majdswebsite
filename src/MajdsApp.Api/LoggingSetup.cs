using System.ComponentModel.DataAnnotations;
using Serilog;
using Serilog.Events;

namespace MajdsApp;

/// <summary>The <c>Logging</c> settings that decide where the application's log goes (F-Errors FR-ERR-003/005). Checked when the application starts.</summary>
public class LogSinkOptions
{
    /// <summary>The least severe event that is written: Verbose, Debug, Information, Warning, Error or Fatal.</summary>
    [RegularExpression("^(?i:Verbose|Debug|Information|Warning|Error|Fatal)$", ErrorMessage = "Logging:Level must be Verbose, Debug, Information, Warning, Error or Fatal.")]
    public string Level { get; set; } = "Information";

    public bool Console { get; set; } = true;

    public FileSinkOptions File { get; set; } = new();
}

public class FileSinkOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>A folder, relative to the application or absolute. One file a day (<c>majds-yyyyMMdd.log</c>).</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Logging:File:Path must not be empty.")]
    public string Path { get; set; } = "App_Data/logs";

    /// <summary>How many daily files are kept before the oldest is deleted.</summary>
    [Range(1, 3650, ErrorMessage = "Logging:File:RetainedFiles must be between 1 and 3650.")]
    public int RetainedFiles { get; set; } = 14;
}

public static class LoggingSetup
{
    /// <summary>
    /// Makes Serilog the application's logger, with the sinks named in configuration: the console and a daily rolling file. Every line carries the request's
    /// correlation id (put on the log context by the correlation middleware) so one request can be followed across lines and across the response header.
    /// </summary>
    public static void AddPlatformLogging(this WebApplicationBuilder builder)
    {
        const string template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {CorrelationId} {SourceContext} {Message:lj}{NewLine}{Exception}";

        builder.Host.UseSerilog((context, services, logger) =>
        {
            // Read here, not earlier, so the final configuration (including anything a host or test overrides) is what counts.
            var options = context.Configuration.GetSection("Logging").Get<LogSinkOptions>() ?? new LogSinkOptions();
            // Serilog would refuse a bad retention with its own wording; say which setting is wrong instead.
            if (options.File.RetainedFiles is < 1 or > 3650) throw new InvalidOperationException("Logging:File:RetainedFiles must be between 1 and 3650.");
            if (!Enum.TryParse<LogEventLevel>(options.Level, ignoreCase: true, out var level)) level = LogEventLevel.Information;

            logger.MinimumLevel.Is(level)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                .Enrich.FromLogContext();

            if (options.Console) logger.WriteTo.Console(outputTemplate: template);

            if (options.File.Enabled)
            {
                var folder = System.IO.Path.IsPathRooted(options.File.Path) ? options.File.Path : System.IO.Path.Combine(context.HostingEnvironment.ContentRootPath, options.File.Path);
                logger.WriteTo.File(System.IO.Path.Combine(folder, "majds-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: options.File.RetainedFiles,
                    outputTemplate: template, shared: true);
            }
        });
    }
}
