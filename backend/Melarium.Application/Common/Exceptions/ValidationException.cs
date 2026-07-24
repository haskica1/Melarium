namespace Melarium.Application.Common.Exceptions;

/// <summary>Thrown when input fails business-rule validation.</summary>
public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation failures occurred.")
    {
        Errors = errors;
    }
}
