using System.Net;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>
/// A from-scratch feature module (Departments and Employees), built as practice reverse-engineering an ABP demo
/// module end to end: its own entity, migration, permissions and CRUD, reusing every existing cross-cutting piece
/// (soft delete, audit stamping, paging/sorting/filtering, `[RequiresPermission]`) rather than writing any of that
/// again — the same "don't repeat yourself" architecture point the supervisor made about the ABP demo's data grid.
/// </summary>
public class HrTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private record DepartmentRow(int Id, string Name, string? Description, int EmployeeCount);
    private record EmployeeRow(int Id, string FullName, string Email, string? JobTitle, int? DepartmentId, string? DepartmentName, string Status);

    [Fact]
    public async Task Creating_or_listing_needs_the_Hr_permission()
    {
        var plain = await factory.SignInAsync("hr.plain1@example.com");

        (await plain.GetAsync<PagedData<DepartmentRow>>("/api/departments/list?page=1&pageSize=5")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await plain.PostAsync("/api/departments/create", new { name = "Engineering", description = (string?)null })).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_department_can_be_created_listed_updated_and_deleted()
    {
        var admin = await factory.SignInAsync("hr.admin1@example.com", "Admin");

        var id = (await admin.PostAsync<int>("/api/departments/create", new { name = "Engineering", description = "Builds the product" })).Data;

        var listed = (await admin.GetAsync<PagedData<DepartmentRow>>("/api/departments/list?page=1&pageSize=20&filter=Engineering")).Data!.Items;
        listed.Should().ContainSingle(d => d.Id == id && d.Name == "Engineering" && d.EmployeeCount == 0);

        (await admin.PostAsync("/api/departments/update", new { id, name = "Engineering", description = "Builds and ships the product" })).Status.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync<PagedData<DepartmentRow>>("/api/departments/list?page=1&pageSize=20&filter=Engineering")).Data!.Items
            .Should().ContainSingle(d => d.Id == id).Which.Description.Should().Be("Builds and ships the product");

        (await admin.PostAsync("/api/departments/delete", new { id })).Status.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync<PagedData<DepartmentRow>>("/api/departments/list?page=1&pageSize=20&filter=Engineering")).Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task A_department_that_still_has_employees_cannot_be_deleted()
    {
        var admin = await factory.SignInAsync("hr.admin2@example.com", "Admin");
        var departmentId = (await admin.PostAsync<int>("/api/departments/create", new { name = "Sales", description = (string?)null })).Data;
        await admin.PostAsync("/api/employees/create", new { fullName = "Sam Seller", email = "sam@example.com", jobTitle = "Rep", departmentId, hireDate = (DateTime?)null, status = "Active" });

        var deleting = await admin.PostAsync("/api/departments/delete", new { id = departmentId });

        deleting.Status.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_employee_can_be_created_with_a_department_listed_with_its_name_updated_and_deleted()
    {
        var admin = await factory.SignInAsync("hr.admin3@example.com", "Admin");
        var departmentId = (await admin.PostAsync<int>("/api/departments/create", new { name = "Support", description = (string?)null })).Data;

        var employeeId = (await admin.PostAsync<int>("/api/employees/create",
            new { fullName = "Alex Agent", email = "alex@example.com", jobTitle = "Support Agent", departmentId, hireDate = "2024-01-15", status = "Active" })).Data;

        var listed = (await admin.GetAsync<PagedData<EmployeeRow>>("/api/employees/list?page=1&pageSize=20&filter=Alex")).Data!.Items;
        listed.Should().ContainSingle(e => e.Id == employeeId && e.DepartmentName == "Support" && e.Status == "Active");

        (await admin.PostAsync("/api/employees/update", new
        {
            id = employeeId,
            employee = new { fullName = "Alex Agent", email = "alex@example.com", jobTitle = "Senior Support Agent", departmentId, hireDate = "2024-01-15", status = "OnLeave" }
        })).Status.Should().Be(HttpStatusCode.OK);

        (await admin.GetAsync<PagedData<EmployeeRow>>("/api/employees/list?page=1&pageSize=20&filter=Alex")).Data!.Items
            .Should().ContainSingle(e => e.Id == employeeId).Which.Status.Should().Be("OnLeave");

        (await admin.PostAsync("/api/employees/delete", new { id = employeeId })).Status.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync<PagedData<EmployeeRow>>("/api/employees/list?page=1&pageSize=20&filter=Alex")).Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Creating_an_employee_for_a_department_that_does_not_exist_is_refused()
    {
        var admin = await factory.SignInAsync("hr.admin4@example.com", "Admin");

        var creating = await admin.PostAsync("/api/employees/create",
            new { fullName = "Ghost Employee", email = "ghost@example.com", jobTitle = (string?)null, departmentId = 999999, hireDate = (DateTime?)null, status = "Active" });

        creating.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_department_picker_lists_every_department_unpaged()
    {
        var admin = await factory.SignInAsync("hr.admin5@example.com", "Admin");
        await admin.PostAsync("/api/departments/create", new { name = "Marketing", description = (string?)null });
        await admin.PostAsync("/api/departments/create", new { name = "Finance", description = (string?)null });

        var all = (await admin.GetAsync<List<DepartmentRow>>("/api/departments/list-all")).Data!;

        all.Select(d => d.Name).Should().Contain(["Marketing", "Finance"]);
    }
}
