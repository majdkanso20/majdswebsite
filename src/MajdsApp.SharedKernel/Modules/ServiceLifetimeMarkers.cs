namespace MajdsApp.SharedKernel.Modules;

/// <summary>
/// Implement one of these on a class to have it auto-registered with the matching DI lifetime
/// (FR-MOD-003) by <c>ModuleRegistrar</c>'s assembly scan — no manual <c>services.AddScoped(...)</c>
/// per class. Each interface is registered against every non-marker interface the class implements,
/// plus its own type.
/// </summary>
public interface IScopedService;

public interface ITransientService;

public interface ISingletonService;
