using System.Diagnostics;

namespace Micro.CQRS.Core;

public class TracingPipelineBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private static readonly ActivitySource ActivitySource = new("CQRS");

    public async Task<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity($"Handle {typeof(TMessage).Name}");
        activity?.AddTag("message.type", typeof(TMessage).Name);

        try
        {
            return await next(message, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
