# MajdsApp.Modules.Diagnostics

**Diagnostics** — SRS Platform proof (P1-P4, F-Errors)

A small module that exists to prove the platform end to end: a ping endpoint, a persisted ping list built on the generic repository and unit of work, and a deliberately failing endpoint that shows the exception-to-envelope mapping. It is also the only module that uses the generic `IRepository`/`ISpecification` layer.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/diagnostics/boom`
- `GET /api/diagnostics/ping`
- `GET /api/diagnostics/ping/list`
- `POST /api/diagnostics/ping/record`

## Permissions

None declared.

## Data

Tables: `DiagnosticsPings`. Migrations live in `MajdsApp.Core`.

## Notes

- `GET /api/diagnostics/boom` always throws, to demonstrate that unhandled errors reach the client as a safe `ResponseDto` with no stack trace. Remove this module from `Program.cs` in an application that does not want it.

## Tests

Not yet covered by automated tests (see the traceability document).
