using MajdsApp.SharedKernel.Api;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MajdsApp.Modules.ApiDocs;

/// <summary>
/// Documents the answers every endpoint can give besides success (FR-API-008, F-ApiDocs): 400, 401, 403, 404 and 429 are all the same <c>ResponseDto</c> envelope, with
/// the reason in <c>message</c> and the details in <c>errors</c>. Without this an operation lists only its 200, and a client generated from the document cannot know the
/// error shape. An action that declares one of these itself keeps its own description.
/// </summary>
public class ErrorResponsesOperationFilter : IOperationFilter
{
    private static readonly (string Code, string Meaning)[] Errors =
    [
        ("400", "The request was not valid. The reasons are in errors."),
        ("401", "Nobody is signed in, or the token is missing or has expired."),
        ("403", "The signed-in user lacks the permission the operation needs."),
        ("404", "What was asked for does not exist."),
        ("429", "Too many requests; wait, then try again (see Retry-After).")
    ];

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var schema = context.SchemaGenerator.GenerateSchema(typeof(ResponseDto<object>), context.SchemaRepository);
        operation.Responses ??= [];

        foreach (var (code, meaning) in Errors)
        {
            if (operation.Responses.ContainsKey(code)) continue;
            operation.Responses[code] = new OpenApiResponse
            {
                Description = meaning,
                Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new OpenApiMediaType { Schema = schema } }
            };
        }
    }
}
