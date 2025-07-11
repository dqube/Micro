namespace Micro.Domain;
public abstract class AggregateRoot<TId, TKey> : Entity<TId, TKey>
    where TId : IIdentity<TKey>
    where TKey : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot(TId id) : base(id) { }

    protected void AddDomainEvent(IDomainEvent eventItem) => _domainEvents.Add(eventItem);

    public void ClearDomainEvents() => _domainEvents.Clear();
}