using System.Diagnostics;

namespace MajdsApp.SharedKernel.Middleware;

/// <summary>
/// The application's own trace source (F-Health FR-HEALTH-003). Every MediatR request runs inside a span named after it, nested under the HTTP request's span, so a trace shows
/// which command or query took the time. Spans are exported when a tracing backend is configured (<c>Telemetry:OtlpEndpoint</c>); with none, they cost almost nothing.
/// </summary>
public static class PlatformTelemetry
{
    public const string SourceName = "MajdsApp.Platform";

    public static readonly ActivitySource Source = new(SourceName);
}
