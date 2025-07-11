namespace Micro.Http;

public class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; } = Guid.NewGuid().ToString();
}
