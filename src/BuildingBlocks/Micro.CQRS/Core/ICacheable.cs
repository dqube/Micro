namespace Micro.CQRS.Core;

public interface ICacheable
{
    string CacheKey { get; }
    TimeSpan CacheDuration { get; }
}
