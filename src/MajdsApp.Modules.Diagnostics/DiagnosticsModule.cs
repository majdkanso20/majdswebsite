using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Diagnostics;

/// <summary>
/// Self-registers this module into the host (P2, FR-MOD-001). Nothing extra to configure here —
/// its MediatR handlers and entity configuration are discovered automatically by assembly scan —
/// but the module still declares itself so it shows up in host startup logs/diagnostics.
/// </summary>
public class DiagnosticsModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Intentionally empty: this module has no bespoke services beyond what convention-based
        // scanning (marker interfaces) and MediatR assembly scanning already pick up.
    }
}
