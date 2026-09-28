using Mapster;
using MajdsApp.Data;

namespace MajdsApp.Modules.Users;

/// <summary>How an account becomes a <see cref="UserDto"/> (P4 FR-XC-007): members match by name, except the two that are worked out. The roles come from another table, so
/// they start empty and the handler fills them in for a whole page at once.</summary>
public class UsersMapping : IRegister
{
    public void Register(TypeAdapterConfig config) =>
        config.NewConfig<ApplicationUser, UserDto>()
            .Map(d => d.LockedOut, s => s.LockoutEnd != null && s.LockoutEnd > DateTimeOffset.UtcNow)
            .Map(d => d.Roles, s => new List<string>());
}
