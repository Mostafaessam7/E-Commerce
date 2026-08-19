using SharedKernel.Results;

namespace Store.Web.Infrastructure.Uploads;

/// <summary>
/// Saves an admin-uploaded product image to disk and hands back the public URL to store on
/// <c>Catalog.Domain.Product.Images</c> (Phase 29). A Web-layer concern, not Application/Domain —
/// the Catalog module only ever deals in a URL string (<c>AddProductImageCommand</c>); it has no
/// opinion on where that file physically lives. Swap the implementation (e.g. for blob storage)
/// without touching Catalog if that's ever needed.
/// </summary>
public interface IProductImageStorage
{
    Task<Result<string>> SaveAsync(Guid productId, IFormFile file, CancellationToken cancellationToken);
}
