using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace MajdsApp.SharedKernel.Security;

public class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => User?.Identity?.Name;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
