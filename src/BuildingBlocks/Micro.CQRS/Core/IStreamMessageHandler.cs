

namespace Micro.CQRS.Core;

public interface IStreamMessageHandler<in TMessage>
    where TMessage : IStreamMessage
{
    IAsyncEnumerable<object> Handle(TMessage message, CancellationToken ct);
}
