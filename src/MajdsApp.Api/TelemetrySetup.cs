using System.ComponentModel.DataAnnotations;
using MajdsApp.SharedKernel.Configuration;
using MajdsApp.SharedKernel.Middleware;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MajdsApp;

/// <summary>The <c>Telemetry</c> section (F-Health FR-HEALTH-003): where traces are sent. Checked when the application starts.</summary>
public class TelemetryOptions : IValidatableObject
{
    /// <summary>The address of an OpenTelemetry collector or tracing backend that speaks OTLP (for example <c>http://localhost:4317</c>). Empty means spans are made but not exported.</summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>How the service is named in the tracing backend.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Telemetry:ServiceName must not be empty.")]
    public string ServiceName { get; set; } = "MajdsApp.Api";

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!string.IsNullOrWhiteSpace(OtlpEndpoint) && !(Uri.TryCreate(OtlpEndpoint, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
            yield return new ValidationResult("Telemetry:OtlpEndpoint must be an http or https address.", [nameof(OtlpEndpoint)]);
    }
}

public static class TelemetrySetup
{
    /// <summary>
    /// Traces the requests the API handles: the HTTP request (continuing an upstream <c>traceparent</c>), the commands and queries it runs, and the outgoing HTTP calls it makes,
    /// with the same trace id the logs and the <c>X-Correlation-Id</c> header carry. Sent to the backend named by <c>Telemetry:OtlpEndpoint</c> when there is one.
    /// The health probes and the metrics endpoint are left out; they would only be noise.
    /// </summary>
    public static void AddPlatformTelemetry(this WebApplicationBuilder builder)
    {
        builder.Services.AddModuleOptions<TelemetryOptions>(builder.Configuration, "Telemetry");

        var telemetry = builder.Configuration.GetSection("Telemetry").Get<TelemetryOptions>() ?? new TelemetryOptions();
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(string.IsNullOrWhiteSpace(telemetry.ServiceName) ? "MajdsApp.Api" : telemetry.ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(PlatformTelemetry.SourceName)
                    .AddAspNetCoreInstrumentation(options => options.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health") && !context.Request.Path.StartsWithSegments("/metrics"))
                    .AddHttpClientInstrumentation();

                if (Uri.TryCreate(telemetry.OtlpEndpoint, UriKind.Absolute, out var endpoint))
                    tracing.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
            });
    }
}
