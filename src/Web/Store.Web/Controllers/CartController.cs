using Messaging;
using Microsoft.AspNetCore.Mvc;
using Ordering.Application.Carts;
using Store.Web.Infrastructure;
using Store.Web.Infrastructure.ExceptionHandling;

namespace Store.Web.Controllers;

public class CartController : Controller
{
    private readonly IDispatcher _dispatcher;

    public CartController(IDispatcher dispatcher) => _dispatcher = dispatcher;

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

    private async Task<CartDto> CurrentCart(CancellationToken cancellationToken)
    {
        var anonymousId = HttpContext.GetOrSetAnonymousId();
        var result = await _dispatcher.Send(new GetOrCreateCartCommand(CustomerId: null, AnonymousId: anonymousId), cancellationToken);
        return result.Value;
    }
}
