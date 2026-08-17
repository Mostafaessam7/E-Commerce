using Catalog.Application.Products;
using Catalog.Domain;
using Messaging;
using SharedKernel.Results;

namespace Catalog.Application.Brands;

public sealed record CreateBrandCommand(string Name, string Slug) : ICommand<Guid>;

public sealed class CreateBrandCommandHandler : IRequestHandler<CreateBrandCommand, Guid>
{
    private readonly IBrandRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public CreateBrandCommandHandler(IBrandRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateBrandCommand request, CancellationToken cancellationToken = default)
    {
        var result = Brand.Create(request.Name, request.Slug);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        var brand = result.Value;
        await _repository.AddAsync(brand, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(brand.Id);
    }
}

public sealed record ActivateBrandCommand(Guid BrandId) : ICommand<Unit>;

public sealed class ActivateBrandCommandHandler : IRequestHandler<ActivateBrandCommand, Unit>
{
    private readonly IBrandRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public ActivateBrandCommandHandler(IBrandRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(ActivateBrandCommand request, CancellationToken cancellationToken = default)
    {
        var brand = await _repository.GetByIdAsync(request.BrandId, cancellationToken);
        if (brand is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Brand.NotFound", "Brand was not found."));
        }

        brand.Activate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record DeactivateBrandCommand(Guid BrandId) : ICommand<Unit>;

public sealed class DeactivateBrandCommandHandler : IRequestHandler<DeactivateBrandCommand, Unit>
{
    private readonly IBrandRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public DeactivateBrandCommandHandler(IBrandRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(DeactivateBrandCommand request, CancellationToken cancellationToken = default)
    {
        var brand = await _repository.GetByIdAsync(request.BrandId, cancellationToken);
        if (brand is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Brand.NotFound", "Brand was not found."));
        }

        brand.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record ListBrandsQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<BrandDto>>;

public sealed class ListBrandsQueryHandler : IRequestHandler<ListBrandsQuery, IReadOnlyList<BrandDto>>
{
    private readonly IBrandQueries _queries;

    public ListBrandsQueryHandler(IBrandQueries queries) => _queries = queries;

    public async Task<Result<IReadOnlyList<BrandDto>>> Handle(ListBrandsQuery request, CancellationToken cancellationToken = default) =>
        Result.Success(await _queries.ListAsync(request.IncludeInactive, cancellationToken));
}
