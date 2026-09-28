# MajdsApp.Modules.Health

**Health and observability (F-Health)** — SRS FR-HEALTH-001..004

Liveness and readiness probes for orchestrators and load balancers, and request metrics for a scraper. All of these are infrastructure endpoints, so they are intentionally outside the `ResponseDto` envelope.

## Endpoints

- `GET /health/live` — anonymous. 200 while the process answers; runs no dependency check.
- `GET /health/ready` — anonymous. Runs every dependency check; **503** when a critical one is down, **200** otherwise (a non-critical failure is reported as `Degraded`). The body lists each check with its status, a short description and how long it took.
- `GET /metrics` — the Prometheus text format: `http_request_duration_seconds` (a histogram, whose `_count` is the request rate and `_bucket` the latency) labelled by status `code`, `method`, `controller`, `action` and `endpoint`, so the error rate is the share of 4xx and 5xx codes; plus process and .NET runtime metrics. See *Metrics access*.

## Readiness checks

| Check | If it fails | Why |
|---|---|---|
| `database` | **Unhealthy** (503) | nothing works without it |
| `file-storage` | **Unhealthy** (503) | uploads and exports cannot be saved |
| `cache` | Degraded | writes, reads and removes a value through `ICacheService`; a miss falls back to the database, so it only slows things down |
| `email` | Degraded | opens a connection to the mail server (nothing is sent); skipped and reported as "not configured" when there are no credentials; mail is queued and retried |

Add a check by registering an `IHealthCheck` in your module; give it `HealthStatus.Degraded` as its failure status if the platform can work without it.

## Metrics access

Metrics describe traffic, so they are not public by default. With `Metrics:Token` set, a scraper must send `Authorization: Bearer <token>`. With no token the endpoint is open only in Development (or when `Metrics:AllowAnonymous` is `true`) and otherwise answers 404. `Metrics:Enabled=false` turns it off. Prometheus can send the token with `authorization: credentials:` in its scrape config.

## Correlation and tracing

Every request has one correlation id, returned in `X-Correlation-Id`: the caller's own when it is well formed (letters, digits and `._-:`, at most 100 characters; anything that could forge a log line is discarded), else the request's W3C trace id, else a new one. An incoming `traceparent` from an upstream service is continued, so a log line, a trace and the response header all name the same request. The id is added to the trace as baggage (so outgoing HTTP calls made during the request carry it) and to every log line with `TraceId`, `SpanId` and the user.

## Permissions

None; the probes are public.

## Notes

- Tracing (FR-HEALTH-003): every request is traced with OpenTelemetry. There is a span for the HTTP request (continuing an upstream `traceparent`), a nested span for each command or query it runs (named after it, marked as an error when it fails) and one for each outgoing HTTP call; the health probes and `/metrics` are left out. The trace id is the same one in the `X-Correlation-Id` header and on every log line. Set `Telemetry:OtlpEndpoint` (for example `http://localhost:4317`, an OpenTelemetry collector, Jaeger or any OTLP backend) to export the spans; leave it empty to keep them in process. `Telemetry:ServiceName` names the service in the backend. No SMS or push provider exists to check.
- Metrics are per server; a scraper collects each one.

## Tests

`HealthAndObservabilityTests` and `MetricsTests` in `src/MajdsApp.Tests`: liveness, readiness with a critical and a non-critical failure (503 vs Degraded), the database, cache and email checks, correlation and `traceparent` handling, and metrics access and content.
