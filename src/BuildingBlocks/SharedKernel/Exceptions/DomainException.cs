namespace SharedKernel.Exceptions;

/// <summary>
/// An invariant that should have been impossible to violate was violated anyway — e.g. code
/// deep in a call stack (an EF interceptor, a background job, a third-party callback) reaches a
/// state a domain factory method would normally have rejected via <c>Result</c>. Reserve this
/// for genuine "how did we even get here" situations, not for expected, user-triggerable
/// business rule failures — those belong in a <c>Result</c> so the caller can react to them
/// without a try/catch. The global exception handler (Store.Web) maps this to HTTP 400.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
