using System.Collections.Concurrent;

namespace Micro.Caching;

// Diagnostics/CacheDiagnostics.cs
public class CacheDiagnostics
{
    private long _totalRequests;
    private long _hits;
    private long _misses;
    private long _fallbacks;
    private long _errors;
    private long _stampedePreventions;
    private readonly ConcurrentDictionary<string, long> _layerHits = new();
    private readonly object _syncLock = new();

    public void RecordHit(string layerName)
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _hits);
        _layerHits.AddOrUpdate(layerName, 1, (_, count) => count + 1);
    }

    public void RecordMiss()
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _misses);
    }

    public void RecordFallback()
    {
        Interlocked.Increment(ref _fallbacks);
    }

    public void RecordError()
    {
        Interlocked.Increment(ref _errors);
    }

    public void RecordStampedePrevention()
    {
        Interlocked.Increment(ref _stampedePreventions);
    }

    public CacheStats GetStats()
    {
        lock (_syncLock)
        {
            return new CacheStats
            {
                TotalRequests = _totalRequests,
                Hits = _hits,
                Misses = _misses,
                Fallbacks = _fallbacks,
                Errors = _errors,
                StampedePreventions = _stampedePreventions,
                HitRatio = _totalRequests > 0 ? _hits / (double)_totalRequests : 0,
                ErrorRate = _totalRequests > 0 ? _errors / (double)_totalRequests : 0,
                LayerHits = new Dictionary<string, long>(_layerHits)
            };
        }
    }

    public void Reset()
    {
        lock (_syncLock)
        {
            _totalRequests = 0;
            _hits = 0;
            _misses = 0;
            _fallbacks = 0;
            _errors = 0;
            _stampedePreventions = 0;
            _layerHits.Clear();
        }
    }
}