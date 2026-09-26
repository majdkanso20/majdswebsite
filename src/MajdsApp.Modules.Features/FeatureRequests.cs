using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Features;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Features;

public record FeatureDto(string Name, string DisplayName, string? Description, bool IsEnabled, bool DefaultEnabled);

[RequiresPermission(Permissions.Features.View)]
public record ListFeaturesQuery : IRequest<IReadOnlyList<FeatureDto>>;

public class ListFeaturesQueryHandler(IFeatureChecker features) : IRequestHandler<ListFeaturesQuery, IReadOnlyList<FeatureDto>>
{
    public async Task<IReadOnlyList<FeatureDto>> Handle(ListFeaturesQuery request, CancellationToken ct)
    {
        var enabled = (await features.GetEnabledAsync(ct)).ToHashSet();
        return FeatureDefinitionRegistry.GetAll().OrderBy(f => f.DisplayName)
            .Select(f => new FeatureDto(f.Name, f.DisplayName, f.Description, enabled.Contains(f.Name), f.DefaultEnabled)).ToList();
    }
}

/// <summary>Names of enabled features, for the SPA to hide disabled modules. No permission: it only
/// reveals which modules exist and are on, never any data.</summary>
public record GetEnabledFeaturesQuery : IRequest<IReadOnlyCollection<string>>, ICacheableQuery
{
    /// <summary>The same answer for everyone, and asked by every page load, so it is cached (P4 FR-XC-003) and dropped the moment a feature is switched.</summary>
    public const string Key = "features.enabled";

    public string CacheKey => Key;
    public int AbsoluteExpirationSeconds => 60;
}

public class GetEnabledFeaturesQueryHandler(IFeatureChecker features) : IRequestHandler<GetEnabledFeaturesQuery, IReadOnlyCollection<string>>
{
    public Task<IReadOnlyCollection<string>> Handle(GetEnabledFeaturesQuery request, CancellationToken ct) =>
        features.GetEnabledAsync(ct);
}

[RequiresPermission(Permissions.Features.Edit)]
public record SetFeatureCommand(string Name, bool Enabled) : IRequest, IAuditableCommand;

public class SetFeatureCommandValidator : AbstractValidator<SetFeatureCommand>
{
    public SetFeatureCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty()
            .Must(n => FeatureDefinitionRegistry.Find(n) is not null).WithMessage("Unknown feature.");
    }
}

public class SetFeatureCommandHandler(ApplicationDbContext db, IFeatureChecker features, MajdsApp.SharedKernel.Caching.ICacheService cache) : IRequestHandler<SetFeatureCommand>
{
    public async Task Handle(SetFeatureCommand request, CancellationToken ct)
    {
        var definition = FeatureDefinitionRegistry.Find(request.Name)!;
        var existing = await db.Set<FeatureOverride>().FirstOrDefaultAsync(f => f.Name == request.Name, ct);

        if (request.Enabled == definition.DefaultEnabled)
        {
            if (existing is not null) db.Set<FeatureOverride>().Remove(existing);
        }
        else if (existing is null)
            db.Set<FeatureOverride>().Add(new FeatureOverride { Name = request.Name, IsEnabled = request.Enabled });
        else
            existing.IsEnabled = request.Enabled;

        await db.SaveChangesAsync(ct);
        features.Invalidate();
        cache.Remove($"query:{GetEnabledFeaturesQuery.Key}"); // the cached answer to GetEnabledFeaturesQuery
    }
}
