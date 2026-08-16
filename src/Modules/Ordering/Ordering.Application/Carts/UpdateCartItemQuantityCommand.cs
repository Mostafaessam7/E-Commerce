using Messaging;
using Ordering.Application.Abstractions;
using SharedKernel.Results;

namespace Ordering.Application.Carts;

public sealed record UpdateCartItemQuantityCommand(Guid CartId, Guid CartItemId, int Quantity) : ICommand<CartDto>;

public sealed class UpdateCartItemQuantityCommandHandler : IRequestHandler<UpdateCartItemQuantityCommand, CartDto>
{
    private readonly ICartRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;

    public UpdateCartItemQuantityCommandHandler(ICartRepository repository, IOrderingUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CartDto>> Handle(UpdateCartItemQuantityCommand request, CancellationToken cancellationToken = default)
    {
        var cart = await _repository.GetByIdAsync(request.CartId, cancellationToken);
        if (cart is null)
        {
            return Result.Failure<CartDto>(Error.NotFound("Cart.NotFound", "Cart was not found."));
        }

        var result = cart.ChangeItemQuantity(request.CartItemId, request.Quantity);
        if (result.IsFailure)
        {
            return Result.Failure<CartDto>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(CartMapper.ToDto(cart));
    }
}
