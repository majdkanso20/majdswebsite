using MajdsApp.SharedKernel.Files;
using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Files;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Account;

// Every request here acts on the *caller's own* account (resolved from ICurrentUser), so none carry
// [RequiresPermission] — being signed in is the whole access rule, and no user id is ever accepted
// from the client, which rules out editing someone else's account through these endpoints.

public record ProfileDto(
    string Id, string Email, string? FullName, string? PhoneNumber, bool HasPicture, bool TwoFactorEnabled,
    IReadOnlyList<string> Roles);

internal static class CurrentAccount
{
    public static async Task<ApplicationUser> RequireAsync(UserManager<ApplicationUser> users, ICurrentUser current)
    {
        var id = current.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        return await users.FindByIdAsync(id) ?? throw new NotFoundException("Account not found.");
    }
}

public record GetMyProfileQuery : IRequest<ProfileDto>;

public class GetMyProfileQueryHandler(UserManager<ApplicationUser> users, ICurrentUser current)
    : IRequestHandler<GetMyProfileQuery, ProfileDto>
{
    public async Task<ProfileDto> Handle(GetMyProfileQuery request, CancellationToken ct)
    {
        var user = await CurrentAccount.RequireAsync(users, current);
        return new ProfileDto(user.Id, user.Email ?? string.Empty, user.FullName, user.PhoneNumber,
            user.ProfilePictureId is not null, user.TwoFactorEnabled, (await users.GetRolesAsync(user)).ToList());
    }
}

public record UpdateMyProfileCommand(string? FullName, string? PhoneNumber) : IRequest, IAuditableCommand;

public class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.FullName).MaximumLength(200);
        RuleFor(x => x.PhoneNumber).MaximumLength(50);
    }
}

public class UpdateMyProfileCommandHandler(UserManager<ApplicationUser> users, ICurrentUser current)
    : IRequestHandler<UpdateMyProfileCommand>
{
    public async Task Handle(UpdateMyProfileCommand request, CancellationToken ct)
    {
        var user = await CurrentAccount.RequireAsync(users, current);
        user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}

public record ChangeMyPasswordCommand(string CurrentPassword, string NewPassword) : IRequest, IAuditableCommand;

public class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}

public class ChangeMyPasswordCommandHandler(UserManager<ApplicationUser> users, ICurrentUser current)
    : IRequestHandler<ChangeMyPasswordCommand>
{
    public async Task Handle(ChangeMyPasswordCommand request, CancellationToken ct)
    {
        var user = await CurrentAccount.RequireAsync(users, current);
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}

/// <summary>Stores the picture with F-Files' storage/metadata and points the user at it, replacing any previous one.</summary>
public record SetMyPictureCommand(string ContentType, long Size, Stream Content) : IRequest, IAuditableCommand;

public class SetMyPictureCommandHandler(
    UserManager<ApplicationUser> users, ICurrentUser current, ApplicationDbContext db, IFileStorage storage)
    : IRequestHandler<SetMyPictureCommand>
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = ["image/png", "image/jpeg", "image/webp"];

    public async Task Handle(SetMyPictureCommand request, CancellationToken ct)
    {
        if (!AllowedTypes.Contains(request.ContentType))
            throw new ValidationException("The picture must be a PNG, JPEG or WebP image.");
        if (request.Size <= 0 || request.Size > MaxBytes)
            throw new ValidationException("The picture must be between 1 byte and 2 MB.");

        var user = await CurrentAccount.RequireAsync(users, current);
        var record = new FileRecord
        {
            Id = Guid.NewGuid(),
            FileName = "profile-picture",
            ContentType = request.ContentType,
            Size = request.Size,
            StoredName = Guid.NewGuid().ToString("N"),
            OwnerId = user.Id,
            OwnerName = user.Email,
            CreatedAt = DateTime.UtcNow
        };

        await storage.SaveAsync(record.StoredName, request.Content, ct);

        var previousId = user.ProfilePictureId;
        db.Set<FileRecord>().Add(record);
        user.ProfilePictureId = record.Id;
        var result = await users.UpdateAsync(user); // saves the new record and the user together
        if (!result.Succeeded)
        {
            storage.Delete(record.StoredName);
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (previousId is not null)
        {
            var old = await db.Set<FileRecord>().FirstOrDefaultAsync(f => f.Id == previousId, ct);
            if (old is not null)
            {
                db.Set<FileRecord>().Remove(old);
                await db.SaveChangesAsync(ct);
                storage.Delete(old.StoredName);
            }
        }
    }
}

public record MyPicture(string ContentType, Stream Content);

public record GetMyPictureQuery : IRequest<MyPicture>;

public class GetMyPictureQueryHandler(
    UserManager<ApplicationUser> users, ICurrentUser current, ApplicationDbContext db, IFileStorage storage)
    : IRequestHandler<GetMyPictureQuery, MyPicture>
{
    public async Task<MyPicture> Handle(GetMyPictureQuery request, CancellationToken ct)
    {
        var user = await CurrentAccount.RequireAsync(users, current);
        var record = user.ProfilePictureId is null
            ? null
            : await db.Set<FileRecord>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == user.ProfilePictureId, ct);

        return record is null
            ? throw new NotFoundException("No profile picture.")
            : new MyPicture(record.ContentType, storage.Open(record.StoredName));
    }
}
