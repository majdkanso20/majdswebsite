using MediatR;

namespace MajdsApp.Modules.Diagnostics;

public record PingQuery : IRequest<string>;

public class PingQueryHandler : IRequestHandler<PingQuery, string>
{
    public Task<string> Handle(PingQuery request, CancellationToken ct) =>
        Task.FromResult($"pong at {DateTimeOffset.UtcNow:O}");
}
