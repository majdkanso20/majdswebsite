using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace MajdsApp.SharedKernel.Middleware;

/// <summary>
/// Gives every request one correlation id and carries it through logs and calls (FR-ERR-003, NFR-OBS-1, F-Health FR-HEALTH-003).
/// The id is the caller's <c>X-Correlation-Id</c> when it is well formed, else the request's W3C trace id (so a log line, a trace and a
/// response header all name the same request, and a <c>traceparent</c> from an upstream service continues its trace), else a new one. It is
/// echoed in the response, added to the trace as baggage (so outgoing HTTP calls made while handling the request carry it), and put on every
/// log line with the trace and span ids and the signed-in user. An id that could forge a log line (control characters, unbounded length) is discarded.
/// If the host did not start a trace for the request (nothing is listening), one is started here so the ids always exist.
/// </summary>
public partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string TraceParentHeader = "traceparent";

    public async Task InvokeAsync(HttpContext context)
    {
        Activity? own = null;
        if (Activity.Current is null)
        {
            own = new Activity("http.request");
            ContinueUpstreamTrace(context, own);
            own.Start();
        }

        try
        {
            var activity = Activity.Current;
            var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var supplied) && IsWellFormed(supplied.ToString())
                ? supplied.ToString()
                : activity is { TraceId: var trace } && trace != default ? trace.ToString() : Guid.NewGuid().ToString("n");

            context.Response.Headers[HeaderName] = correlationId;
            context.Items[HeaderName] = correlationId;
            activity?.AddBaggage("correlation-id", correlationId);
            activity?.SetTag("correlation.id", correlationId);

            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("TraceId", activity?.TraceId.ToString()))
            using (LogContext.PushProperty("SpanId", activity?.SpanId.ToString()))
            using (LogContext.PushProperty("UserId", context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null))
            {
                await next(context);
            }
        }
        finally
        {
            own?.Stop();
        }
    }

    /// <summary>Continues the caller's W3C trace: <c>traceparent: 00-&lt;trace id&gt;-&lt;parent span id&gt;-&lt;flags&gt;</c>. A malformed header is ignored and a new trace begins.</summary>
    private static void ContinueUpstreamTrace(HttpContext context, Activity activity)
    {
        var header = context.Request.Headers[TraceParentHeader].ToString();
        if (!TraceParent().IsMatch(header)) return;

        var parts = header.Split('-');
        try
        {
            var flags = (ActivityTraceFlags)Convert.ToByte(parts[3], 16) & ActivityTraceFlags.Recorded;
            activity.SetParentId(ActivityTraceId.CreateFromString(parts[1]), ActivitySpanId.CreateFromString(parts[2]), flags);
        }
        catch (ArgumentException)
        {
            // an unusable id (for example all zeros): start a fresh trace
        }
    }

    /// <summary>Letters, digits and <c>._-:</c>, at most 100 characters: enough for a GUID, a trace id or a gateway's own id, nothing that can break a log line.</summary>
    public static bool IsWellFormed(string value) => value.Length is > 0 and <= 100 && SafeId().IsMatch(value);

    [GeneratedRegex(@"^[A-Za-z0-9._:\-]+$")]
    private static partial Regex SafeId();

    [GeneratedRegex(@"^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$")]
    private static partial Regex TraceParent();
}
