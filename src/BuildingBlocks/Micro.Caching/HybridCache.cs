using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Micro.Caching;
// HybridCache.cs
public sealed class HybridCache : IHybridCache
{
    private readonly ICacheLayer[] _cacheLayers;
    private readonly ILogger<HybridCache> _logger;
    private readonly CacheDiagnostics _diagnostics;
    private readonly ICachePolicyManager _policyManager;
    private readonly IHybridCacheSerializer _serializer;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();
    private bool _disposed;

    public HybridCache(
        IEnumerable<ICacheLayer> cacheLayers,
        ILogger<HybridCache> logger,
        CacheDiagnostics diagnostics,
        ICachePolicyManager policyManager,
        IHybridCacheSerializer serializer)
    {
        _cacheLayers = cacheLayers.OrderBy(l => l.Order).ToArray();
        _logger = logger;
        _diagnostics = diagnostics;
        _policyManager = policyManager;
        _serializer = serializer;
    }

    public async ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CacheOptions? options = null,
        CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var policy = _policyManager.GetPolicy(key);
        options = MergeWithPolicy(options, policy);

        // Try to get from cache first
        var (value, foundInLayer) = await TryGetFromLayersAsync<T>(key, policy, token).ConfigureAwait(false);
        if (foundInLayer is not null)
        {
            _diagnostics.RecordHit(foundInLayer.Name);
            return value!;
        }

        _diagnostics.RecordMiss();

        // Cache miss - acquire key lock to prevent stampede
        var keyLock = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(token).ConfigureAwait(false);

        try
        {
            // Double-check cache after acquiring lock
            (value, foundInLayer) = await TryGetFromLayersAsync<T>(key, policy, token).ConfigureAwait(false);
            if (foundInLayer is not null)
            {
                _diagnostics.RecordHit(foundInLayer.Name);
                return value!;
            }

            // Execute factory method to get the value
            value = await factory(token).ConfigureAwait(false);

            // Set in all applicable cache layers
            await SetInLayersAsync(key, value, options, policy, token).ConfigureAwait(false);

            return value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _diagnostics.RecordError();
            _logger.LogError(ex, "Error in GetOrCreateAsync for key {Key}", key);
            throw;
        }
        finally
        {
            keyLock.Release();
            _keyLocks.TryRemove(key, out _);
        }
    }

    public async ValueTask<T?> GetAsync<T>(string key, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var policy = _policyManager.GetPolicy(key);
        var (value, foundInLayer) = await TryGetFromLayersAsync<T>(key, policy, token).ConfigureAwait(false);
        if (foundInLayer is not null)
        {
            _diagnostics.RecordHit(foundInLayer.Name);
            return value;
        }
        _diagnostics.RecordMiss();
        return default;
    }

    public async ValueTask SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var policy = _policyManager.GetPolicy(key);
        options = MergeWithPolicy(options, policy);
        await SetInLayersAsync(key, value, options, policy, token).ConfigureAwait(false);
    }

    public async ValueTask RemoveAsync(string key, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(key);

        foreach (var layer in _cacheLayers)
        {
            if (!layer.IsAvailable) continue;
            try
            {
                await layer.RemoveAsync(key, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _diagnostics.RecordError();
                _logger.LogError(ex, "Error removing key {Key} from layer {Layer}", key, layer.Name);
            }
        }
    }

    public async ValueTask RefreshAsync(string key, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(key);

        foreach (var layer in _cacheLayers)
        {
            if (!layer.IsAvailable) continue;
            try
            {
                // For demonstration, just re-set the value if present
                var value = await layer.GetAsync<byte[]>(key, token).ConfigureAwait(false);
                if (value is not null)
                {
                    await layer.SetAsync(key, value, null, token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _diagnostics.RecordError();
                _logger.LogError(ex, "Error refreshing key {Key} in layer {Layer}", key, layer.Name);
            }
        }
    }

    public async ValueTask<bool> ExistsAsync(string key, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(key);

        foreach (var layer in _cacheLayers)
        {
            if (!layer.IsAvailable) continue;
            try
            {
                if (await layer.ExistsAsync(key, token).ConfigureAwait(false))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _diagnostics.RecordError();
                _logger.LogError(ex, "Error checking existence of key {Key} in layer {Layer}", key, layer.Name);
            }
        }
        return false;
    }

    public ValueTask<CacheStats> GetStatsAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ValueTask.FromResult(_diagnostics.GetStats());
    }

    public async ValueTask ClearRegionAsync(string regionPrefix, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(regionPrefix);

        foreach (var layer in _cacheLayers)
        {
            if (!layer.IsAvailable) continue;
            try
            {
                await layer.ClearRegionAsync(regionPrefix, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _diagnostics.RecordError();
                _logger.LogError(ex, "Error clearing region {Region} in layer {Layer}", regionPrefix, layer.Name);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;

        foreach (var (_, keyLock) in _keyLocks)
        {
            keyLock.Dispose();
        }
        _keyLocks.Clear();

        foreach (var layer in _cacheLayers.OfType<IAsyncDisposable>())
        {
            await layer.DisposeAsync().ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        foreach (var (_, keyLock) in _keyLocks)
        {
            keyLock.Dispose();
        }
        _keyLocks.Clear();

        foreach (var layer in _cacheLayers)
        {
            layer.Dispose();
        }
    }

    // Private helper methods...

    private async ValueTask<(T? value, ICacheLayer? foundInLayer)> TryGetFromLayersAsync<T>(string key, CachePolicy policy, CancellationToken token)
    {
        foreach (var layer in _cacheLayers)
        {
            if (!layer.IsAvailable) continue;
            try
            {
                var value = await layer.GetAsync<T>(key, token).ConfigureAwait(false);
                if (value is not null)
                {
                    return (value, layer);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _diagnostics.RecordError();
                _logger.LogError(ex, "Error getting key {Key} from layer {Layer}", key, layer.Name);
            }
        }
        return (default, null);
    }

    private async ValueTask SetInLayersAsync<T>(string key, T value, CacheOptions? options, CachePolicy policy, CancellationToken token)
    {
        foreach (var layer in _cacheLayers)
        {
            if (!layer.IsAvailable) continue;
            try
            {
                await layer.SetAsync(key, value, options, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _diagnostics.RecordError();
                _logger.LogError(ex, "Error setting key {Key} in layer {Layer}", key, layer.Name);
            }
        }
    }

    private static CacheOptions MergeWithPolicy(CacheOptions? options, CachePolicy policy)
    {
        if (options is null && policy == CachePolicy.Default)
            return new CacheOptions();

        return new CacheOptions
        {
            AbsoluteExpirationRelativeToNow = options?.AbsoluteExpirationRelativeToNow ?? policy.MemoryExpiration,
            SlidingExpiration = options?.SlidingExpiration ?? policy.SlidingExpiration,
            Region = options?.Region
        };
    }
}

