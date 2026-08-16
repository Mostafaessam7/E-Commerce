namespace SharedKernel.Exceptions;

/// <summary>
/// The caller is authenticated but lacks the permission for this action (see the
/// permission-based authorization model in <c>Security</c>, e.g. missing <c>Orders.Cancel</c>).
/// ASP.NET Core's policy-based authorization normally short-circuits this before a handler runs;
/// this exists for the same check made explicitly inside a handler. Maps to HTTP 403.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message)
    {
    }
}
