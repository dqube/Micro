using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Micro.Caching;

public class MemoryCacheLayer : ICacheLayer
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MemoryCacheLayer> _logger;

    public string Name => "Memory";
    public int Order => 1;
    public bool IsAvailable => true;

    public MemoryCacheLayer(IMemoryCache memoryCache, ILogger<MemoryCacheLayer> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public ValueTask<T?> GetAsync<T>(string key, CancellationToken token = default)
    {
        if (_memoryCache.TryGetValue(key, out var value) && value is T tValue)
        {
            return ValueTask.FromResult<T?>(tValue);
        }
        return ValueTask.FromResult<T?>(default);
    }

    public ValueTask SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken token = default)
    {
        var cacheEntryOptions = new MemoryCacheEntryOptions();
        if (options != null)
        {
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
                cacheEntryOptions.SetAbsoluteExpiration(options.AbsoluteExpirationRelativeToNow.Value);
            if (options.SlidingExpiration.HasValue)
                cacheEntryOptions.SetSlidingExpiration(options.SlidingExpiration.Value);
        }
        _memoryCache.Set(key, value, cacheEntryOptions);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string key, CancellationToken token = default)
    {
        _memoryCache.Remove(key);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ExistsAsync(string key, CancellationToken token = default)
    {
        return ValueTask.FromResult(_memoryCache.TryGetValue(key, out _));
    }

    public ValueTask ClearRegionAsync(string regionPrefix, CancellationToken token = default)
    {
        // IMemoryCache does not support regions natively.
        // This is a no-op or could be implemented with a custom solution if needed.
        _logger.LogWarning("ClearRegionAsync is not supported for MemoryCacheLayer.");
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_memoryCache is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_memoryCache is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }
        Dispose();
        return ValueTask.CompletedTask;
    }
}
