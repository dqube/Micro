using System.ComponentModel.DataAnnotations;

namespace Micro.CQRS.Core;

// Dummy IValidator interface for demonstration
public interface IValidator<T>
{
    Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken);
}
