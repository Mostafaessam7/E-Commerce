using SharedKernel.Results;

namespace Store.Web.Infrastructure.Uploads;

/// <summary>
/// Saves to <c>wwwroot/uploads/products/{productId}/</c> on the local disk the app runs on.
/// Fine for this single-instance dev/demo deployment (docs/deployment.md); a real multi-instance
/// production deployment would need shared storage (blob storage, mounted volume) behind the same
/// <see cref="IProductImageStorage"/> seam instead — see this type's doc comment.
/// </summary>
public sealed class LocalProductImageStorage : IProductImageStorage
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif",
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IWebHostEnvironment _environment;

    public LocalProductImageStorage(IWebHostEnvironment environment) => _environment = environment;

    public async Task<Result<string>> SaveAsync(Guid productId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Result.Failure<string>(Error.Validation("ProductImage.Empty", "Choose an image file first."));
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return Result.Failure<string>(Error.Validation("ProductImage.TooLarge", "Image must be 5 MB or smaller."));
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return Result.Failure<string>(Error.Validation(
                "ProductImage.UnsupportedType", "Only JPG, PNG, WEBP, and GIF images are allowed."));
        }

        var webRoot = _environment.WebRootPath;
        var relativeDirectory = Path.Combine("uploads", "products", productId.ToString());
        var absoluteDirectory = Path.Combine(webRoot, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var absolutePath = Path.Combine(absoluteDirectory, fileName);

        await using (var stream = new FileStream(absolutePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var url = "/" + string.Join('/', relativeDirectory.Split(Path.DirectorySeparatorChar)) + "/" + fileName;
        return Result.Success(url);
    }

    public bool Delete(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // The URL is read back from the database, but treat it as untrusted anyway: a stored value
        // like "/uploads/products/../../../appsettings.json" must never resolve into a delete. Build
        // the absolute path, then require it to sit under the uploads root before touching anything.
        var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "products"));
        var relative = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, relative));

        if (!absolutePath.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return false;
        }

        // Already-missing file is a success, not an error: the point is that it is gone.
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return true;
    }

    public void DeleteAllForProduct(Guid productId)
    {
        var directory = Path.GetFullPath(
            Path.Combine(_environment.WebRootPath, "uploads", "products", productId.ToString()));

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
