using System.ComponentModel;

namespace MajdsApp.SharedKernel.Api;

public enum ResponseStatusCode
{
    [Description("Operation completed successfully.")]
    Success = 200,

    [Description("The request was invalid.")]
    ValidationError = 400,

    [Description("Authentication is required.")]
    Unauthorized = 401,

    [Description("You do not have permission to perform this action.")]
    Forbidden = 403,

    [Description("The requested resource was not found.")]
    NotFound = 404,

    [Description("The request could not be completed due to a conflict.")]
    Conflict = 409,

    [Description("Too many requests. Please wait a moment and try again.")]
    TooManyRequests = 429,

    [Description("An unexpected error occurred.")]
    Error = 500
}
