using Polly.CircuitBreaker;

namespace Micro.CQRS.Core;

public interface ICircuitBreakerState
{
    AsyncCircuitBreakerPolicy GetOrAdd(string key, Func<AsyncCircuitBreakerPolicy> policyFactory);
}
