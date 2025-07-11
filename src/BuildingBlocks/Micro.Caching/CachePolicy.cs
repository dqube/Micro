namespace Micro.Caching;

public sealed record CachePolicy
{
    public TimeSpan? MemoryExpiration { get; init; }
    public TimeSpan? DistributedExpiration { get; init; }
    public TimeSpan? SlidingExpiration { get; init; }
    public bool SkipMemoryCache { get; init; }
    public bool SkipDistributedCache { get; init; }
    public int Priority { get; init; } = 100;

    public static CachePolicy Default { get; } = new();
}
