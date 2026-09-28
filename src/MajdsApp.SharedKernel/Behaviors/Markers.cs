namespace MajdsApp.SharedKernel.Behaviors;

/// <summary>Opt a query into the caching behavior (FR-XC-003). Implement <see cref="CacheKey"/> to
/// build a stable, parameter-inclusive key, and <see cref="AbsoluteExpirationSeconds"/> for its TTL.</summary>
public interface ICacheableQuery
{
    string CacheKey { get; }
    int AbsoluteExpirationSeconds => 60;
}

/// <summary>Opt a command into the transaction behavior (FR-XC-003): wraps the handler in a UoW commit,
/// rolling back on any exception.</summary>
public interface ITransactionalCommand;

/// <summary>Opt a command into the audit log (F-Audit): <see cref="AuditBehavior{TRequest,TResponse}"/>
/// records it after it succeeds. Separate from <see cref="ITransactionalCommand"/> on purpose — some
/// commands need a DB transaction but aren't an audit-worthy admin action (a health-check ping), and
/// some are audit-worthy without needing one (ASP.NET Identity's stores already self-save).</summary>
public interface IAuditableCommand;

/// <summary>Declares the permission required to execute a request (FR-XC-003, F-Authorization).
/// On a request, checked by <see cref="AuthorizationBehavior{TRequest,TResponse}"/> before the handler runs; on a controller or an action, checked by
/// <see cref="Api.PermissionActionFilter"/> before the action runs.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequiresPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}
