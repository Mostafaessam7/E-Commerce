using Messaging;
using Microsoft.AspNetCore.Mvc;
using Ordering.Application.Carts;
using Security;
using Store.Web.Infrastructure;
using Store.Web.Infrastructure.ExceptionHandling;

namespace Store.Web.Controllers;

public class CartController : Controller
{
    private readonly IDispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;

    public CartController(IDispatcher dispatcher, ICurrentUser currentUser)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var cart = await CurrentCart(cancellationToken);
        return View(cart);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(Guid productVariantId, int quantity, CancellationToken cancellationToken)
    {
        var cart = await CurrentCart(cancellationToken);
        var result = await _dispatcher.Send(new AddCartItemCommand(cart.Id, productVariantId, quantity), cancellationToken);

        return result.IsSuccess ? RedirectToAction(nameof(Index)) : result.ToActionResult();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(Guid cartItemId, int quantity, CancellationToken cancellationToken)
    {
        var cart = await CurrentCart(cancellationToken);
        var result = await _dispatcher.Send(new UpdateCartItemQuantityCommand(cart.Id, cartItemId, quantity), cancellationToken);

        return result.IsSuccess ? RedirectToAction(nameof(Index)) : result.ToActionResult();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid cartItemId, CancellationToken cancellationToken)
    {
        var cart = await CurrentCart(cancellationToken);
        await _dispatcher.Send(new RemoveCartItemCommand(cart.Id, cartItemId), cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    // A signed-in shopper's cart is always looked up by CustomerId (Phase 28) — the anonymous-id
    // cookie is still read (never skipped) so a guest who logs in mid-session still has something
    // for AccountController.Login's MergeCartCommand to fold in on their *next* login, not this
    // request's.
    private async Task<CartDto> CurrentCart(CancellationToken cancellationToken)
    {
        var anonymousId = HttpContext.GetOrSetAnonymousId();
        var customerId = _currentUser.IsAuthenticated ? _currentUser.UserId : null;
        var result = await _dispatcher.Send(new GetOrCreateCartCommand(CustomerId: customerId, AnonymousId: anonymousId), cancellationToken);
        return result.Value;
    }
}
