# MajdsApp.Modules.Health

**Health (F-Health)** — SRS FR-HEALTH-001, 004

Liveness and readiness probes for orchestrators and load balancers.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /health/live` (anonymous)
- `GET /health/ready` (anonymous)

## Permissions

None; the probes are public and intentionally outside the `ResponseDto` envelope.

## Notes

- `/health/ready` runs a database check and a file-storage check and returns 503 if either is unhealthy. Add a check by registering an `IHealthCheck` in your module.
- No metrics or distributed tracing yet.

## Tests

Not yet covered by automated tests (see the traceability document).
