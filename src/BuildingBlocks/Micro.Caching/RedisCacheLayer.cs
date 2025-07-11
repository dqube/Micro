using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Micro.Caching;

// RedisCacheLayer.cs
public sealed class RedisCacheLayer : ICacheLayer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly IHybridCacheSerializer _serializer;
    private readonly ILogger<RedisCacheLayer> _logger;
    private bool _disposed;

    public string Name => "Redis";
    public int Order => 2;
    public bool IsAvailable => !_disposed && _redis.IsConnected;

    public RedisCacheLayer(
        IConnectionMultiplexer redis,
        IHybridCacheSerializer serializer,
        ILogger<RedisCacheLayer> logger,
        int databaseNumber = 0)
    {
        _redis = redis;
        _database = redis.GetDatabase(databaseNumber);
        _serializer = serializer;
        _logger = logger;
    }

    public async ValueTask<T?> GetAsync<T>(string key, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        token.ThrowIfCancellationRequested();

        try
        {
            var redisValue = await _database.StringGetAsync(key).WaitAsync(token).ConfigureAwait(false);
            return redisValue.HasValue ? _serializer.Deserialize<T>(redisValue!) : default;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis error getting key {Key}", key);
            return default;
        }
    }


    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _redis.DisposeAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _redis.Dispose();
    }

    public ValueTask SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        token.ThrowIfCancellationRequested();

        try
        {
            var bytes = _serializer.Serialize(value);
            TimeSpan? expiry = options?.AbsoluteExpirationRelativeToNow;
            // Redis does not natively support sliding expiration, so only absolute is handled
            var task = _database.StringSetAsync(key, bytes, expiry);
            return new ValueTask(task.WaitAsync(token));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis error setting key {Key}", key);
            return ValueTask.CompletedTask;
        }
    }

    public ValueTask RemoveAsync(string key, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        token.ThrowIfCancellationRequested();

        try
        {
            var task = _database.KeyDeleteAsync(key);
            return new ValueTask(task.WaitAsync(token));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis error removing key {Key}", key);
            return ValueTask.CompletedTask;
        }
    }

    public async ValueTask<bool> ExistsAsync(string key, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        token.ThrowIfCancellationRequested();

        try
        {
            var exists = await _database.KeyExistsAsync(key).WaitAsync(token).ConfigureAwait(false);
            return exists;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis error checking existence of key {Key}", key);
            return false;
        }
    }

    public async ValueTask ClearRegionAsync(string regionPrefix, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        token.ThrowIfCancellationRequested();

        try
        {
            // Redis does not support region natively, so we use key pattern matching
            // WARNING: This uses KEYS command, which is not recommended for production on large datasets
            var endpoints = _redis.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _redis.GetServer(endpoint);
                var keys = server.Keys(_database.Database, $"{regionPrefix}*").ToArray();
                if (keys.Length > 0)
                {
                    await _database.KeyDeleteAsync(keys).WaitAsync(token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis error clearing region with prefix {RegionPrefix}", regionPrefix);
        }
    }
}