# MajdsApp.Modules.Roles

**Roles (F-Roles)** — SRS FR-ROLE-001..005

Create, edit and delete roles. `Admin` (all permissions) and `User` are seeded and built in; a role can be marked as the default assigned to new users. Editing a role's permissions is in the Authorization module.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/roles/get`
- `GET /api/roles/list`
- `POST /api/roles/create`
- `POST /api/roles/delete`
- `POST /api/roles/update`

## Permissions

`Roles.View`, `Roles.Create`, `Roles.Edit`, `Roles.Delete` (declared in the Authorization module).

## Notes

- Built-in roles cannot be deleted, and a role that still has members cannot be deleted (there is no reassignment flow yet).
- Role names are unique; duplicates return 409 and an empty name returns 400 with the reason in `errors`.

## Tests

Covered by the integration tests in `src/MajdsApp.Tests` (run `dotnet test src/MajdsApp.Tests`).
