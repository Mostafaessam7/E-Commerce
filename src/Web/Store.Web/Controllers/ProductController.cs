using Catalog.Application.Products;
using Messaging;
using Microsoft.AspNetCore.Mvc;

namespace Store.Web.Controllers;

[Route("product")]
public class ProductController : Controller
{
    private readonly IDispatcher _dispatcher;

    public ProductController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetProductBySlugQuery(slug), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        return View(result.Value);
    }
}
