namespace SharedKernel.Results;

/// <summary>
/// Outcome of an operation that can fail for reasons the caller is expected to handle —
/// a business rule, a validation failure, a missing entity. Domain methods, Application command
/// or query handlers, and anything else that models an *expected* failure return
/// <see cref="Result"/> / <see cref="Result{TValue}"/> instead of throwing.
///
/// Exceptions are reserved for the unexpected: a bug, a violated invariant that should have
/// been impossible to reach, infrastructure failing in a way the caller cannot meaningfully
/// react to. See <c>SharedKernel.Exceptions</c> for that half of the error-handling story.
/// </summary>
public class Result
{
    protected internal Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    /// <summary>
    /// Wraps a possibly-null lookup result: <paramref name="value"/> present → success,
    /// null → failure with <paramref name="error"/>. Handy for
    /// <c>Result.Create(await repo.FindAsync(id), ProductErrors.NotFound(id))</c>.
    /// </summary>
    public static Result<TValue> Create<TValue>(TValue? value, Error error)
        where TValue : class =>
        value is not null ? Success(value) : Failure<TValue>(error);
}

/// <summary>
/// A <see cref="Result"/> that carries a value on success. <see cref="Value"/> throws if the
/// result failed — always check <see cref="Result.IsSuccess"/> (or pattern-match) first, the
/// same way you'd check before dereferencing a nullable.
/// </summary>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public TValue Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
