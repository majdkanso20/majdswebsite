using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Features;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Search;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Search;

public class SearchModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Providers are discovered by ModuleRegistrar's ISearchProvider scan.
    }
}

/// <summary>Runs every search provider the caller may use and concatenates the hits. Access is decided
/// here, per provider (permission and feature flag), so a provider can't leak data to someone who
/// couldn't open its own list page. No [RequiresPermission]: the result set itself is the filter.</summary>
public record SearchQuery(string Term) : IRequest<IReadOnlyList<SearchResult>>;

public class SearchQueryHandler(
    IEnumerable<ISearchProvider> providers, IPermissionChecker permissions, IFeatureChecker features, ICurrentUser currentUser)
    : IRequestHandler<SearchQuery, IReadOnlyList<SearchResult>>
{
    private const int PerProvider = 5;

    public async Task<IReadOnlyList<SearchResult>> Handle(SearchQuery request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedAppException("Authentication is required.");

        var term = request.Term.Trim();
        if (term.Length < 2 || term.Length > 100)
            return [];

        var results = new List<SearchResult>();
        foreach (var provider in providers.OrderBy(p => p.Category))
        {
            if (provider.RequiredFeature is not null && !await features.IsEnabledAsync(provider.RequiredFeature, ct))
                continue;
            if (provider.RequiredPermission is not null && !await permissions.HasPermissionAsync(provider.RequiredPermission, ct))
                continue;

            results.AddRange(await provider.SearchAsync(term, PerProvider, ct));
        }

        return results;
    }
}

[Authorize]
public class SearchController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(MajdsApp.SharedKernel.Security.RateLimitPolicies.Expensive)]
    public async Task<ResponseDto<IReadOnlyList<SearchResult>>> Search([FromQuery] string q) =>
        Ok(await mediator.Send(new SearchQuery(q ?? string.Empty)));
}
