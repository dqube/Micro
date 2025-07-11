using Microsoft.Extensions.Logging;

namespace Micro.CQRS.Core;

public class CachingPipelineBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ICacheProvider _cacheProvider;
    private readonly ILogger<CachingPipelineBehavior<TMessage, TResponse>> _logger;

    public CachingPipelineBehavior(
        ICacheProvider cacheProvider,
        ILogger<CachingPipelineBehavior<TMessage, TResponse>> logger)
    {
        _cacheProvider = cacheProvider;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (message is not ICacheable cacheable)
        {
            return await next(message, cancellationToken);
        }

        var cacheKey = cacheable.CacheKey;
        var cachedResponse = await _cacheProvider.GetAsync<TResponse>(cacheKey, cancellationToken);

        if (cachedResponse != null)
        {
            _logger.LogDebug("Cache hit for {CacheKey}", cacheKey);
            return cachedResponse;
        }

        _logger.LogDebug("Cache miss for {CacheKey}", cacheKey);
        var response = await next(message, cancellationToken);

        await _cacheProvider.SetAsync(
            cacheKey,
            response,
            cacheable.CacheDuration,
            cancellationToken);

        return response;
    }
}
