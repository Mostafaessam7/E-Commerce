using Catalog.Application.Products;
using Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Reviews.Application.Reviews;
using Store.Web.Models;

namespace Store.Web.Controllers;

[Route("product")]
public class ProductController : Controller
{
    private readonly IDispatcher _dispatcher;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ProductController(IDispatcher dispatcher, IStringLocalizer<SharedResource> localizer)
    {
        _dispatcher = dispatcher;
        _localizer = localizer;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetProductBySlugQuery(slug), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        var reviewsResult = await _dispatcher.Send(new GetProductReviewsQuery(result.Value.Id), cancellationToken);
        ViewBag.Reviews = reviewsResult.Value;
        ViewBag.ReviewForm = new SubmitReviewFormModel { ProductId = result.Value.Id };

        return View(result.Value);
    }

    [HttpPost("review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(SubmitReviewFormModel form, string slug, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ReviewError"] = _localizer["Please fill in every required field with a rating between 1 and 5."].Value;
            return RedirectToAction(nameof(Details), new { slug });
        }

        var result = await _dispatcher.Send(
            new SubmitReviewCommand(form.ProductId, form.ReviewerName, form.ReviewerEmail, form.Rating, form.Title, form.Body),
            cancellationToken);

        // result.Error.Message (a domain validation message from Reviews.Domain) is deliberately
        // left in English — localizing every domain error message across every module is a much
        // larger, separate effort than the storefront UI text this phase covers.
        TempData[result.IsSuccess ? "ReviewSuccess" : "ReviewError"] = result.IsSuccess
            ? _localizer["Thanks — your review was submitted and will appear once approved."].Value
            : result.Error.Message;

        return RedirectToAction(nameof(Details), new { slug });
    }
}
