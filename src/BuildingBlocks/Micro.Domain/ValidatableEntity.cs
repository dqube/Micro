namespace Micro.Domain;

public abstract class ValidatableEntity<TId, TValue> : Entity<TId, TValue>
    where TId : IIdentity<TValue>
    where TValue : notnull
{
    protected ValidatableEntity(TId id) : base(id) { }

    protected void Validate(ValidationResult validationResult)
    {
        if (!validationResult.IsValid)
            throw new DomainValidationException(validationResult.Errors);
    }
}
