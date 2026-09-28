using MajdsApp.SharedKernel.Files;
using System.Text.Json;
using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Files;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Export;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Exports;

public record ExportJobDto(
    Guid Id, string Source, string Title, string Format, string Status, Guid? FileId, string? FileName, long? Size,
    string? Error, DateTime CreatedAt, DateTime? CompletedAt);

/// <summary>Queues a background export of one dataset with the list's filters (FR-EXP-004). The permission is the source's own
/// (the resource's view permission), checked here while the request still knows who is asking.</summary>
[RequiresFeature("Files")]
public record StartExportCommand(string Source, string? Format, Dictionary<string, string>? Filters) : IRequest<ExportJobDto>, IAuditableCommand;

public class StartExportCommandValidator : AbstractValidator<StartExportCommand>
{
    public StartExportCommandValidator()
    {
        RuleFor(x => x.Source).NotEmpty();
        RuleFor(x => x.Filters).Must(f => f is null || f.Count <= 10).WithMessage("Too many filters.");
        RuleFor(x => x.Filters).Must(f => f is null || f.All(kv => kv.Key.Length <= 50 && kv.Value.Length <= 200))
            .WithMessage("A filter is too long.");
    }
}

public class StartExportCommandHandler(
    ApplicationDbContext db, IEnumerable<IExportSource> sources, IPermissionChecker permissions, ICurrentUser currentUser)
    : IRequestHandler<StartExportCommand, ExportJobDto>
{
    private const int MaxActivePerUser = 3;

    public async Task<ExportJobDto> Handle(StartExportCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var source = sources.FirstOrDefault(s => s.Key.Equals(request.Source, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException($"There is no '{request.Source}' export.");

        if (source.Permission is not null && !await permissions.HasPermissionAsync(source.Permission, ct))
            throw new ForbiddenException($"Missing permission: {source.Permission}");

        var format = ExportFormats.Parse(request.Format);
        if (format == ExportFormat.Pdf)
            throw new ValidationException("Background exports are CSV or Excel. A PDF is limited to 2,000 rows, so download it directly.");

        var active = await db.Set<ExportJob>().CountAsync(
            j => j.UserId == userId && (j.Status == ExportJobStatus.Pending || j.Status == ExportJobStatus.Running), ct);
        if (active >= MaxActivePerUser)
            throw new ConflictException($"You already have {MaxActivePerUser} exports in progress. Wait for one to finish.");

        var job = new ExportJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = currentUser.UserName,
            Source = source.Key,
            Title = source.Title,
            Format = format == ExportFormat.Xlsx ? "xlsx" : "csv",
            Filters = JsonSerializer.Serialize(request.Filters ?? []),
            Status = ExportJobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.Set<ExportJob>().Add(job);
        await db.SaveChangesAsync(ct);

        return ExportJobMapping.ToDto(job, null);
    }
}

/// <summary>The caller's own recent exports, newest first. Always scoped to the caller, so it needs no permission.</summary>
[RequiresFeature("Files")]
public record ListMyExportsQuery : IRequest<IReadOnlyList<ExportJobDto>>;

public class ListMyExportsQueryHandler(ApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListMyExportsQuery, IReadOnlyList<ExportJobDto>>
{
    public async Task<IReadOnlyList<ExportJobDto>> Handle(ListMyExportsQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var jobs = await db.Set<ExportJob>().AsNoTracking().Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt).Take(25).ToListAsync(ct);

        var fileIds = jobs.Where(j => j.FileId.HasValue).Select(j => j.FileId!.Value).ToList();
        var files = await db.Set<FileRecord>().AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);

        return jobs.Select(j => ExportJobMapping.ToDto(j, j.FileId.HasValue ? files.GetValueOrDefault(j.FileId.Value) : null)).ToList();
    }
}

/// <summary>Streams a finished export. Only the user who asked for it may download it, through this endpoint.</summary>
[RequiresFeature("Files")]
public record DownloadExportQuery(Guid JobId) : IRequest<DownloadedFile>;

public class DownloadExportQueryHandler(ApplicationDbContext db, IFileStorage storage, ICurrentUser currentUser)
    : IRequestHandler<DownloadExportQuery, DownloadedFile>
{
    public async Task<DownloadedFile> Handle(DownloadExportQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var job = await db.Set<ExportJob>().AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.JobId, ct);

        // Someone else's export is reported as missing, so its existence is not revealed.
        if (job is null || job.UserId != userId) throw new NotFoundException("Export not found.");
        if (job.Status != ExportJobStatus.Completed || job.FileId is null) throw new ConflictException("This export is not ready yet.");

        var record = await db.Set<FileRecord>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == job.FileId, ct)
            ?? throw new NotFoundException("The exported file was deleted.");

        return new DownloadedFile(record.FileName, record.ContentType, storage.Open(record.StoredName));
    }
}

internal static class ExportJobMapping
{
    public static ExportJobDto ToDto(ExportJob j, FileRecord? file) => new(
        j.Id, j.Source, j.Title, j.Format, j.Status.ToString(), j.FileId, file?.FileName, file?.Size, j.Error, j.CreatedAt, j.CompletedAt);
}
