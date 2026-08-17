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
