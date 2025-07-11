namespace Micro.Domain;

// YourCompany.DDD.Validation/ValidationResult.cs
public class ValidationResult
{
    private readonly List<ValidationError> _errors = new();

    public bool IsValid => !_errors.Any();
    public IReadOnlyCollection<ValidationError> Errors => _errors.AsReadOnly();

    public void AddError(string propertyName, string errorMessage)
    {
        _errors.Add(new ValidationError(propertyName, errorMessage));
    }

    public void AddErrors(IEnumerable<ValidationError> errors)
    {
        _errors.AddRange(errors);
    }
}
