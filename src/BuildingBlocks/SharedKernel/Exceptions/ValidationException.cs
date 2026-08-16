using SharedKernel.Results;

namespace SharedKernel.Exceptions;

/// <summary>
/// Thrown by the Application-layer validation pipeline behavior (added once CQRS handlers
/// exist, Phase 4+) when a command/query fails FluentValidation checks before it ever reaches
/// its handler. Carries every failure at once so the client gets a single, complete response
/// instead of fixing one field at a time. Maps to HTTP 400 with a field-level breakdown.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyCollection<Error> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyCollection<Error> Errors { get; }
}
