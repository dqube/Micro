namespace Micro.Domain;

public record ValidationError(string PropertyName, string ErrorMessage);
