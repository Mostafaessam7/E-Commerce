using Catalog.Domain;
using Messaging;
using SharedKernel.Results;

namespace Catalog.Application.Products;

public sealed record CreateProductCommand(
    string Name,
    string Slug,
    string? ShortDescription,
    string? Description,
    Guid? BrandId,
    IReadOnlyList<Guid>? CategoryIds) : ICommand<Guid>;

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IProductRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public CreateProductCommandHandler(IProductRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken = default)
    {
        var result = Product.Create(
            request.Name, request.Slug, request.ShortDescription, request.Description, request.BrandId, request.CategoryIds);

        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        var product = result.Value;
        await _repository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(product.Id);
    }
}
