namespace SharedKernel.Exceptions;

/// <summary>
/// The caller is not authenticated. Distinct from the BCL's <see cref="System.UnauthorizedAccessException"/>
/// (a .NET runtime/OS access-denied signal) so the global exception handler can map this one
/// specifically to HTTP 401 without guessing at intent. In practice ASP.NET Core's own
/// authentication middleware handles the unauthenticated case before a request reaches a
/// controller; this exists for the same situation reached programmatically (e.g. deep in a
/// service call, a background job impersonating a user that turns out to be invalid).
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Authentication is required.")
        : base(message)
    {
    }
}
