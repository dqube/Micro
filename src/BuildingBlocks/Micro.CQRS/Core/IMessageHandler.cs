

namespace Micro.CQRS.Core;

public interface IMessageHandler<in TMessage>
    where TMessage : IMessage
{
    Task Handle(TMessage message, CancellationToken ct);
}
