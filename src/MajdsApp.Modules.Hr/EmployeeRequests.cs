using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Paging;
using MajdsApp.SharedKernel.Search;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Hr;

public record EmployeeDto(int Id, string FullName, string Email, string? JobTitle, int? DepartmentId, string? DepartmentName, DateTime? HireDate, string Status, DateTime CreatedAt);

[RequiresPermission(Permissions.Hr.View)]
public record ListEmployeesQuery(PagedRequest Request) : IRequest<PagedResponse<EmployeeDto>>;

public class ListEmployeesQueryHandler(ApplicationDbContext db) : IRequestHandler<ListEmployeesQuery, PagedResponse<EmployeeDto>>
{
    public Task<PagedResponse<EmployeeDto>> Handle(ListEmployeesQuery request, CancellationToken ct)
    {
        var query = db.Set<Employee>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Request.Filter))
        {
            var pattern = LikePattern.Contains(request.Request.Filter);
            query = query.Where(e => EF.Functions.Like(e.FullName, pattern, LikePattern.Escape) || EF.Functions.Like(e.Email, pattern, LikePattern.Escape));
        }

        var sortableColumns = new Dictionary<string, System.Linq.Expressions.Expression<Func<Employee, object>>>
        {
            ["fullName"] = e => e.FullName,
            ["email"] = e => e.Email,
            ["hireDate"] = e => e.HireDate!,
            ["status"] = e => e.Status,
            ["createdAt"] = e => e.CreatedAt
        };

        return query.ApplyPagingAsync(request.Request, sortableColumns, e => new EmployeeDto(
            e.Id, e.FullName, e.Email, e.JobTitle, e.DepartmentId,
            db.Set<Department>().Where(d => d.Id == e.DepartmentId).Select(d => d.Name).FirstOrDefault(),
            e.HireDate, e.Status.ToString(), e.CreatedAt), ct);
    }
}

[RequiresPermission(Permissions.Hr.View)]
public record GetEmployeeQuery(int Id) : IRequest<EmployeeDto>;

public class GetEmployeeQueryHandler(ApplicationDbContext db) : IRequestHandler<GetEmployeeQuery, EmployeeDto>
{
    public async Task<EmployeeDto> Handle(GetEmployeeQuery request, CancellationToken ct)
    {
        var employee = await db.Set<Employee>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new NotFoundException("Employee not found.");
        var departmentName = employee.DepartmentId is null ? null
            : await db.Set<Department>().Where(d => d.Id == employee.DepartmentId).Select(d => d.Name).FirstOrDefaultAsync(ct);

        return new EmployeeDto(employee.Id, employee.FullName, employee.Email, employee.JobTitle, employee.DepartmentId,
            departmentName, employee.HireDate, employee.Status.ToString(), employee.CreatedAt);
    }
}

public record EmployeeInput(string FullName, string Email, string? JobTitle, int? DepartmentId, DateTime? HireDate, string Status);

[RequiresPermission(Permissions.Hr.Create)]
public record CreateEmployeeCommand(EmployeeInput Employee) : IRequest<int>, IAuditableCommand;

public class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator(ApplicationDbContext db)
    {
        RuleFor(x => x.Employee.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Employee.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Employee.JobTitle).MaximumLength(200);
        RuleFor(x => x.Employee.Status).Must(s => Enum.TryParse<EmployeeStatus>(s, out _)).WithMessage("Invalid status.");
        RuleFor(x => x.Employee.DepartmentId)
            .MustAsync(async (id, ct) => id is null || await db.Set<Department>().AnyAsync(d => d.Id == id, ct))
            .WithMessage("Department not found.");
    }
}

public class CreateEmployeeCommandHandler(ApplicationDbContext db) : IRequestHandler<CreateEmployeeCommand, int>
{
    public async Task<int> Handle(CreateEmployeeCommand request, CancellationToken ct)
    {
        var input = request.Employee;
        var employee = new Employee
        {
            Id = 0, FullName = input.FullName, Email = input.Email, JobTitle = input.JobTitle,
            DepartmentId = input.DepartmentId, HireDate = input.HireDate, Status = Enum.Parse<EmployeeStatus>(input.Status)
        };

        db.Set<Employee>().Add(employee);
        await db.SaveChangesAsync(ct);
        return employee.Id;
    }
}

[RequiresPermission(Permissions.Hr.Edit)]
public record UpdateEmployeeCommand(int Id, EmployeeInput Employee) : IRequest, IAuditableCommand;

public class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator(ApplicationDbContext db)
    {
        RuleFor(x => x.Employee.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Employee.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Employee.JobTitle).MaximumLength(200);
        RuleFor(x => x.Employee.Status).Must(s => Enum.TryParse<EmployeeStatus>(s, out _)).WithMessage("Invalid status.");
        RuleFor(x => x.Employee.DepartmentId)
            .MustAsync(async (id, ct) => id is null || await db.Set<Department>().AnyAsync(d => d.Id == id, ct))
            .WithMessage("Department not found.");
    }
}

public class UpdateEmployeeCommandHandler(ApplicationDbContext db) : IRequestHandler<UpdateEmployeeCommand>
{
    public async Task Handle(UpdateEmployeeCommand request, CancellationToken ct)
    {
        var employee = await db.Set<Employee>().FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new NotFoundException("Employee not found.");

        var input = request.Employee;
        employee.FullName = input.FullName;
        employee.Email = input.Email;
        employee.JobTitle = input.JobTitle;
        employee.DepartmentId = input.DepartmentId;
        employee.HireDate = input.HireDate;
        employee.Status = Enum.Parse<EmployeeStatus>(input.Status);

        await db.SaveChangesAsync(ct);
    }
}

[RequiresPermission(Permissions.Hr.Delete)]
public record DeleteEmployeeCommand(int Id) : IRequest, IAuditableCommand;

public class DeleteEmployeeCommandHandler(ApplicationDbContext db) : IRequestHandler<DeleteEmployeeCommand>
{
    public async Task Handle(DeleteEmployeeCommand request, CancellationToken ct)
    {
        var employee = await db.Set<Employee>().FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new NotFoundException("Employee not found.");

        db.Set<Employee>().Remove(employee);
        await db.SaveChangesAsync(ct);
    }
}
