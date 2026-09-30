using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Paging;
using MajdsApp.SharedKernel.Search;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Hr;

public record DepartmentDto(int Id, string Name, string? Description, int EmployeeCount, DateTime CreatedAt);

[RequiresPermission(Permissions.Hr.View)]
public record ListDepartmentsQuery(PagedRequest Request) : IRequest<PagedResponse<DepartmentDto>>;

public class ListDepartmentsQueryHandler(ApplicationDbContext db) : IRequestHandler<ListDepartmentsQuery, PagedResponse<DepartmentDto>>
{
    public Task<PagedResponse<DepartmentDto>> Handle(ListDepartmentsQuery request, CancellationToken ct)
    {
        var query = db.Set<Department>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Request.Filter))
        {
            var pattern = LikePattern.Contains(request.Request.Filter);
            query = query.Where(d => EF.Functions.Like(d.Name, pattern, LikePattern.Escape));
        }

        var sortableColumns = new Dictionary<string, System.Linq.Expressions.Expression<Func<Department, object>>>
        {
            ["name"] = d => d.Name,
            ["createdAt"] = d => d.CreatedAt
        };

        return query.ApplyPagingAsync(request.Request, sortableColumns, d =>
            new DepartmentDto(d.Id, d.Name, d.Description, db.Set<Employee>().Count(e => e.DepartmentId == d.Id), d.CreatedAt), ct);
    }
}

/// <summary>Every department, unpaged — for the department picker on the employee form.</summary>
[RequiresPermission(Permissions.Hr.View)]
public record ListAllDepartmentsQuery : IRequest<IReadOnlyList<DepartmentDto>>;

public class ListAllDepartmentsQueryHandler(ApplicationDbContext db) : IRequestHandler<ListAllDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    public async Task<IReadOnlyList<DepartmentDto>> Handle(ListAllDepartmentsQuery request, CancellationToken ct) =>
        await db.Set<Department>().AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Description, 0, d.CreatedAt)).ToListAsync(ct);
}

[RequiresPermission(Permissions.Hr.Create)]
public record CreateDepartmentCommand(string Name, string? Description) : IRequest<int>, IAuditableCommand;

public class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class CreateDepartmentCommandHandler(ApplicationDbContext db) : IRequestHandler<CreateDepartmentCommand, int>
{
    public async Task<int> Handle(CreateDepartmentCommand request, CancellationToken ct)
    {
        var department = new Department { Id = 0, Name = request.Name, Description = request.Description };
        db.Set<Department>().Add(department);
        await db.SaveChangesAsync(ct);
        return department.Id;
    }
}

[RequiresPermission(Permissions.Hr.Edit)]
public record UpdateDepartmentCommand(int Id, string Name, string? Description) : IRequest, IAuditableCommand;

public class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class UpdateDepartmentCommandHandler(ApplicationDbContext db) : IRequestHandler<UpdateDepartmentCommand>
{
    public async Task Handle(UpdateDepartmentCommand request, CancellationToken ct)
    {
        var department = await db.Set<Department>().FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new NotFoundException("Department not found.");

        department.Name = request.Name;
        department.Description = request.Description;
        await db.SaveChangesAsync(ct);
    }
}

[RequiresPermission(Permissions.Hr.Delete)]
public record DeleteDepartmentCommand(int Id) : IRequest, IAuditableCommand;

public class DeleteDepartmentCommandHandler(ApplicationDbContext db) : IRequestHandler<DeleteDepartmentCommand>
{
    public async Task Handle(DeleteDepartmentCommand request, CancellationToken ct)
    {
        var department = await db.Set<Department>().FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new NotFoundException("Department not found.");

        if (await db.Set<Employee>().AnyAsync(e => e.DepartmentId == request.Id, ct))
            throw new ConflictException("This department still has employees. Move them to another department first.");

        db.Set<Department>().Remove(department);
        await db.SaveChangesAsync(ct);
    }
}
