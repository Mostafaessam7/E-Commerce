using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Reviews;
using Security;

namespace Store.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Reviews.View)]
public sealed class ReviewsController : Controller
{
    private readonly IDispatcher _dispatcher;

    public ReviewsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<IActionResult> Index(bool pendingOnly = true, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new ListReviewsQuery(pendingOnly), cancellationToken);
        ViewBag.PendingOnly = pendingOnly;
        return View(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Reviews.Moderate)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ApproveReviewCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Review approved." : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Reviews.Moderate)]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RejectReviewCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Review rejected." : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }
}
