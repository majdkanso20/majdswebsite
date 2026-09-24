using FluentValidation;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Data;
using MediatR;

namespace MajdsApp.Modules.Diagnostics;

/// <summary>Implements <see cref="ITransactionalCommand"/> so the TransactionBehavior (P4) commits it.</summary>
public record RecordPingCommand(string Message) : IRequest<Guid>, ITransactionalCommand;

public class RecordPingCommandValidator : AbstractValidator<RecordPingCommand>
{
    public RecordPingCommandValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(256);
    }
}

public class RecordPingCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<RecordPingCommand, Guid>
{
    public async Task<Guid> Handle(RecordPingCommand request, CancellationToken ct)
    {
        var ping = new DiagnosticsPing { Id = Guid.NewGuid(), Message = request.Message };
        var repository = unitOfWork.Repository<DiagnosticsPing, Guid>();
        await repository.AddAsync(ping, ct);
        // No explicit SaveChangesAsync here: the TransactionBehavior (P4) commits via the UoW
        // after the handler returns, so this handler contains only use-case logic (cross-cutting standard #5).
        return ping.Id;
    }
}
