

namespace Micro.Domain;
public abstract class Entity<TId, TKey>
    where TId : IIdentity<TKey>
    where TKey : notnull
{
    public TId Id { get; protected set; } = default!;

    protected Entity(TId id)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
    }

    // For ORM compatibility
    protected Entity() { }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId, TKey> other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id.Value.Equals(other.Id.Value);
    }

    public override int GetHashCode() =>
        HashCode.Combine(GetType(), Id.Value);

    public static bool operator ==(Entity<TId, TKey>? left, Entity<TId, TKey>? right)
        => Equals(left, right);

    public static bool operator !=(Entity<TId, TKey>? left, Entity<TId, TKey>? right)
        => !Equals(left, right);
}