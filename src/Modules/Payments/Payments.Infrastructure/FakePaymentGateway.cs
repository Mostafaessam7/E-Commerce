using Microsoft.Extensions.Configuration;
using Payments.Application.Abstractions;
using SharedKernel.Results;

namespace Payments.Infrastructure;

/// <summary>
/// The only <see cref="IPaymentGateway"/> implementation today — no real Stripe/Paymob account
/// exists for this project. Still exercises the real mechanics Section 9 asks for (signed
/// webhooks, signature verification) rather than short-circuiting them; swapping in a real
/// provider later means adding a new class here, not touching Payments.Application/Domain or any
/// other module (that's the point of the abstraction).
/// </summary>
internal sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly string _webhookSecret;

    public FakePaymentGateway(IConfiguration configuration)
    {
        _webhookSecret = configuration["Payments:WebhookSecret"]
            ?? throw new InvalidOperationException("Configuration 'Payments:WebhookSecret' is required.");
    }

    public string ProviderName => "fake";

    public Task<Result<PaymentIntentResult>> CreateIntentAsync(Guid orderId, decimal amount, string currency, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new PaymentIntentResult($"pi_{Guid.NewGuid():N}", RedirectUrl: null)));

    public Task<Result<RefundResult>> RefundAsync(string providerTransactionId, decimal amount, string currency, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new RefundResult($"re_{Guid.NewGuid():N}")));

    public bool VerifyWebhookSignature(string rawPayload, string signature) =>
        WebhookSignature.Verify(rawPayload, signature, _webhookSecret);
}
