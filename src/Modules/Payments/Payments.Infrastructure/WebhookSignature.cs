using System.Security.Cryptography;
using System.Text;

namespace Payments.Infrastructure;

/// <summary>
/// HMAC-SHA256 over the raw request body, hex-encoded — the same scheme real providers use
/// (Stripe, Paymob, ...) for webhook signing. Shared by <see cref="FakePaymentGateway"/> (verifies
/// inbound webhooks) and <see cref="FakeWebhookSimulator"/> (signs simulated ones the same way a
/// real provider would) so both sides of the "fake provider" agree on one implementation instead
/// of two that could drift apart.
/// </summary>
internal static class WebhookSignature
{
    public static string Compute(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Constant-time comparison — a naive <c>==</c> here would leak timing information
    /// about how much of the signature matched, defeating the point of verifying it.</summary>
    public static bool Verify(string payload, string signature, string secret)
    {
        var expected = Compute(payload, secret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }
}
