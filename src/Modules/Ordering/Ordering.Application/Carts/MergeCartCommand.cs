using Messaging;
using Ordering.Application.Abstractions;
using SharedKernel.Results;

namespace Ordering.Application.Carts;

/// <summary>Called once at login (Section 6: "Merge Guest Cart after Login") — Store.Web's
/// Account controller dispatches this after a successful sign-in, passing the guest cart id it
/// had in a cookie and the now-known customer id.</summary>
public sealed record MergeCartCommand(Guid CustomerId, Guid GuestAnonymousId) : ICommand<CartDto>;

public sealed class MergeCartCommandHandler : IRequestHandler<MergeCartCommand, CartDto>
{
    private readonly ICartRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;

    public MergeCartCommandHandler(ICartRepository repository, IOrderingUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CartDto>> Handle(MergeCartCommand request, CancellationToken cancellationToken = default)
    {
        var guestCart = await _repository.GetByAnonymousIdAsync(request.GuestAnonymousId, cancellationToken);
        if (guestCart is null)
        {
            // Nothing to merge — resolve (and create if needed) the customer's own cart as-is.
            var ownCart = await _repository.GetByCustomerIdAsync(request.CustomerId, cancellationToken)
                ?? await CreateCustomerCart(request.CustomerId, cancellationToken);

            return Result.Success(CartMapper.ToDto(ownCart));
        }

        var customerCart = await _repository.GetByCustomerIdAsync(request.CustomerId, cancellationToken)
            ?? await CreateCustomerCart(request.CustomerId, cancellationToken);

        customerCart.MergeFrom(guestCart);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(CartMapper.ToDto(customerCart));
    }

    private async Task<Domain.Cart> CreateCustomerCart(Guid customerId, CancellationToken cancellationToken)
    {
        var cart = Domain.Cart.CreateForCustomer(customerId);
        await _repository.AddAsync(cart, cancellationToken);
        return cart;
    }
}
