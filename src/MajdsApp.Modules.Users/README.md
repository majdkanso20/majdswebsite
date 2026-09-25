# MajdsApp.Modules.Users

**Users (F-Users)** — SRS FR-USER-001..008

Administrative management of user accounts: list and search, create, edit (name, phone, roles), activate or deactivate, soft-delete, reset password (emails a link), unlock, reset two-factor, export to CSV, Excel or PDF, and import from CSV or Excel.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `POST /api/users/import` — multipart `file` (.csv or .xlsx, up to 5 MB and 5,000 rows). Creates one user per row through the normal create command and returns `{ total, succeeded, failed, errors: [{ row, reason }] }`. Requires `Users.Create`. Columns: `Email` (required), `Full name`, `Roles` (semicolon-separated), `Password` (blank generates a random one; the user then uses Forgot password). Valid rows are imported, invalid rows are reported with their row number (the header is row 1) and reason; an unknown role or a duplicate email fails only that row.
- `GET /api/users/import-template` — `format=csv` (default) or `xlsx`; headers only, plus an Instructions sheet in Excel. Requires `Users.View`.
- `GET /api/users/export` — `format` (`csv` default, `xlsx`, `pdf`) plus the list's `filter`, `isActive` and `role`. Returns the file, not the envelope.
- `GET /api/users/get`
- `GET /api/users/list`
- `POST /api/users/create`
- `POST /api/users/delete`
- `POST /api/users/reset-password`
- `POST /api/users/reset-two-factor`
- `POST /api/users/set-activation`
- `POST /api/users/unlock`
- `POST /api/users/update`

## Permissions

`Users.View/Create/Edit/Delete` (declared in the Authorization module). Reset-password, unlock, two-factor reset and activation need `Users.Edit`.

## Notes

- Protections: an administrator cannot delete or deactivate their own account, and the last active administrator can never be deleted, deactivated or removed from the `Admin` role.
- A deactivated user cannot sign in; deletion is soft and deleted users are excluded from lists. Roles changes take effect on the user's next request and notify them.
- New accounts are created with a set password (no emailed set-password link yet); password rules are Identity's defaults plus a six-character minimum.
- The list is paged and sorted in the database and takes `filter`, `isActive` and `role`.

## Configuration keys

- Password reset links point at `WebApp:BaseUrl` (the Razor site's reset page).

## Tests

Covered by the integration tests in `src/MajdsApp.Tests` (run `dotnet test src/MajdsApp.Tests`).
