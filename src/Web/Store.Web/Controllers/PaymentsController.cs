using Messaging;
using Microsoft.AspNetCore.Mvc;
using Ordering.Application.Checkout;
using Payments.Application.Abstractions;
using Payments.Application.Payments;

namespace Store.Web.Controllers;

/// <summary>
/// Demo-only "pay now" flow: initializes a payment, then (since no real provider account exists)
/// signs a simulated webhook the same way the provider behind <c>IPaymentGateway</c> would and
/// POSTs it through <see cref="Api.WebhooksController"/> — the real endpoint, real signature
/// verification, real idempotent processing. Nothing about the webhook path itself is faked.
/// </summary>
public class PaymentsController : Controller
{
    private readonly IDispatcher _dispatcher;
    private readonly IWebhookSimulator _webhookSimulator;
    private readonly IHttpClientFactory _httpClientFactory;

    public PaymentsController(IDispatcher dispatcher, IWebhookSimulator webhookSimulator, IHttpClientFactory httpClientFactory)
    {
        _dispatcher = dispatcher;
        _webhookSimulator = webhookSimulator;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(Guid orderId, CancellationToken cancellationToken)
    {
        var orderResult = await _dispatcher.Send(new GetOrderQuery(orderId), cancellationToken);
        if (orderResult.IsFailure)
        {
            return NotFound();
        }

        var order = orderResult.Value;

        var initResult = await _dispatcher.Send(new InitializePaymentCommand(orderId, order.Total, order.Currency), cancellationToken);
        if (initResult.IsFailure)
        {
            TempData["PaymentError"] = initResult.Error.Message;
            return RedirectToAction("Confirmation", "Checkout", new { orderId });
        }

        // Simulate the provider's async callback by driving the real webhook endpoint —
        // in a real integration this request would come from the provider's servers, not ours.
        var (payload, signature) = _webhookSimulator.BuildSucceededPayload(initResult.Value.PaymentTransactionId);

        var client = _httpClientFactory.CreateClient();
        var webhookUrl = $"{Request.Scheme}://{Request.Host}/api/webhooks/payments/fake";
        using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        content.Headers.Remove("Content-Type");
        content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, webhookUrl) { Content = content };
        requestMessage.Headers.Add("X-Payment-Signature", signature);

        await client.SendAsync(requestMessage, cancellationToken);

        return RedirectToAction("Confirmation", "Checkout", new { orderId });
    }
}
