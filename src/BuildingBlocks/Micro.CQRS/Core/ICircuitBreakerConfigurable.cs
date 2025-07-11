namespace Micro.CQRS.Core;

public interface ICircuitBreakerConfigurable
{
    int ExceptionsAllowedBeforeBreaking { get; }
    int BreakDurationInSeconds { get; }
}
