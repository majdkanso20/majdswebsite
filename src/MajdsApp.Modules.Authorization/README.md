# MajdsApp.Modules.Authorization

**Authorization (F-Authorization)** — SRS FR-AUTHZ-001..008

Permission-based access control. Roles are bags of permissions; permissions are the atomic thing checked in code and in the UI. A user's effective permissions are the union over their roles, plus direct grants, minus direct denies; the `Admin` role holds everything.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/permissions/tree`
- `GET /api/session/permissions`
- `GET /api/roles/permissions/get?roleId=`
- `POST /api/roles/permissions/update`

## Permissions

`Users.View/Create/Edit/Delete` and `Roles.View/Create/Edit/Delete` are defined here. `permissions/tree` and `roles/permissions/get` need `Roles.View`; `roles/permissions/update` needs `Roles.Edit`; `session/permissions` needs only sign-in.

Declared here: `Roles.Create`, `Roles.Delete`, `Roles.Edit`, `Roles.View`, `Users.Create`, `Users.Delete`, `Users.Edit`, `Users.View`.

## Data

Tables: `RolePermissions`, `UserPermissions`. Migrations live in `MajdsApp.Core`.

## Notes

- The permission tree is built by scanning loaded `MajdsApp.Modules.*` and `MajdsApp.Plugins.*` assemblies for nested static classes of `const string`. A module or plugin adds permissions just by declaring them; they appear grouped in the role editor with no registration.
- Effective permissions are cached per user for five minutes and invalidated when a role's permissions change or a user's roles change.
- Direct per-user grants and denies are honoured by the checker, but there is no screen or endpoint to set them yet.
- Never check role names in business logic: put `[RequiresPermission(...)]` on the request.

## Tests

Covered by the integration tests in `src/MajdsApp.Tests` (run `dotnet test src/MajdsApp.Tests`).

## Direct user permissions (FR-AUTHZ-003)

Besides what a user's roles give, an administrator can grant or deny single permissions to one user. `GET /api/users/permissions/get?userId=` (needs `Users.View`) returns `fromRoles`, `granted`, `denied` and the resulting `effective` set; `POST /api/users/permissions/update` (needs `Users.Edit`) replaces the grants and denies (`{ userId, granted: [], denied: [] }`). A deny removes a permission the roles give and wins over a grant; a grant adds one they do not. The user's cached permissions are dropped, so it applies to their next request. The Admin role holds every permission whatever is set. The Users page has a Permissions dialog for it.
