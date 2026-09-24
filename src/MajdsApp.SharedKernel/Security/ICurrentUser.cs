namespace MajdsApp.SharedKernel.Security;

/// <summary>Identity of the caller, sourced from the existing auth's token/cookie — never re-implemented here.</summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
}
