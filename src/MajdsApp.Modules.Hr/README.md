# MajdsApp.Modules.Hr

**HR: Departments and Employees** — not an SRS requirement. Built as practice reverse-engineering an ABP demo module end to end (the supervisor's suggestion: pick one demo module and rebuild it, using every existing cross-cutting piece rather than writing any of it again — the same "don't repeat yourself" point made about the ABP demo's data grid).

Two related entities, both in the shared `ApplicationDbContext` (no new DbContext, no plugin): `Department` (name, description) and `Employee` (name, email, job title, an optional department, hire date, status). Deleting a department that still has employees is refused (409) — move them first.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/departments/list`
- `GET /api/departments/list-all` — every department, unpaged, for the employee form's department picker
- `POST /api/departments/create`
- `POST /api/departments/update`
- `POST /api/departments/delete`
- `GET /api/employees/list`
- `GET /api/employees/get`
- `POST /api/employees/create`
- `POST /api/employees/update`
- `POST /api/employees/delete`

## Permissions

One flat set for both entities (the same shape as Files' Upload/View/Delete, reasonable for two lists edited by the same HR staff): `Hr.View`, `Hr.Create`, `Hr.Edit`, `Hr.Delete`.

## Data

Tables: `HrDepartments`, `HrEmployees` (`DepartmentId` is a nullable FK with `Restrict` delete behavior — an employee's department is never silently cleared or cascaded). Migrations live in `MajdsApp.Core`.

Both entities extend `AuditableEntity<int>` (`MajdsApp.SharedKernel.Data`), so soft delete, `CreatedAt`/`CreatedBy`/`ModifiedAt`/`ModifiedBy` stamping, and the audit trail's before/after property capture all come for free from the shared `AuditSaveChangesInterceptor` — nothing HR-specific was written for any of that.

## Notes

- Feature flag `Hr` (default on); the HR menu entries and routes are both gated on it, same as Files/Imports.
- The employee list's `departmentName` column is a correlated subquery in the paging projection, the same technique `ListDepartmentsQuery`'s `employeeCount` column uses — sorting/paging happens on the entity query first, the DTO projection (including the joined name) happens only for the returned page (`ApplyPagingAsync`'s two-type-parameter overload).

## Tests

Covered by the integration tests in `src/MajdsApp.Tests` (run `dotnet test src/MajdsApp.Tests`): permission gating, full CRUD on both entities, the department-still-has-employees refusal, an unknown department id refused on create, and the unpaged picker listing.
