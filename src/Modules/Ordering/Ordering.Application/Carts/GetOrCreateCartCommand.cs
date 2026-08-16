using Messaging;
using Ordering.Application.Abstractions;
using SharedKernel.Results;

namespace Ordering.Application.Carts;

/// <summary>Resolves the caller's cart — by CustomerId for a signed-in user, by AnonymousId
/// (a long-lived cookie value, Store.Web's concern) for a guest — creating one if it doesn't
/// exist yet. Exactly one of the two must be supplied.</summary>
public sealed record GetOrCreateCartCommand(Guid? CustomerId, Guid? AnonymousId) : ICommand<CartDto>;

public sealed class GetOrCreateCartCommandHandler : IRequestHandler<GetOrCreateCartCommand, CartDto>
{
    private readonly ICartRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;

    public GetOrCreateCartCommandHandler(ICartRepository repository, IOrderingUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CartDto>> Handle(GetOrCreateCartCommand request, CancellationToken cancellationToken = default)
    {
        if (request.CustomerId is null && request.AnonymousId is null)
        {
            return Result.Failure<CartDto>(SharedKernel.Results.Error.Validation(
                "Cart.MissingIdentity", "Either a customer id or an anonymous id is required."));
        }

        var cart = request.CustomerId is Guid customerId
            ? await _repository.GetByCustomerIdAsync(customerId, cancellationToken)
            : await _repository.GetByAnonymousIdAsync(request.AnonymousId!.Value, cancellationToken);

        if (cart is null)
        {
            cart = request.CustomerId is Guid id
                ? Domain.Cart.CreateForCustomer(id)
                : Domain.Cart.CreateForGuest(request.AnonymousId!.Value);

            await _repository.AddAsync(cart, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(CartMapper.ToDto(cart));
    }
}
