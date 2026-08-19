using Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Payments.Application.Payments;
using Store.Web.Infrastructure.RateLimiting;

namespace Store.Web.Controllers.Api;

/// <summary>
/// Real webhook receiver (Section 9) — signature verified before anything else, idempotent
/// processing, acks receipt independently of the business outcome. Provider-agnostic route
/// (<c>{provider}</c>) even though only "fake" exists today — adding a real provider means adding
/// a route value and an <c>IPaymentGateway</c>, not a new endpoint.
/// </summary>
[ApiController]
[Route("api/webhooks/payments")]
[EnableRateLimiting(RateLimiterExtensions.WebhookPolicy)]
public sealed class WebhooksController : ControllerBase
{
    private const string SignatureHeader = "X-Payment-Signature";

    private readonly IDispatcher _dispatcher;

    public WebhooksController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpPost("{provider}")]
    public async Task<IActionResult> Receive(string provider, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync(cancellationToken);

        if (!Request.Headers.TryGetValue(SignatureHeader, out var signature) || string.IsNullOrWhiteSpace(signature))
        {
            return Unauthorized();
        }

        var result = await _dispatcher.Send(new ProcessWebhookCommand(rawPayload, signature.ToString()), cancellationToken);

        if (result.IsFailure && result.Error.Code == "Webhook.InvalidSignature")
        {
            return Unauthorized();
        }

        // Every other outcome (unknown payment, already-resolved transaction, unknown event
        // type) still returns 200 — the provider's retry policy shouldn't be driven by our
        // domain-level conflicts; those are for us to investigate, not for them to redeliver.
        return Ok();
    }
}
