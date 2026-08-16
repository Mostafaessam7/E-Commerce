using SharedKernel.Exceptions;
using SharedKernel.Results;

namespace Store.Web.Infrastructure.ExceptionHandling;

/// <summary>
/// One place mapping "what kind of failure" to "which HTTP status code" — shared by
/// <see cref="GlobalExceptionHandler"/> (for thrown exceptions) and
/// <c>ResultExtensions.ToActionResult</c> (for <see cref="Result"/> failures returned by
/// Application handlers), so a controller gets the exact same ProblemDetails shape regardless of
/// which of the two error-handling paths (Section 24's Exceptions vs Section 13's CQRS Result
/// pattern) actually produced the failure.
/// </summary>
internal static class HttpStatusCodeMapper
{
    public static int FromErrorType(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Failure => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static int FromException(Exception exception) => exception switch
    {
        ValidationException => StatusCodes.Status400BadRequest,
        DomainException => StatusCodes.Status400BadRequest,
        NotFoundException => StatusCodes.Status404NotFound,
        ConflictException => StatusCodes.Status409Conflict,
        UnauthorizedException => StatusCodes.Status401Unauthorized,
        ForbiddenException => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };
}
