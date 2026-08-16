namespace SharedKernel.Exceptions;

/// <summary>
/// The requested entity does not exist. Prefer returning <c>Result.Failure(Error.NotFound(...))</c>
/// from a query/command handler when "not found" is a normal, expected outcome the caller
/// checks for; throw this instead only where a Result can't flow (e.g. a route parameter that
/// must resolve to an aggregate before an authorization handler can even run). Maps to HTTP 404.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.")
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }
}
