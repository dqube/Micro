namespace Micro.Domain;

// YourCompany.DDD.Abstractions/IIdentity.cs
public interface IIdentity<T>
    where T : notnull
{
    T Value { get; }
}
//// Example usage
//public record ProductId(int Value) : Identity<int>(Value);
//public record CustomerId(Guid Value) : Identity<Guid>(Value);