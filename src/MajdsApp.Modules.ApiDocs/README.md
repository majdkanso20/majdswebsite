# MajdsApp.Modules.ApiDocs

**API documentation and versioning (F-ApiDocs)** — SRS FR-DOC-001..004

An OpenAPI document generated from the controllers (so a new endpoint appears with no extra work), an interactive UI at `/swagger` where a token can be supplied to try secured endpoints, API versioning, and control over who can read the docs outside Development.

## Using it

- **Documents:** `/swagger/v1/swagger.json` (and one per further version). Responses are described as the `ResponseDto<T>` envelope (schemas named `…ResponseDto`). Every operation also lists the error answers a client can get (400, 401, 403, 404, 429) with the same envelope, added by `ErrorResponsesOperationFilter` unless the action declares its own.
- **Trying secured endpoints:** sign in with `POST /api/identity/login`, click **Authorize** in the UI and paste the access token. The bearer scheme is applied to every operation, and the token is kept across a page reload.
- **Versions:** a request names its version with `?api-version=2.0` or the `X-Api-Version` header; with none it gets the default, **1.0**, so every existing route is unchanged. Responses list the versions the endpoint supports in `api-supported-versions`; an unknown version is a 400.
- **Adding a version:** mark a controller `[ApiVersion("2.0")]`. It appears in its own document, `/swagger/v2/swagger.json`, and the UI lists both. Today only 1.0 exists.

## Who can read the docs

- **Development:** open.
- **Anywhere else:** off (404) unless `Docs:Enabled` is `true`. Then `Docs:Access` decides: `Permission` (the default: a signed-in user holding `Docs.View`), `Authenticated` (any signed-in user) or `Open`.

The gate covers the UI page and the JSON. In a browser a page cannot send a bearer header on navigation, so with `Permission` or `Authenticated` the docs are meant to be fetched with a token (curl, Postman, an OpenAPI tool) rather than opened directly; use `Open` behind your own network controls if you want the browser UI in production.

## Permissions

Declared here: `Docs.View`. It appears in the role editor like any other, and the `Admin` role holds it.

## Configuration keys

`Docs:Enabled` (`true` to serve the docs outside Development) and `Docs:Access` (`Permission`, `Authenticated` or `Open`).

## Notes

- Not done: generating the Angular client from the document as part of the build (the Agent Notes suggest it), and XML-comment descriptions on operations.
- Only version 1.0 exists; the versioning machinery is in place and tested with a version-2.0 controller that lives in the test project.

## Tests

`ApiDocsTests` in `src/MajdsApp.Tests`: the gate in each mode, granting `Docs.View`, the content of the document (endpoints from many modules, the envelope, the bearer scheme and requirement), the UI configuration, and versioning end to end.
