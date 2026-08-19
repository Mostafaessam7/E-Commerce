using Catalog.Contracts;
using Messaging;
using Ordering.Application.Abstractions;
using SharedKernel.Results;

namespace Ordering.Application.Carts;

public sealed record AddCartItemCommand(Guid CartId, Guid ProductVariantId, int Quantity) : ICommand<CartDto>;

public sealed class AddCartItemCommandHandler : IRequestHandler<AddCartItemCommand, CartDto>
{
    private readonly ICartRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly IDispatcher _dispatcher;

    public AddCartItemCommandHandler(ICartRepository repository, IOrderingUnitOfWork unitOfWork, IDispatcher dispatcher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
    }

    public async Task<Result<CartDto>> Handle(AddCartItemCommand request, CancellationToken cancellationToken = default)
    {
        var cart = await _repository.GetByIdAsync(request.CartId, cancellationToken);
        if (cart is null)
        {
            return Result.Failure<CartDto>(Error.NotFound("Cart.NotFound", "Cart was not found."));
        }

        // Cross-module read via the shared dispatcher + Catalog's Contracts-defined query
        // (ADR-014) — never a direct reference to Catalog.Application/Domain/Infrastructure.
        var variantResult = await _dispatcher.Send(new GetProductVariantSnapshotQuery(request.ProductVariantId), cancellationToken);
        if (variantResult.IsFailure)
        {
            return Result.Failure<CartDto>(variantResult.Error);
        }

        var variant = variantResult.Value;
        if (!variant.IsPurchasable)
        {
            return Result.Failure<CartDto>(Error.Conflict("Cart.ProductUnavailable", $"'{variant.ProductName}' is not currently available for purchase."));
        }

        var addResult = cart.AddItem(
            variant.ProductVariantId, variant.ProductId, variant.ProductName, variant.Sku,
            variant.SalePrice ?? variant.Price, variant.Currency, request.Quantity, variant.PrimaryImageUrl);

        if (addResult.IsFailure)
        {
            return Result.Failure<CartDto>(addResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(CartMapper.ToDto(cart));
    }
}
