using System.ComponentModel.DataAnnotations.Schema;
using MediatR;

namespace MajdsApp.SharedKernel.Data;

/// <summary>Something that happened to an entity that other parts of the application may react to (P3 FR-REPO-005). Handle it with a MediatR <c>INotificationHandler</c>.</summary>
public interface IDomainEvent : INotification;

/// <summary>An entity that records domain events as it changes. The events are published after the save that stored the change succeeds, never before, and never when it fails.</summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

/// <summary>Convenience base for an entity that raises domain events: call <see cref="Raise"/> where the change happens.</summary>
public abstract class DomainEventSource : IHasDomainEvents
{
    private readonly List<IDomainEvent> _events = [];

    [NotMapped]
    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    protected void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);

    public void ClearDomainEvents() => _events.Clear();
}
