using Messaging;
using Ordering.Application.Abstractions;
using SharedKernel.Results;

namespace Ordering.Application.Carts;

public sealed record GetCartQuery(Guid CartId) : IQuery<CartDto>;

public sealed class GetCartQueryHandler : IRequestHandler<GetCartQuery, CartDto>
{
    private readonly ICartRepository _repository;

    public GetCartQueryHandler(ICartRepository repository) => _repository = repository;

    public async Task<Result<CartDto>> Handle(GetCartQuery request, CancellationToken cancellationToken = default)
    {
        var cart = await _repository.GetByIdAsync(request.CartId, cancellationToken);

        return cart is null
            ? Result.Failure<CartDto>(Error.NotFound("Cart.NotFound", "Cart was not found."))
            : Result.Success(CartMapper.ToDto(cart));
    }
}
