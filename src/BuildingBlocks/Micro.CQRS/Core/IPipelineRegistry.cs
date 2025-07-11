using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Micro.CQRS.Core;

// Pipeline registry
public interface IPipelineRegistry
{
    PipelineConfiguration GetConfiguration(Type messageType);
    void ConfigurePipeline(Type messageType, Action<PipelineConfiguration> configure);
    void ConfigurePipeline<TMessage>(Action<PipelineConfiguration> configure) where TMessage : IMessage;
}

public class PipelineConfiguration
{
    public List<Type> BehaviorTypes { get; } = new List<Type>();
    public PipelineType PipelineType { get; set; } = PipelineType.CommandPipeline;
}

public class PipelineRegistry : IPipelineRegistry
{
    private readonly ConcurrentDictionary<Type, PipelineConfiguration> _configurations = new();
    private readonly IServiceCollection _services;

    public PipelineRegistry(IServiceCollection services)
    {
        _services = services;
    }

    public PipelineConfiguration GetConfiguration(Type messageType)
    {
        return _configurations.GetOrAdd(messageType, _ => new PipelineConfiguration());
    }

    public void ConfigurePipeline(Type messageType, Action<PipelineConfiguration> configure)
    {
        var config = GetConfiguration(messageType);
        configure(config);
        RegisterBehaviorTypes(config.BehaviorTypes);
    }

    public void ConfigurePipeline<TMessage>(Action<PipelineConfiguration> configure)
        where TMessage : IMessage
    {
        ConfigurePipeline(typeof(TMessage), configure);
    }

    private void RegisterBehaviorTypes(IEnumerable<Type> behaviorTypes)
    {
        foreach (var behaviorType in behaviorTypes)
        {
            if (behaviorType.IsGenericTypeDefinition)
            {
                // Register open generic types
                if (typeof(IPipelineBehavior<,>).IsAssignableFrom(behaviorType.GetGenericTypeDefinition()))
                {
                    _services.AddTransient(typeof(IPipelineBehavior<,>), behaviorType);
                }
                else if (typeof(IStreamPipelineBehavior<>).IsAssignableFrom(behaviorType.GetGenericTypeDefinition()))
                {
                    _services.AddTransient(typeof(IStreamPipelineBehavior<>), behaviorType);
                }
            }
            else
            {
                // Register concrete types
                if (!_services.Any(x => x.ServiceType == behaviorType))
                {
                    _services.AddTransient(behaviorType);
                }
            }
        }
    }
}