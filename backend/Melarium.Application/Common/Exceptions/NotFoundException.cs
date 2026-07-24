namespace Melarium.Application.Common.Exceptions;

/// <summary>Thrown when a requested resource cannot be found in the data store.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string name, object key)
        : base($"Entity '{name}' with key '{key}' was not found.") { }
}
