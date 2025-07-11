 Usage Example
csharp
// In your Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHybridCache(options =>
{
    options.RedisConfiguration = builder.Configuration.GetConnectionString("Redis");
    options.DefaultMemoryExpiration = TimeSpan.FromMinutes(10);
});

// In your service
public sealed class ProductService(IHybridCache cache)
{
    public async ValueTask<Product?> GetProductAsync(int id, CancellationToken ct = default)
    {
        var cacheKey = $"products:{id}";
        return await cache.GetOrCreateAsync(cacheKey, async token =>
        {
            // Database or API call here
            return await _repository.GetProductAsync(id, token);
        }, cancellationToken: ct);
    }
}
Key Improvements for .NET 9.0
Records: Used for immutable models

ValueTask: Improved async performance

CancellationToken: Proper support throughout

IAsyncDisposable: Full async cleanup

Nullability: Full nullable reference types support

System.Text.Json: Modern JSON serialization

Throw helpers: Using new throw helpers like ThrowIf

ConfigureAwait(false): Consistent async patterns

Sealed classes: Better performance

Primary constructors: Where applicable

This implementation provides a high-performance, modern hybrid caching solution for .NET 9.0 applications with Redis support and proper async patterns.