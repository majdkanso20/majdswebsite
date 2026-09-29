# MajdsApp.Modules.Users

**Users (F-Users)** — SRS FR-USER-001..008

Administrative management of user accounts: list and search, create (a password set directly or an emailed set-password link), edit (name, phone, roles), activate or deactivate, soft-delete, reset password (emails a link), unlock, reset two-factor, export to CSV, Excel or PDF, and import from CSV or Excel (as a background job, through `MajdsApp.Modules.Imports`).

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `POST /api/users/create` — `password` or `sendSetPasswordEmail: true` (FR-USER-002): with the latter, the account gets a password nobody knows and an email with a set-password link (the same mechanism as Forgot password), and `password` may be omitted.
- Importing users is `POST /api/imports/start?source=users` and `GET /api/imports/template?source=users` — see `MajdsApp.Modules.Imports`'s README for the API; `UsersImportSource` (`ImportUsers.cs`) is what registers `users` there and does the actual parsing and per-row creation. Columns: `Email` (required), `Full name`, `Roles` (semicolon-separated), `Password` (blank generates a random one; the user then uses Forgot password — a bulk import does not email a set-password link to every row). Valid rows are imported, invalid rows are reported with their row number (the header is row 1) and reason; an unknown role or a duplicate email fails only that row.
- `GET /api/users/export` — `format` (`csv` default, `xlsx`, `pdf`) plus the list's `filter`, `isActive` and `role`. Returns the file, not the envelope.
- `GET /api/users/get`
- `GET /api/users/list`
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
- New accounts are created with a password set directly, or with an emailed set-password link (`sendSetPasswordEmail`, FR-USER-002); password rules are Identity's defaults plus a six-character minimum.
- The list is paged and sorted in the database and takes `filter`, `isActive` and `role`.

## Configuration keys

- Password reset links point at `WebApp:BaseUrl` (the Razor site's reset page).

## Tests

Covered by the integration tests in `src/MajdsApp.Tests` (run `dotnet test src/MajdsApp.Tests`).
