namespace SharedKernel.Results;

/// <summary>
/// A single, structured failure reason. <see cref="Code"/> is a stable machine-readable string
/// (e.g. <c>"Product.NotFound"</c>, <c>"Order.CannotCancelAfterShipped"</c>) — log on it, assert
/// on it in tests, never parse <see cref="Message"/>.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
