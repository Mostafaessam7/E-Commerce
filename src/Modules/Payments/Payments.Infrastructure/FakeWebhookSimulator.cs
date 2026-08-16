using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Payments.Application.Abstractions;
using Payments.Application.Payments;

namespace Payments.Infrastructure;

internal sealed class FakeWebhookSimulator : IWebhookSimulator
{
    private readonly string _webhookSecret;

    public FakeWebhookSimulator(IConfiguration configuration)
    {
        _webhookSecret = configuration["Payments:WebhookSecret"]
            ?? throw new InvalidOperationException("Configuration 'Payments:WebhookSecret' is required.");
    }

    public (string Payload, string Signature) BuildSucceededPayload(Guid paymentTransactionId)
    {
        var payload = JsonSerializer.Serialize(new WebhookPayload(
            EventId: $"evt_{Guid.NewGuid():N}",
            EventType: "payment.succeeded",
            PaymentTransactionId: paymentTransactionId,
            ProviderTransactionId: $"txn_{Guid.NewGuid():N}",
            FailureReason: null));

        return (payload, WebhookSignature.Compute(payload, _webhookSecret));
    }

    public (string Payload, string Signature) BuildFailedPayload(Guid paymentTransactionId, string reason)
    {
        var payload = JsonSerializer.Serialize(new WebhookPayload(
            EventId: $"evt_{Guid.NewGuid():N}",
            EventType: "payment.failed",
            PaymentTransactionId: paymentTransactionId,
            ProviderTransactionId: null,
            FailureReason: reason));

        return (payload, WebhookSignature.Compute(payload, _webhookSecret));
    }
}
