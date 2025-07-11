using Microsoft.Extensions.Logging;
using Polly;

namespace Micro.CQRS.Core;

public class CircuitBreakerPipelineBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ICircuitBreakerState _circuitBreakerState;
    private readonly ILogger<CircuitBreakerPipelineBehavior<TMessage, TResponse>> _logger;

    public CircuitBreakerPipelineBehavior(
        ICircuitBreakerState circuitBreakerState,
        ILogger<CircuitBreakerPipelineBehavior<TMessage, TResponse>> logger)
    {
        _circuitBreakerState = circuitBreakerState;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (message is not ICircuitBreakerConfigurable configurable)
        {
            return await next(message, cancellationToken);
        }

        var breaker = _circuitBreakerState.GetOrAdd(
            typeof(TMessage).Name,
            () => Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    configurable.ExceptionsAllowedBeforeBreaking,
                    TimeSpan.FromSeconds(configurable.BreakDurationInSeconds),
                    (ex, breakDuration) =>
                    {
                        _logger.LogWarning(
                            ex,
                            "Circuit breaker opened for {MessageType} for {BreakDuration} seconds",
                            typeof(TMessage).Name,
                            breakDuration.TotalSeconds);
                    },
                    () =>
                    {
                        _logger.LogInformation(
                            "Circuit breaker reset for {MessageType}",
                            typeof(TMessage).Name);
                    },
                    () =>
                    {
                        _logger.LogDebug(
                            "Circuit breaker half-opened for {MessageType}",
                            typeof(TMessage).Name);
                    }));

        return await breaker.ExecuteAsync(() => next(message, cancellationToken));
    }
}
