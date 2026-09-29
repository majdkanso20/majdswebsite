using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Import;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MajdsApp.Modules.Users;

public static class UserImportColumns
{
    public static readonly IReadOnlyList<ImportColumn> All =
    [
        new("Email", true, "The sign-in address. Must be unique."),
        new("Full name", false, "Display name."),
        new("Roles", false, "Role names separated by semicolons, for example: Admin; User. Leave empty for no role."),
        new("Password", false, "Initial password. Leave empty to generate one; the user then signs in with Forgot password.")
    ];

    public const string Email = "Email", FullName = "Full name", Roles = "Roles", Password = "Password";
}

/// <summary>
/// Creates users from an uploaded CSV or Excel file, running as a background job through the Imports module
/// (F-Export FR-EXP-003/004). Each row goes through <see cref="UserCreator.CreateAsync"/> — the same validation
/// and creation logic <see cref="CreateUserCommand"/>'s own handler uses — but called directly rather than through
/// MediatR: a background worker has no signed-in user for <c>[RequiresPermission]</c> to check against, and the
/// permission was already checked once, while the import was queued (<c>StartImportCommandHandler</c>), the same
/// reason an export's <see cref="MajdsApp.SharedKernel.Export.IExportSource"/> queries the database directly
/// instead of re-sending a permission-gated request. Valid rows are created; invalid rows are reported with
/// their row number and reason (AC-EXP-2).
/// </summary>
public class UsersImportSource(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IConfiguration configuration, IEmailSender<ApplicationUser>? emailSender = null)
    : IImportSource
{
    public string Key => "users";
    public string Title => "Users";
    public string? Permission => Permissions.Users.Create;
    public IReadOnlyList<ImportColumn> TemplateColumns => UserImportColumns.All;

    public async Task<ImportResult> RunAsync(byte[] content, string fileName, CancellationToken ct)
    {
        using var stream = new MemoryStream(content);
        var table = TabularReader.Read(stream, fileName, [UserImportColumns.Email]);

        var roleNames = await db.Roles.AsNoTracking().Select(r => r.Name!).ToListAsync(ct);

        return await ImportRunner.RunAsync(table, async row =>
        {
            var roles = ResolveRoles(row.Get(UserImportColumns.Roles), roleNames);
            var password = row.Get(UserImportColumns.Password);

            // No email column: a random password nobody sees, same as before — a bulk import should not silently start
            // emailing every imported account a set-password link (FR-USER-002's SendSetPasswordEmail is opt-in elsewhere).
            var command = new CreateUserCommand(
                row.Get(UserImportColumns.Email),
                password.Length > 0 ? password : PasswordGenerator.Generate(), false,
                NullIfEmpty(row.Get(UserImportColumns.FullName)),
                roles);
            await UserCreator.CreateAsync(userManager, command, configuration, emailSender, ct);
        }, ct);
    }

    /// <summary>Unknown role names are a row error, detected before the user is created so a row is never half-imported.</summary>
    private static List<string> ResolveRoles(string cell, IReadOnlyList<string> known)
    {
        var resolved = new List<string>();
        foreach (var name in cell.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = known.FirstOrDefault(k => k.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new FluentValidation.ValidationException($"Unknown role '{name}'.");
            if (!resolved.Contains(match)) resolved.Add(match);
        }

        return resolved;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
