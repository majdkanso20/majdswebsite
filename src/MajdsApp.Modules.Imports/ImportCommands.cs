using MajdsApp.SharedKernel.Mapping;
using MajdsApp.SharedKernel.Data;
using System.Text.Json;
using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Export;
using MajdsApp.SharedKernel.Import;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Imports;

public record ImportJobDto(
    Guid Id, string Source, string Title, string FileName, string Status, int Total, int Succeeded,
    IReadOnlyList<ImportRowError>? Errors, string? Error, DateTime CreatedAt, DateTime? CompletedAt);

/// <summary>Queues a background import of an uploaded file (FR-EXP-003/004). The permission is the source's own
/// (the resource's create permission), checked here while the request still knows who is asking.</summary>
[RequiresFeature("Imports")]
public record StartImportCommand(string Source, byte[] Content, string FileName) : IRequest<ImportJobDto>, IAuditableCommand;

public class StartImportCommandValidator : AbstractValidator<StartImportCommand>
{
    public StartImportCommandValidator()
    {
        RuleFor(x => x.Source).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.Content).Must(c => c.Length > 0 && c.Length <= TabularReader.MaxBytes)
            .WithMessage($"The file must be no larger than {TabularReader.MaxBytes / (1024 * 1024)} MB.");
    }
}

public class StartImportCommandHandler(
    ApplicationDbContext db, IEnumerable<IImportSource> sources, IPermissionChecker permissions, ICurrentUser currentUser, IObjectMapper mapper)
    : IRequestHandler<StartImportCommand, ImportJobDto>
{
    private const int MaxActivePerUser = 3;

    public async Task<ImportJobDto> Handle(StartImportCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var source = sources.FirstOrDefault(s => s.Key.Equals(request.Source, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException($"There is no '{request.Source}' import.");

        if (source.Permission is not null && !await permissions.HasPermissionAsync(source.Permission, ct))
            throw new ForbiddenException($"Missing permission: {source.Permission}");

        var active = await db.Set<ImportJob>().CountAsync(
            j => j.UserId == userId && (j.Status == ImportJobStatus.Pending || j.Status == ImportJobStatus.Running), ct);
        if (active >= MaxActivePerUser)
            throw new ConflictException($"You already have {MaxActivePerUser} imports in progress. Wait for one to finish.");

        var job = new ImportJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = currentUser.UserName,
            Source = source.Key,
            Title = source.Title,
            FileName = request.FileName,
            Content = request.Content,
            Status = ImportJobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.Set<ImportJob>().Add(job);
        await db.SaveChangesAsync(ct);

        return ToDto(job);
    }

    internal static ImportJobDto ToDto(ImportJob job) => new(
        job.Id, job.Source, job.Title, job.FileName, job.Status.ToString(), job.Total, job.Succeeded,
        job.Errors is null ? null : JsonSerializer.Deserialize<List<ImportRowError>>(job.Errors), job.Error, job.CreatedAt, job.CompletedAt);
}

/// <summary>The caller's own recent imports, newest first. Always scoped to the caller, so it needs no permission.</summary>
[RequiresFeature("Imports")]
public record ListMyImportsQuery : IRequest<IReadOnlyList<ImportJobDto>>;

public class ListMyImportsQueryHandler(IReadRepository<ImportJob, Guid> importJobs, ICurrentUser currentUser)
    : IRequestHandler<ListMyImportsQuery, IReadOnlyList<ImportJobDto>>
{
    public async Task<IReadOnlyList<ImportJobDto>> Handle(ListMyImportsQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var jobs = await importJobs.Query().Where(j => j.UserId == userId).OrderByDescending(j => j.CreatedAt).Take(50).ToListAsync(ct);
        return jobs.Select(StartImportCommandHandler.ToDto).ToList();
    }
}

/// <summary>The blank template for one import source (FR-EXP-005), the same shape every source declares through <see cref="IImportSource.TemplateColumns"/>.</summary>
public record GetImportTemplateQuery(string Source, string? Format) : IRequest<ExportFile>;

public class GetImportTemplateQueryHandler(IEnumerable<IImportSource> sources) : IRequestHandler<GetImportTemplateQuery, ExportFile>
{
    public Task<ExportFile> Handle(GetImportTemplateQuery request, CancellationToken ct)
    {
        var source = sources.FirstOrDefault(s => s.Key.Equals(request.Source, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException($"There is no '{request.Source}' import.");
        return Task.FromResult(ImportTemplate.Create(ExportFormats.Parse(request.Format), source.Key, source.TemplateColumns));
    }
}
