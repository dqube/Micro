using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Micro.Caching;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHybridCache(
        this IServiceCollection services,
        Action<HybridCacheOptions> configureOptions)
    {
        var options = new HybridCacheOptions();
        configureOptions(options);

        // Register core services
        services.AddMemoryCache();
        services.AddSingleton<CacheDiagnostics>();
        services.AddSingleton<ICachePolicyManager, CachePolicyManager>();

        // Register serializer (System.Text.Json by default)
        services.AddSingleton<IHybridCacheSerializer>(
            options.Serializer ?? new SystemTextJsonSerializer());

        // Register Redis if configured
        if (options.RedisConfiguration is not null)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(options.RedisConfiguration));

            services.AddSingleton<ICacheLayer, RedisCacheLayer>(sp =>
                new RedisCacheLayer(
                    sp.GetRequiredService<IConnectionMultiplexer>(),
                    sp.GetRequiredService<IHybridCacheSerializer>(),
                    sp.GetService<ILogger<RedisCacheLayer>>() ?? NullLogger<RedisCacheLayer>.Instance,
                    options.RedisDatabase));
        }

        // Register memory cache layer
        services.AddSingleton<ICacheLayer, MemoryCacheLayer>(sp =>
            new MemoryCacheLayer(
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetService<ILogger<MemoryCacheLayer>>() ?? NullLogger<MemoryCacheLayer>.Instance));

        // Register main hybrid cache service
        services.AddSingleton<IHybridCache, HybridCache>();

        return services;
    }
}
