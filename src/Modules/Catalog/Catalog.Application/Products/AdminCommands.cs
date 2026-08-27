using Infrastructure;
using Messaging;
using Security;
using SharedKernel.Results;

namespace Catalog.Application.Products;

/// <summary>
/// Admin-only write operations on <see cref="Catalog.Domain.Product"/>, beyond the storefront's
/// read-only concerns — same repository/unit-of-work pair as <see cref="CreateProductCommand"/>,
/// no new abstractions.
/// </summary>
public sealed record UpdateProductCommand(
    Guid ProductId, string Name, string? ShortDescription, string? Description,
    Guid? BrandId = null, IReadOnlyList<Guid>? CategoryIds = null) : ICommand<Unit>;

public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Unit>
{
    private readonly IProductRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public UpdateProductCommandHandler(IProductRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(UpdateProductCommand request, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Product.NotFound", "Product was not found."));
        }

        var result = product.UpdateDetails(request.Name, request.ShortDescription, request.Description);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        product.SetBrand(request.BrandId);
        if (request.CategoryIds is not null)
        {
            product.SetCategories(request.CategoryIds);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record AddProductVariantCommand(
    Guid ProductId, string Sku, decimal Price, string Currency, decimal? SalePrice) : ICommand<Guid>;

public sealed class AddProductVariantCommandHandler : IRequestHandler<AddProductVariantCommand, Guid>
{
    private readonly IProductRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public AddProductVariantCommandHandler(IProductRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(AddProductVariantCommand request, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Product.NotFound", "Product was not found."));
        }

        var result = product.AddVariant(request.Sku, request.Price, request.Currency, request.SalePrice, barcode: null, weightKg: null);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(result.Value);
    }
}

public sealed record AddProductImageCommand(Guid ProductId, string Url, string? AltText, bool IsPrimary) : ICommand<Unit>;

public sealed class AddProductImageCommandHandler : IRequestHandler<AddProductImageCommand, Unit>
{
    private readonly IProductRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public AddProductImageCommandHandler(IProductRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(AddProductImageCommand request, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Product.NotFound", "Product was not found."));
        }

        product.AddImage(request.Url, request.AltText, request.IsPrimary);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

/// <summary>
/// Returns the removed image's URL rather than <c>Unit</c> so the Web layer can delete the backing
/// file. Catalog still has no opinion on where that file lives (it only ever handled a URL string);
/// it just reports which URL stopped being referenced, and <c>IProductImageStorage</c> decides what
/// that means on disk. Before this, removing an image deleted the row and left the file orphaned.
/// </summary>
public sealed record RemoveProductImageCommand(Guid ProductId, Guid ImageId) : ICommand<string>;

public sealed class RemoveProductImageCommandHandler : IRequestHandler<RemoveProductImageCommand, string>
{
    private readonly IProductRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public RemoveProductImageCommandHandler(IProductRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<string>> Handle(RemoveProductImageCommand request, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<string>(Error.NotFound("Product.NotFound", "Product was not found."));
        }

        // Captured before removal — afterwards the image is off the collection and its URL is gone.
        var removedUrl = product.Images.FirstOrDefault(i => i.Id == request.ImageId)?.Url;

        var result = product.RemoveImage(request.ImageId);
        if (result.IsFailure)
        {
            return Result.Failure<string>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(removedUrl ?? string.Empty);
    }
}

/// <summary>
/// Phase 39 (ADR-050): <c>Product.Feature</c>/<c>Unfeature</c> existed in the domain since the
/// original build but were never wired to any admin command — the Home page's "Featured Products"
/// section (Phase 4) had no way for an admin to ever put a product in it. Same shape of gap as
/// <c>MergeCartCommand</c> before Phase 28, the image commands before Phase 29, and the coupon
/// commands before Phase 31.
/// </summary>
public sealed record FeatureProductCommand(Guid ProductId) : ICommand<Unit>;

public sealed class FeatureProductCommandHandler : IRequestHandler<FeatureProductCommand, Unit>
{
    private readonly IProductRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public FeatureProductCommandHandler(IProductRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(FeatureProductCommand request, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Product.NotFound", "Product was not found."));
        }

        product.Feature();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record UnfeatureProductCommand(Guid ProductId) : ICommand<Unit>;

public sealed class UnfeatureProductCommandHandler : IRequestHandler<UnfeatureProductCommand, Unit>
{
    private readonly IProductRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public UnfeatureProductCommandHandler(IProductRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(UnfeatureProductCommand request, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Product.NotFound", "Product was not found."));
        }

        product.Unfeature();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record PublishProductCommand(Guid ProductId) : ICommand<Unit>;

public sealed class PublishProductCommandHandler : IRequestHandler<PublishProductCommand, Unit>
{
    private readonly IProductRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public PublishProductCommandHandler(IProductRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(PublishProductCommand request, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Product.NotFound", "Product was not found."));
        }

        var result = product.Publish();
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record ArchiveProductCommand(Guid ProductId) : ICommand<Unit>;

public sealed class ArchiveProductCommandHandler : IRequestHandler<ArchiveProductCommand, Unit>
{
    private readonly IProductRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public ArchiveProductCommandHandler(IProductRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(ArchiveProductCommand request, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Product.NotFound", "Product was not found."));
        }

        product.Archive();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record DeleteProductCommand(Guid ProductId) : ICommand<Unit>;

public sealed class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Unit>
{
    private readonly IProductRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeleteProductCommandHandler(
        IProductRepository repository,
        ICatalogUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Unit>> Handle(DeleteProductCommand request, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Product.NotFound", "Product was not found."));
        }

        product.Delete(_dateTimeProvider.UtcNow, _currentUser.Email ?? "admin");
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record GetProductByIdQuery(Guid ProductId) : IQuery<ProductDetailsDto>;

public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDetailsDto>
{
    private readonly IProductRepository _repository;

    public GetProductByIdQueryHandler(IProductRepository repository) => _repository = repository;

    public async Task<Result<ProductDetailsDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ProductDetailsDto>(Error.NotFound("Product.NotFound", "Product was not found."));
        }

        return Result.Success(new ProductDetailsDto(
            product.Id,
            product.Name,
            product.Slug.Value,
            product.ShortDescription,
            product.Description,
            product.BrandId,
            product.Status.ToString(),
            product.IsFeatured,
            product.Seo.MetaTitle,
            product.Seo.MetaDescription,
            product.Tags.ToList(),
            product.Variants.Select(v => new ProductVariantDto(v.Id, v.Sku, v.Price.Amount, v.Price.Currency, v.SalePrice?.Amount)).ToList(),
            product.Images.OrderBy(i => i.DisplayOrder).Select(i => new ProductImageDto(i.Id, i.Url, i.AltText, i.IsPrimary)).ToList(),
            product.CategoryIds.ToList()));
    }
}
