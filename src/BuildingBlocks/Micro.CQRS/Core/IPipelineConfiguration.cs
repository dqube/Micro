namespace Micro.CQRS.Core;

// Pipeline configuration
public interface IPipelineConfiguration
{
    PipelineType Type { get; }
    IEnumerable<Type> BehaviorTypes { get; }
}
