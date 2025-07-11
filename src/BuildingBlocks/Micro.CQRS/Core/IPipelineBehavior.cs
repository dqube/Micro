

namespace Micro.CQRS.Core;

public interface IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    Task<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken ct);
}
