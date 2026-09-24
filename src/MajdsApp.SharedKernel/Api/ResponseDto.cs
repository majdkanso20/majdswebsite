namespace MajdsApp.SharedKernel.Api;

/// <summary>
/// Non-generic marker so the result filter (<see cref="ResponseStatusCodeFilter"/>) can read
/// <see cref="Code"/> off any closed <see cref="ResponseDto{T}"/> without reflection.
/// </summary>
public interface IResponseDto
{
    ResponseStatusCode Code { get; }
}

public partial class ResponseDto<T> : IResponseDto
{
    public ResponseStatusCode Code { get; set; }

    private string? _message;
    public string? Message
    {
        get => _message ?? Code.GetDescription() ?? string.Empty;
        internal set => _message = value;
    }

    public T? Data { get; set; }

    public IEnumerable<string>? Errors { get; set; }
}

/// <summary>
/// DRY construction helpers for <see cref="ResponseDto{T}"/> (P1). Controllers/behaviors should
/// build responses through these rather than constructing the envelope by hand.
/// </summary>
public static class ResponseDto
{
    public static ResponseDto<T> Ok<T>(T data, string? message = null) => new()
    {
        Code = ResponseStatusCode.Success,
        Data = data,
        Message = message
    };

    public static ResponseDto<T> Fail<T>(ResponseStatusCode code, string? message = null, IEnumerable<string>? errors = null) => new()
    {
        Code = code,
        Message = message,
        Errors = errors
    };
}
