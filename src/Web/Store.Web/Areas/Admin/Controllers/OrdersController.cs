using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ordering.Application.Checkout;
using Security;
using Store.Web.Areas.Admin.Models;

namespace Store.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Orders.View)]
public sealed class OrdersController : Controller
{
    private readonly IDispatcher _dispatcher;

    public OrdersController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(
            new SearchOrdersQuery(new OrderSearchCriteria(status, page, PageSize: 20)), cancellationToken);

        ViewData["StatusFilter"] = status;
        return View(result.Value);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetOrderQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        return View(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Orders.Edit)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ConfirmOrderCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Order confirmed." : result.Error.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Orders.Edit)]
    public async Task<IActionResult> StartProcessing(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new StartProcessingOrderCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Order is now processing." : result.Error.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Orders.Edit)]
    public async Task<IActionResult> Ship(ShipOrderFormModel form, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ShipOrderCommand(form.OrderId, form.TrackingNumber), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Order marked as shipped." : result.Error.Message;
        return RedirectToAction(nameof(Details), new { id = form.OrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Orders.Edit)]
    public async Task<IActionResult> Deliver(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeliverOrderCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Order marked as delivered." : result.Error.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Orders.Cancel)]
    public async Task<IActionResult> Cancel(CancelOrderFormModel form, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CancelOrderCommand(form.OrderId, form.Reason), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Order cancelled." : result.Error.Message;
        return RedirectToAction(nameof(Details), new { id = form.OrderId });
    }
}
