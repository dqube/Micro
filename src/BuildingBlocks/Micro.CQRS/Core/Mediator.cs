using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Micro.CQRS.Core;


// Delegate for the next handler in the pipeline
public delegate Task<TResponse> MessageHandlerDelegate<in TMessage, TResponse>(
    TMessage message,
    CancellationToken cancellationToken)
    where TMessage : IMessage;


// Define the StreamHandlerDelegate delegate
public delegate IAsyncEnumerable<object> StreamHandlerDelegate<in TMessage>(
    TMessage message,
    CancellationToken cancellationToken)
    where TMessage : IStreamMessage;



public class Mediator : IMediator
{
    private readonly Channel<CommandEnvelope> _commandChannel;
    private readonly Channel<StreamEnvelope> _streamChannel;
    private readonly IServiceProvider _services;
    private readonly ILogger<Mediator> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<Type, object> _handlerCache = new();

    public Mediator(
        IServiceProvider services,
        ILogger<Mediator> logger,
        int capacity = 1000)
    {
        _services = services;
        _logger = logger;

        _commandChannel = Channel.CreateBounded<CommandEnvelope>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = false
            });

        _streamChannel = Channel.CreateBounded<StreamEnvelope>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = false
            });

        Task.Run(() => ProcessCommands(_cts.Token));
        Task.Run(() => ProcessStreams(_cts.Token));
    }

    public async Task<TResponse> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<object>();
        var envelope = new CommandEnvelope(command, tcs, typeof(TResponse));

        await _commandChannel.Writer.WriteAsync(envelope, ct);
        var result = await tcs.Task.WaitAsync(ct);
        return (TResponse)result!;
    }

    public Task Send(
        ICommand command,
        CancellationToken ct = default)
    {
        return Send<Unit>((ICommand<Unit>)command, ct);
    }

    public Task<TResponse> Query<TResponse>(
        IQuery<TResponse> query,
        CancellationToken ct = default)
    {
        return Send<TResponse>((ICommand<TResponse>)query, ct);
    }

    public async Task Publish<TEvent>(
        TEvent @event,
        CancellationToken ct = default)
        where TEvent : IEvent
    {
        var handlers = _services.GetServices<IEventHandler<TEvent>>();
        await Task.WhenAll(handlers.Select(handler =>
            handler.Handle(@event, ct)));
    }

    public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamMessage message,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var responseChannel = Channel.CreateUnbounded<object>();
        var envelope = new StreamEnvelope(message, responseChannel, typeof(TResponse));

        await _streamChannel.Writer.WriteAsync(envelope, ct);

        await foreach (var item in responseChannel.Reader.ReadAllAsync(ct))
        {
            yield return (TResponse)item!;
        }
    }

    private async Task ProcessCommands(CancellationToken ct)
    {
        await foreach (var envelope in _commandChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                var handler = GetHandler(envelope.Command.GetType(), envelope.ResponseType);
                var pipeline = CreatePipeline(envelope.Command.GetType(), envelope.ResponseType);
                var result = await pipeline(envelope.Command, handler, ct);
                envelope.Completion.SetResult(result);
            }
            catch (Exception ex)
            {
                envelope.Completion.SetException(ex);
                _logger.LogError(ex, "Error processing command");
            }
        }
    }

    private async Task ProcessStreams(CancellationToken ct)
    {
        await foreach (var envelope in _streamChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                var handler = GetStreamHandler(envelope.Message.GetType());
                var pipeline = CreateStreamPipeline(envelope.Message.GetType());

                await foreach (var item in pipeline(envelope.Message, handler, ct))
                {
                    if (item?.GetType() == envelope.ResponseType)
                    {
                        await envelope.ResponseChannel.Writer.WriteAsync(item, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing stream");
            }
            finally
            {
                envelope.ResponseChannel.Writer.Complete();
            }
        }
    }

    private MessageHandlerDelegate<IMessage, object> GetHandler(Type messageType, Type responseType)
    {
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(messageType, responseType);
        var handler = _services.GetRequiredService(handlerType);

        return async (msg, ct) =>
        {
            var handleMethod = handlerType.GetMethod("Handle")
                ?? throw new InvalidOperationException("Handle method not found");

            var result = handleMethod.Invoke(handler, new object[] { msg, ct });

            return await (result is Task task
                ? AwaitTask(task, responseType)
                : Task.FromResult((object)result!));
        };
    }

    private async Task<object> AwaitTask(Task task, Type resultType)
    {
        await task;
        return resultType == typeof(Unit)
            ? Unit.Value
            : task.GetType().GetProperty("Result")?.GetValue(task) ?? throw new InvalidOperationException("Task result is null");
    }

    private StreamHandlerDelegate<IStreamMessage> GetStreamHandler(Type messageType)
    {
        var handlerType = typeof(IStreamMessageHandler<>).MakeGenericType(messageType);
        var handler = _services.GetRequiredService(handlerType);

        return (msg, ct) =>
        {
            var handleMethod = handlerType.GetMethod("Handle")
                ?? throw new InvalidOperationException("Handle method not found");

            var result = handleMethod.Invoke(handler, new object[] { msg, ct });
            if (result is not IAsyncEnumerable<object> asyncEnumerable)
            {
                throw new InvalidOperationException("Handle method returned null or invalid type.");
            }
            return asyncEnumerable;
        };
    }

    private Func<IMessage, MessageHandlerDelegate<IMessage, object>, CancellationToken, Task<object>>
        CreatePipeline(Type messageType, Type responseType)
    {
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(messageType, responseType);
        var behaviors = _services.GetServices(behaviorType)
            .Cast<IPipelineBehavior<IMessage, object>>()
            .Reverse()
            .ToList();

        return (msg, handler, ct) =>
        {
            var pipeline = behaviors.Aggregate(handler, (next, behavior) =>
                (m, c) => behavior.Handle(m, next, c));

            return pipeline(msg, ct);
        };
    }

    private Func<IStreamMessage, StreamHandlerDelegate<IStreamMessage>, CancellationToken, IAsyncEnumerable<object>>
        CreateStreamPipeline(Type messageType)
    {
        var behaviorType = typeof(IStreamPipelineBehavior<>).MakeGenericType(messageType);
        var behaviors = _services.GetServices(behaviorType)
            .Cast<IStreamPipelineBehavior<IStreamMessage>>()
            .Reverse()
            .ToList();

        return (msg, handler, ct) =>
        {
            var pipeline = behaviors.Aggregate(handler, (next, behavior) =>
                (m, c) => behavior.Handle(m, next, c));

            return pipeline(msg, ct);
        };
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _commandChannel.Writer.Complete();
        _streamChannel.Writer.Complete();
        await Task.WhenAll(
            _commandChannel.Reader.Completion,
            _streamChannel.Reader.Completion);
        _cts.Dispose();
    }

    private record CommandEnvelope(
        IMessage Command,
        TaskCompletionSource<object> Completion,
        Type ResponseType);

    private record StreamEnvelope(
        IStreamMessage Message,
        Channel<object> ResponseChannel,
        Type ResponseType);
}
