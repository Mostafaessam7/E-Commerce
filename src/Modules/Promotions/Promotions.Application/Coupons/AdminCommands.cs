using Messaging;
using Promotions.Application.Abstractions;
using Promotions.Domain;
using SharedKernel.Results;

namespace Promotions.Application.Coupons;

public sealed record CreateCouponCommand(
    string Code, DiscountType DiscountType, decimal Value, string Currency,
    DateTime? ExpiresAtUtc, int? UsageLimit, decimal? MinimumOrderAmount) : ICommand<Guid>;

public sealed class CreateCouponCommandHandler : IRequestHandler<CreateCouponCommand, Guid>
{
    private readonly ICouponRepository _repository;
    private readonly IPromotionsUnitOfWork _unitOfWork;

    public CreateCouponCommandHandler(ICouponRepository repository, IPromotionsUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateCouponCommand request, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByCodeAsync(request.Code, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<Guid>(Error.Conflict("Coupon.DuplicateCode", $"A coupon with code '{request.Code}' already exists."));
        }

        var result = Coupon.Create(
            request.Code, request.DiscountType, request.Value, request.Currency,
            request.ExpiresAtUtc, request.UsageLimit, request.MinimumOrderAmount);

        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        var coupon = result.Value;
        await _repository.AddAsync(coupon, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(coupon.Id);
    }
}

public sealed record DeactivateCouponCommand(Guid CouponId) : ICommand<Unit>;

public sealed class DeactivateCouponCommandHandler : IRequestHandler<DeactivateCouponCommand, Unit>
{
    private readonly ICouponRepository _repository;
    private readonly IPromotionsUnitOfWork _unitOfWork;

    public DeactivateCouponCommandHandler(ICouponRepository repository, IPromotionsUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(DeactivateCouponCommand request, CancellationToken cancellationToken = default)
    {
        var coupon = await _repository.GetByIdAsync(request.CouponId, cancellationToken);
        if (coupon is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Coupon.NotFound", "Coupon was not found."));
        }

        coupon.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record ActivateCouponCommand(Guid CouponId) : ICommand<Unit>;

public sealed class ActivateCouponCommandHandler : IRequestHandler<ActivateCouponCommand, Unit>
{
    private readonly ICouponRepository _repository;
    private readonly IPromotionsUnitOfWork _unitOfWork;

    public ActivateCouponCommandHandler(ICouponRepository repository, IPromotionsUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(ActivateCouponCommand request, CancellationToken cancellationToken = default)
    {
        var coupon = await _repository.GetByIdAsync(request.CouponId, cancellationToken);
        if (coupon is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Coupon.NotFound", "Coupon was not found."));
        }

        coupon.Activate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record CouponDto(
    Guid Id, string Code, string DiscountType, decimal Value, string Currency, bool IsActive,
    DateTime? ExpiresAtUtc, int? UsageLimit, int UsageCount, decimal? MinimumOrderAmount);

public sealed record ListCouponsQuery : IQuery<IReadOnlyList<CouponDto>>;

public sealed class ListCouponsQueryHandler : IRequestHandler<ListCouponsQuery, IReadOnlyList<CouponDto>>
{
    private readonly IPromotionsQueries _queries;

    public ListCouponsQueryHandler(IPromotionsQueries queries) => _queries = queries;

    public async Task<Result<IReadOnlyList<CouponDto>>> Handle(ListCouponsQuery request, CancellationToken cancellationToken = default) =>
        Result.Success(await _queries.ListAsync(cancellationToken));
}

public interface IPromotionsQueries
{
    Task<IReadOnlyList<CouponDto>> ListAsync(CancellationToken cancellationToken = default);
}
