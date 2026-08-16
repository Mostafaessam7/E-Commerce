using Messaging;
using Ordering.Application.Abstractions;
using SharedKernel.Results;

namespace Ordering.Application.Carts;

public sealed record RemoveCartItemCommand(Guid CartId, Guid CartItemId) : ICommand<CartDto>;

public sealed class RemoveCartItemCommandHandler : IRequestHandler<RemoveCartItemCommand, CartDto>
{
    private readonly ICartRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;

    public RemoveCartItemCommandHandler(ICartRepository repository, IOrderingUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CartDto>> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken = default)
    {
        var cart = await _repository.GetByIdAsync(request.CartId, cancellationToken);
        if (cart is null)
        {
            return Result.Failure<CartDto>(Error.NotFound("Cart.NotFound", "Cart was not found."));
        }

        var result = cart.RemoveItem(request.CartItemId);
        if (result.IsFailure)
        {
            return Result.Failure<CartDto>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(CartMapper.ToDto(cart));
    }
}
