namespace SharedKernel.Guards;

/// <summary>
/// Defensive checks for arguments that should never be invalid if the caller is using the API
/// correctly — an entity constructor receiving a null it should never receive, a negative
/// quantity a private method assumes is already validated. These throw
/// <see cref="ArgumentException"/>/<see cref="ArgumentNullException"/> because that's a bug, not
/// a business outcome.
///
/// This is deliberately not the same tool as <c>SharedKernel.Results.Result</c>: a Guard failure
/// means "this code path should have been unreachable"; a Result failure means "the business
/// rule was violated and the caller needs to react to that". Domain factory methods
/// (e.g. <c>Money.Create</c>, <c>Product.Create</c>) use Result for the checks a caller can
/// legitimately trigger (bad user input) and Guard only for the ones they can't.
/// </summary>
public static class Guard
{
    public static class Against
    {
        public static T Null<T>(T? value, string paramName)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(value, paramName);
            return value;
        }

        public static string NullOrWhiteSpace(string? value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
            }

            return value;
        }

        public static Guid Empty(Guid value, string paramName)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be an empty Guid.", paramName);
            }

            return value;
        }

        public static decimal Negative(decimal value, string paramName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative.");
            }

            return value;
        }

        public static int NegativeOrZero(int value, string paramName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be greater than zero.");
            }

            return value;
        }
    }
}
