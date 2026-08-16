using Messaging;
using Ordering.Application.Abstractions;
using SharedKernel.Results;

namespace Ordering.Application.Carts;

public sealed record ApplyCouponCommand(Guid CartId, string Code) : ICommand<CartDto>;

public sealed record RemoveCouponCommand(Guid CartId) : ICommand<CartDto>;

public sealed class ApplyCouponCommandHandler : IRequestHandler<ApplyCouponCommand, CartDto>
{
    private readonly ICartRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;

    public ApplyCouponCommandHandler(ICartRepository repository, IOrderingUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CartDto>> Handle(ApplyCouponCommand request, CancellationToken cancellationToken = default)
    {
        var cart = await _repository.GetByIdAsync(request.CartId, cancellationToken);
        if (cart is null)
        {
            return Result.Failure<CartDto>(Error.NotFound("Cart.NotFound", "Cart was not found."));
        }

        // Coupon *validation* (does this code exist, is it active/within limits) is Promotions'
        // job (not built yet) — for now the code is only stored on the cart; PlaceOrderCommand
        // applies zero discount until Promotions exists to actually price it.
        cart.ApplyCoupon(request.Code);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(CartMapper.ToDto(cart));
    }
}

public sealed class RemoveCouponCommandHandler : IRequestHandler<RemoveCouponCommand, CartDto>
{
    private readonly ICartRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;

    public RemoveCouponCommandHandler(ICartRepository repository, IOrderingUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CartDto>> Handle(RemoveCouponCommand request, CancellationToken cancellationToken = default)
    {
        var cart = await _repository.GetByIdAsync(request.CartId, cancellationToken);
        if (cart is null)
        {
            return Result.Failure<CartDto>(Error.NotFound("Cart.NotFound", "Cart was not found."));
        }

        cart.RemoveCoupon();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(CartMapper.ToDto(cart));
    }
}
