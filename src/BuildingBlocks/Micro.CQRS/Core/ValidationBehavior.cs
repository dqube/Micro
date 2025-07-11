using System.ComponentModel.DataAnnotations;

namespace Micro.CQRS.Core;

// ValidationBehavior.cs
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any()) return await next(request, cancellationToken);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(request, cancellationToken)));
        var failures = validationResults.Where(r => r != ValidationResult.Success).ToList();

        if (failures.Any())
        // Modify the exception throwing line in ValidationBehavior.cs
        throw new ValidationException(failures.First().ErrorMessage);
        return await next(request, cancellationToken);
    }
}
