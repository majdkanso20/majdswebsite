namespace MajdsApp.Modules.Roles;

public record RoleDto(string Id, string Name, string? DisplayName, bool IsStatic, bool IsDefault, int UserCount);
