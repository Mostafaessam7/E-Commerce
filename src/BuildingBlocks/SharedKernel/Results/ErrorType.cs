namespace SharedKernel.Results;

/// <summary>
/// Classifies a failed <see cref="Result"/> so a single, generic HTTP-mapping middleware
/// (Phase 1's global exception/result handler) can turn it into the right status code without
/// every controller switching on error codes by hand.
/// </summary>
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
}
