

using Micro.CQRS.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace CQRS.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChannelMediator(
        this IServiceCollection services,
        Action<MediatorOptions> configure)
    {
        var options = new MediatorOptions();
        configure(options);
        services.AddSingleton<IPipelineRegistry, PipelineRegistry>();
        services.AddSingleton<IMediator, Mediator>();

        foreach (var assembly in options.AssembliesToScan)
        {
            RegisterHandlers(services, assembly);
        }

        RegisterPipelineBehaviors(services);

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t.GetInterfaces().Any(IsHandlerInterface))
            .ToList();

        foreach (var type in handlerTypes)
        {
            foreach (var interfaceType in type.GetInterfaces().Where(IsHandlerInterface))
            {
                services.AddTransient(interfaceType, type);
            }
        }
    }

    private static bool IsHandlerInterface(Type type)
    {
        if (!type.IsGenericType) return false;

        var typeDefinition = type.GetGenericTypeDefinition();
        return typeDefinition == typeof(ICommandHandler<,>) ||
               typeDefinition == typeof(IQueryHandler<,>) ||
               typeDefinition == typeof(IEventHandler<>) ||
               typeDefinition == typeof(IStreamMessageHandler<>);
    }

    private static void RegisterPipelineBehaviors(IServiceCollection services)
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
       // services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RetryPolicyPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CircuitBreakerPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionPipelineBehavior<,>));
       // services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceMonitoringPipelineBehavior<,>));

       // services.AddTransient(typeof(IStreamPipelineBehavior<>), typeof(StreamLoggingBehavior<>));
    }
}

public class MediatorOptions
{
    public List<Assembly> AssembliesToScan { get; } = new();

    public MediatorOptions AddAssembly(Assembly assembly)
    {
        AssembliesToScan.Add(assembly);
        return this;
    }

    public MediatorOptions AddAssemblyOf<T>()
    {
        AssembliesToScan.Add(typeof(T).Assembly);
        return this;
    }
}

//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container
//builder.Services.AddCQRS(options =>
//{
//    // Register assemblies containing your handlers
//    options.AddAssembly(typeof(Program).Assembly);
//    options.AddAssembly(typeof(CreateUserCommand).Assembly);
//});

//// Configure pipelines after AddCQRS
//builder.Services.AddTransient<IPipelineRegistry>(provider =>
//{
//    var registry = new PipelineRegistry(builder.Services);

//    // Configure default pipelines
//    registry.ConfigurePipeline<ICommand<object>>(config =>
//    {
//        config.BehaviorTypes.Add(typeof(LoggingPipelineBehavior<,>));
//        config.BehaviorTypes.Add(typeof(ValidationPipelineBehavior<,>));
//        config.BehaviorTypes.Add(typeof(TransactionPipelineBehavior<,>));
//    });

//    registry.ConfigurePipeline<IQuery<object>>(config =>
//    {
//        config.BehaviorTypes.Add(typeof(LoggingPipelineBehavior<,>));
//        config.BehaviorTypes.Add(typeof(CachingPipelineBehavior<,>));
//    });

//    registry.ConfigurePipeline<IStreamMessage>(config =>
//    {
//        config.BehaviorTypes.Add(typeof(StreamLoggingBehavior<>));
//    });

//    return registry;
//});

//public static IServiceCollection AddConfiguredPipelines(this IServiceCollection services)
//{
//    services.AddTransient<IPipelineRegistry>(provider =>
//    {
//        var registry = new PipelineRegistry(services);

//        registry.ConfigurePipeline<ICommand<object>>(config =>
//        {
//            config.BehaviorTypes.AddRange(new[]
//            {
//                typeof(TracingPipelineBehavior<,>),
//                typeof(LoggingPipelineBehavior<,>),
//                typeof(ValidationPipelineBehavior<,>),
//                typeof(TransactionPipelineBehavior<,>)
//            });
//        });

//        // Other pipeline configurations...

//        return registry;
//    });

//    return services;
//}

//builder.Services.AddCQRS(options =>
//{
//    options.AddAssembly(typeof(Program).Assembly);
//})
//    .AddConfiguredPipelines();

//// appsettings.json
//{
//    "PipelineConfigurations": {
//        "CommandPipeline": [
//          "CQRS.Infrastructure.PipelineBehaviors.LoggingPipelineBehavior`2",
//      "CQRS.Infrastructure.PipelineBehaviors.ValidationPipelineBehavior`2"
//        ],
//    "QueryPipeline": [
//      "CQRS.Infrastructure.PipelineBehaviors.CachingPipelineBehavior`2"
//    ]
//    }
//}

//// Pipeline configuration service
//builder.Services.AddTransient<IPipelineRegistry>(provider =>
//{
//    var registry = new PipelineRegistry(builder.Services);
//    var config = provider.GetRequiredService<IConfiguration>();

//    var commandBehaviors = config.GetSection("PipelineConfigurations:CommandPipeline")
//        .Get<string[]>();

//    foreach (var behavior in commandBehaviors)
//    {
//        var behaviorType = Type.GetType(behavior);
//        if (behaviorType != null)
//        {
//            registry.ConfigurePipeline<ICommand<object>>(cfg =>
//                cfg.BehaviorTypes.Add(behaviorType));
//        }
//    }

//    // Similar for other pipeline types...

//    return registry;
//});