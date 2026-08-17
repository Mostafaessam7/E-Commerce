using Catalog.Application.Products;
using Catalog.Domain;
using Messaging;
using SharedKernel.Results;

namespace Catalog.Application.Categories;

public sealed record CreateCategoryCommand(string Name, string Slug, Guid? ParentId) : ICommand<Guid>;

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Guid>
{
    private readonly ICategoryRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public CreateCategoryCommandHandler(ICategoryRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken = default)
    {
        var result = Category.Create(request.Name, request.Slug, request.ParentId);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        var category = result.Value;
        await _repository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(category.Id);
    }
}

public sealed record ActivateCategoryCommand(Guid CategoryId) : ICommand<Unit>;

public sealed class ActivateCategoryCommandHandler : IRequestHandler<ActivateCategoryCommand, Unit>
{
    private readonly ICategoryRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public ActivateCategoryCommandHandler(ICategoryRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(ActivateCategoryCommand request, CancellationToken cancellationToken = default)
    {
        var category = await _repository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Category.NotFound", "Category was not found."));
        }

        category.Activate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record DeactivateCategoryCommand(Guid CategoryId) : ICommand<Unit>;

public sealed class DeactivateCategoryCommandHandler : IRequestHandler<DeactivateCategoryCommand, Unit>
{
    private readonly ICategoryRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public DeactivateCategoryCommandHandler(ICategoryRepository repository, ICatalogUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(DeactivateCategoryCommand request, CancellationToken cancellationToken = default)
    {
        var category = await _repository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Category.NotFound", "Category was not found."));
        }

        category.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record ListCategoriesQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<CategoryDto>>;

public sealed class ListCategoriesQueryHandler : IRequestHandler<ListCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private readonly ICategoryQueries _queries;

    public ListCategoriesQueryHandler(ICategoryQueries queries) => _queries = queries;

    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(ListCategoriesQuery request, CancellationToken cancellationToken = default) =>
        Result.Success(await _queries.ListAsync(request.IncludeInactive, cancellationToken));
}
