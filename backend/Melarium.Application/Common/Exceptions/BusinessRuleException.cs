namespace Melarium.Application.Common.Exceptions;

/// <summary>Thrown when an operation is not allowed in the current state.</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}
