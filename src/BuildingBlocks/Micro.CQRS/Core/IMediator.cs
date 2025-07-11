

namespace Micro.CQRS.Core;

// Channel-based Mediator interface
public interface IMediator : IAsyncDisposable
{
    Task<TResponse> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken ct = default);

    Task Send(
        ICommand command,
        CancellationToken ct = default);

    Task<TResponse> Query<TResponse>(
        IQuery<TResponse> query,
        CancellationToken ct = default);

    Task Publish<TEvent>(
        TEvent @event,
        CancellationToken ct = default)
        where TEvent : IEvent;

    IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamMessage message,
        CancellationToken ct = default);
}
