namespace MajdsApp.Modules.Users;

public record UserDto(
    string Id,
    string Email,
    string? FullName,
    string? PhoneNumber,
    bool IsActive,
    bool EmailConfirmed,
    bool LockedOut,
    bool TwoFactorEnabled,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt);
