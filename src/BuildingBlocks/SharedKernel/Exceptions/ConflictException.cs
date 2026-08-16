namespace SharedKernel.Exceptions;

/// <summary>
/// The request conflicts with the current state of the resource — an EF Core
/// <c>DbUpdateConcurrencyException</c> surfacing a lost-update race (Inventory stock, Order
/// status), or a uniqueness rule (SKU, slug, email) failing at the database. Maps to HTTP 409.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
