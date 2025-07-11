using Microsoft.Extensions.Logging;
using Polly;

namespace Micro.CQRS.Core;

public class RetryPolicyPipelineBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ILogger<RetryPolicyPipelineBehavior<TMessage, TResponse>> _logger;

    public RetryPolicyPipelineBehavior(
        ILogger<RetryPolicyPipelineBehavior<TMessage, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (message is not IRetryable retryable)
        {
            return await next(message, cancellationToken);
        }

        var policy = Policy
            .Handle<Exception>(ex => retryable.IsRetryableException(ex))
            .WaitAndRetryAsync(
                retryable.RetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, delay, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount} of {MaxRetries} for {MessageType} after {Delay}ms",
                        retryCount,
                        retryable.RetryCount,
                        typeof(TMessage).Name,
                        delay.TotalMilliseconds);
                });

        return await policy.ExecuteAsync(() => next(message, cancellationToken));
    }
}
