namespace Micro.Domain;

// YourCompany.DDD.Core/Identity.cs
public abstract record Identity<T>(T Value) : IIdentity<T>
    where T : notnull
{
    public static implicit operator T(Identity<T> id) => id.Value;

    public override string ToString() => Value.ToString() ?? string.Empty;
}
