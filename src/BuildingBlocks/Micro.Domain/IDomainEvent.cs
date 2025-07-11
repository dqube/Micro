namespace Micro.Domain;

// YourCompany.DDD.Abstractions/IDomainEvent.cs
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
