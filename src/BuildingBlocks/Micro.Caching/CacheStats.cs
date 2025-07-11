namespace Micro.Caching;

// Models/CacheStats.cs
public sealed record CacheStats
{
    public required long TotalRequests { get; init; }
    public required long Hits { get; init; }
    public required long Misses { get; init; }
    public required long Fallbacks { get; init; }
    public required long Errors { get; init; }
    public required long StampedePreventions { get; init; }
    public required double HitRatio { get; init; }
    public required double ErrorRate { get; init; }
    public required Dictionary<string, long> LayerHits { get; init; }
}
