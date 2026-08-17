using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payments.Application.Payments;
using Security;
using Store.Web.Areas.Admin.Models;

namespace Store.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Payments.View)]
public sealed class PaymentsController : Controller
{
    private readonly IDispatcher _dispatcher;

    public PaymentsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ListPaymentsQuery(), cancellationToken);
        return View(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Payments.Refund)]
    public async Task<IActionResult> Refund(RefundPaymentFormModel form, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new RefundPaymentCommand(form.PaymentTransactionId, form.Amount, form.Reason), cancellationToken);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Refund processed." : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }
}
