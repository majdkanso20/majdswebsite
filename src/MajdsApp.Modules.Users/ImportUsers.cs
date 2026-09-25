using System.Security.Cryptography;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Export;
using MajdsApp.SharedKernel.Import;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

/// <summary>Creates users from an uploaded CSV or Excel file (FR-EXP-003). Each row goes through the same
/// <see cref="CreateUserCommand"/> as the create endpoint, so it obeys the same validation and is audited the same
/// way. Valid rows are created; invalid rows are reported with their row number and reason (AC-EXP-2).</summary>
[RequiresPermission(Permissions.Users.Create)]
public record ImportUsersCommand(byte[] Content, string FileName) : IRequest<ImportResult>;

public class ImportUsersCommandHandler(IMediator mediator, ApplicationDbContext db) : IRequestHandler<ImportUsersCommand, ImportResult>
{
    public async Task<ImportResult> Handle(ImportUsersCommand request, CancellationToken ct)
    {
        using var stream = new MemoryStream(request.Content);
        var table = TabularReader.Read(stream, request.FileName, [UserImportColumns.Email]);

        var roleNames = await db.Roles.AsNoTracking().Select(r => r.Name!).ToListAsync(ct);

        return await ImportRunner.RunAsync(table, async row =>
        {
            var roles = ResolveRoles(row.Get(UserImportColumns.Roles), roleNames);
            var password = row.Get(UserImportColumns.Password);

            await mediator.Send(new CreateUserCommand(
                row.Get(UserImportColumns.Email),
                password.Length > 0 ? password : GeneratePassword(),
                NullIfEmpty(row.Get(UserImportColumns.FullName)),
                roles), ct);
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

    /// <summary>Random and long enough for any password policy; nobody ever sees it, so the user has to use Forgot password.</summary>
    private static string GeneratePassword()
    {
        const string lower = "abcdefghijkmnopqrstuvwxyz", upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", digits = "23456789", symbols = "!@#$%^&*";
        var all = lower + upper + digits + symbols;
        var chars = new List<char>
        {
            lower[RandomNumberGenerator.GetInt32(lower.Length)], upper[RandomNumberGenerator.GetInt32(upper.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)], symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
        };
        while (chars.Count < 24) chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }
}

/// <summary>The blank template for a users import (FR-EXP-005).</summary>
[RequiresPermission(Permissions.Users.View)]
public record GetUsersImportTemplateQuery(string? Format) : IRequest<ExportFile>;

public class GetUsersImportTemplateQueryHandler : IRequestHandler<GetUsersImportTemplateQuery, ExportFile>
{
    public Task<ExportFile> Handle(GetUsersImportTemplateQuery request, CancellationToken ct) =>
        Task.FromResult(ImportTemplate.Create(ExportFormats.Parse(request.Format), "users", UserImportColumns.All));
}
