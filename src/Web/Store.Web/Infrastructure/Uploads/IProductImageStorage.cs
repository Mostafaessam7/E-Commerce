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

    /// <summary>
    /// Deletes the file behind a stored image URL. Best-effort by design: the database row is the
    /// source of truth, so a file that is already gone is not an error. Returns false only when the
    /// URL does not point somewhere this storage owns — the caller should treat that as suspicious
    /// rather than routine.
    /// </summary>
    bool Delete(string url);

    /// <summary>
    /// Deletes every image belonging to a product — used when the product itself is deleted, where
    /// removing files one URL at a time would leave the (now empty) product folder behind anyway.
    /// </summary>
    void DeleteAllForProduct(Guid productId);
}
