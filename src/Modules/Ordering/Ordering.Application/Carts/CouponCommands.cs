using Messaging;
using Ordering.Application.Abstractions;
using SharedKernel.Results;

namespace Ordering.Application.Carts;

/// <summary>
/// Stores a coupon *code* on the cart — display-only, same deferred-validation rule as price/stock
/// (Section 6): the code is never checked against Promotions here, only at
/// <c>PlaceOrderCommandHandler</c>'s real <c>Promotions.Contracts.RedeemCouponCommand</c> dispatch
/// (ADR-014, built Phase 18), which fails the whole checkout if the code turns out to be
/// invalid/expired/exhausted. Applying an unrecognized code to the cart is harmless and
/// reversible; redeeming one against a real order is not.
/// </summary>
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
