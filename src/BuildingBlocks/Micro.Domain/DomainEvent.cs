namespace Micro.Domain;

// YourCompany.DDD.Core/DomainEvent.cs
public abstract class DomainEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
