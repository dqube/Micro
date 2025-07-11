namespace Micro.Domain;

public class DomainValidationException : Exception
{
    public IReadOnlyCollection<ValidationError> Errors { get; }

    public DomainValidationException(IEnumerable<ValidationError> errors)
        : base("Domain validation failed")
    {
        Errors = errors.ToList().AsReadOnly();
    }
}