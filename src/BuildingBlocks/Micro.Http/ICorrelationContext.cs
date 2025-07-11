namespace Micro.Http;

public interface ICorrelationContext
{
    string CorrelationId { get; }
}
