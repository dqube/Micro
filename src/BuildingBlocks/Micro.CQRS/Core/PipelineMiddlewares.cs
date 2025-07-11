namespace Micro.CQRS.Core;
public class PipelineMiddlewares<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    private readonly IPerformanceTracker _tracker;

    public PipelineMiddlewares(IPerformanceTracker tracker)
    {
        _tracker = tracker;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _tracker.StartTracking(requestName);

        try
        {
            return await next(request, cancellationToken);
        }
        finally
        {
            _tracker.StopTracking(requestName);
        }
    }
}

// If ICacheProvider is not defined, you need to define it. Here's an example definition:
public interface ICacheProvider
{
    Task<T> GetAsync<T>(string key, CancellationToken cancellationToken);
    Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken);
}
