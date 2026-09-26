using MajdsApp.SharedKernel.Data;
using MajdsApp.SharedKernel.Mapping;
using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Security;
using MajdsApp.SharedKernel.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Files;

public record FileDto(Guid Id, string FileName, string ContentType, long Size, string? OwnerName, DateTime CreatedAt);

[RequiresFeature("Files")]
[RequiresPermission(Permissions.Files.Upload)]
public record UploadFileCommand(string FileName, string ContentType, long Size, Stream Content) : IRequest<FileDto>, IAuditableCommand;

public class UploadFileCommandHandler(
    ApplicationDbContext db, FileStorage storage, ISettingsProvider settings, ICurrentUser currentUser, IObjectMapper mapper)
    : IRequestHandler<UploadFileCommand, FileDto>
{
    // Executable/script types that have no business being uploaded to a shared store.
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".com", ".msi", ".ps1", ".vbs", ".js", ".sh", ".scr", ".jar"
    };

    public async Task<FileDto> Handle(UploadFileCommand request, CancellationToken ct)
    {
        var fileName = Path.GetFileName(request.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ValidationException("A file name is required.");

        if (request.Size <= 0)
            throw new ValidationException("The file is empty.");

        var maxMb = await settings.GetIntegerAsync("Files.MaxUploadMb", ct);
        if (request.Size > maxMb * 1024L * 1024L)
            throw new ValidationException($"The file exceeds the maximum upload size of {maxMb} MB.");

        if (BlockedExtensions.Contains(Path.GetExtension(fileName)))
            throw new ValidationException($"Files of type '{Path.GetExtension(fileName)}' are not allowed.");

        var record = new FileRecord
        {
            Id = Guid.NewGuid(),
            FileName = fileName.Length > 260 ? fileName[^260..] : fileName,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            Size = request.Size,
            StoredName = Guid.NewGuid().ToString("N"),
            OwnerId = currentUser.UserId,
            OwnerName = currentUser.UserName,
            CreatedAt = DateTime.UtcNow
        };

        await storage.SaveAsync(record.StoredName, request.Content, ct);
        try
        {
            db.Set<FileRecord>().Add(record);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            storage.Delete(record.StoredName);
            throw;
        }

        return mapper.Map<FileDto>(record);
    }
}

[RequiresFeature("Files")]
public record DeleteFileCommand(Guid FileId) : IRequest, IAuditableCommand;

public class DeleteFileCommandHandler(
    IRepository<FileRecord, Guid> files, IUnitOfWork unitOfWork, ICurrentUser currentUser, IPermissionChecker permissions)
    : IRequestHandler<DeleteFileCommand>
{
    public async Task Handle(DeleteFileCommand request, CancellationToken ct)
    {
        var record = await files.GetByIdAsync(request.FileId, ct)
            ?? throw new NotFoundException("File not found.");

        var isOwner = record.OwnerId is not null && record.OwnerId == currentUser.UserId;
        if (!isOwner && !await permissions.HasPermissionAsync(Permissions.Files.Delete, ct))
            throw new ForbiddenException($"Missing permission '{Permissions.Files.Delete}'.");

        // Soft delete (FR-FILE-006): the file disappears at once, its bytes stay until PurgeDeletedFilesJob removes them after the retention period.
        record.DeletedAt = DateTime.UtcNow;
        record.DeletedBy = currentUser.UserId;
        files.Update(record);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
