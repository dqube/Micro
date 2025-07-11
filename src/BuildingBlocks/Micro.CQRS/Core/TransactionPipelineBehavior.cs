using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Micro.CQRS.Core;

public class TransactionPipelineBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly DbContext _dbContext;
    private readonly ILogger<TransactionPipelineBehavior<TMessage, TResponse>> _logger;

    public TransactionPipelineBehavior(
        DbContext dbContext,
        ILogger<TransactionPipelineBehavior<TMessage, TResponse>> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (message is not ITransactional)
        {
            return await next(message, cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        _logger.LogInformation("Beginning transaction for {MessageType}", typeof(TMessage).Name);

        try
        {
            var response = await next(message, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Transaction committed for {MessageType}", typeof(TMessage).Name);
            return response;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Transaction rolled back for {MessageType}", typeof(TMessage).Name);
            throw;
        }
    }
}
