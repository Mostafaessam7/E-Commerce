using Infrastructure;
using Messaging;
using Promotions.Application.Abstractions;
using Promotions.Contracts;
using SharedKernel.Results;

namespace Promotions.Application.Coupons;

public sealed class RedeemCouponCommandHandler : IRequestHandler<RedeemCouponCommand, decimal>
{
    private readonly ICouponRepository _repository;
    private readonly IPromotionsUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RedeemCouponCommandHandler(ICouponRepository repository, IPromotionsUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<decimal>> Handle(RedeemCouponCommand request, CancellationToken cancellationToken = default)
    {
        var coupon = await _repository.GetByCodeAsync(request.Code, cancellationToken);
        if (coupon is null)
        {
            return Result.Failure<decimal>(Error.NotFound("Coupon.NotFound", $"Coupon code '{request.Code}' was not found."));
        }

        var result = coupon.Redeem(request.OrderAmount, request.Currency, _dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }
}

public sealed class ReleaseCouponCommandHandler : IRequestHandler<ReleaseCouponCommand, Unit>
{
    private readonly ICouponRepository _repository;
    private readonly IPromotionsUnitOfWork _unitOfWork;

    public ReleaseCouponCommandHandler(ICouponRepository repository, IPromotionsUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(ReleaseCouponCommand request, CancellationToken cancellationToken = default)
    {
        var coupon = await _repository.GetByCodeAsync(request.Code, cancellationToken);
        if (coupon is null)
        {
            // Nothing to release — same "compensation is best-effort, never fails the caller"
            // reasoning as Inventory's ReleaseStockCommand.
            return Result.Success(Unit.Value);
        }

        coupon.ReleaseRedemption();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}
