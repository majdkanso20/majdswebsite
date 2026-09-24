using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Features;
using MediatR;

namespace MajdsApp.SharedKernel.Behaviors;

/// <summary>Blocks a request whose module has been switched off (F-Features): put
/// <c>[RequiresFeature("Files")]</c> on the request and no handler checks flags itself.</summary>
[AttributeUsage(AttributeTargets.Class)]
public class RequiresFeatureAttribute(string feature) : Attribute
{
    public string Feature { get; } = feature;
}

public class FeatureBehavior<TRequest, TResponse>(IFeatureChecker features) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var attribute = typeof(TRequest).GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true)
            .OfType<RequiresFeatureAttribute>().FirstOrDefault();

        if (attribute is not null && !await features.IsEnabledAsync(attribute.Feature, ct))
            throw new ForbiddenException($"The '{attribute.Feature}' feature is disabled.");

        return await next(ct);
    }
}
