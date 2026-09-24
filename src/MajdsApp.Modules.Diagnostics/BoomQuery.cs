using MediatR;

namespace MajdsApp.Modules.Diagnostics;

/// <summary>Deliberately throws — proves the exception-handling middleware (F-Errors) maps an
/// unhandled exception to a safe ResponseDto with no stack trace leaked to the client.</summary>
public record BoomQuery : IRequest<string>;

public class BoomQueryHandler : IRequestHandler<BoomQuery, string>
{
    public Task<string> Handle(BoomQuery request, CancellationToken ct) =>
        throw new InvalidOperationException("Deliberate diagnostic failure.");
}
