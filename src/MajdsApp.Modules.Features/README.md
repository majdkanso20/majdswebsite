# MajdsApp.Modules.Features

**Feature flags (F-Features)** — SRS FR-FEAT-001..004

Turn whole capabilities on or off at runtime without a deploy. Modules declare flags with `FeatureDefinition` fields (Files and Notifications ship with the platform); `[RequiresFeature("Name")]` on a request makes it unavailable when the flag is off, and the frontend hides the matching UI.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/features/enabled`
- `GET /api/features/list`
- `POST /api/features/set`

## Permissions

`Features.View` (list) and `Features.Edit` (set). `GET /api/features/enabled` needs only sign-in.

Declared here: `Features.Edit`, `Features.View`.

## Data

Tables: `FeatureOverrides`. Migrations live in `MajdsApp.Core`.

## Notes

- Effective values (default plus application override) are cached and invalidated on change. Per-user overrides are optional in the SRS and not implemented.

## Tests

Covered by the integration tests in `src/MajdsApp.Tests` (run `dotnet test src/MajdsApp.Tests`).
