# MajdsApp.Modules.Search

**Global search (F-Search)** — SRS FR-SEARCH-001..004

One search box across the platform. Each module contributes an `ISearchProvider` (label, required permission or feature, query, result mapping); the endpoint runs every provider the caller is allowed to use and merges the results. Users, Roles and Files ship providers.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/search?q=` (rate limited)

## Permissions

Signed-in users only; each provider's own permission (and feature flag) decides whether its results are included.

## Notes

- Providers are found by assembly scan, so a new module adds search results with no change here. The frontend debounces input and groups results by category.

## Tests

Not yet covered by automated tests (see the traceability document).
