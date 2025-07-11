//using System.ComponentModel.DataAnnotations;

//namespace Micro.CQRS.Core;

//// PerformanceBehavior.cs
//public class ValidationPipelineBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
//    where TMessage : IMessage
//{
//    private readonly IEnumerable<IValidator<TMessage>> _validators;

//    public ValidationPipelineBehavior(IEnumerable<IValidator<TMessage>> validators)
//    {
//        _validators = validators;
//    }

//    public async Task<TResponse> Handle(
//        TMessage message,
//        MessageHandlerDelegate<TMessage, TResponse> next,
//        CancellationToken cancellationToken)
//    {
//        if (!_validators.Any()) return await next(message, cancellationToken);

//        // Remove generic argument from ValidationContext
//        var context = new ValidationContext(message);

//        var validationResults = await Task.WhenAll(
//            _validators.Select(v => v.ValidateAsync(message, cancellationToken)));

//        var failures = validationResults
//            .SelectMany(r => r.Errors)
//            .Where(f => f != null)
//            .ToList();

//        if (failures.Count != 0)
//        {
//            throw new ValidationException(failures);
//        }

//        return await next(message, cancellationToken);
//    }
//}
