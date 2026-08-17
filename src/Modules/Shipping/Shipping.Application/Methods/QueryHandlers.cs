using Messaging;
using Shipping.Application.Abstractions;
using Shipping.Contracts;
using SharedKernel.Results;

namespace Shipping.Application.Methods;

public sealed class ListShippingMethodsQueryHandler : IRequestHandler<ListShippingMethodsQuery, IReadOnlyList<ShippingMethodDto>>
{
    private readonly IShippingQueries _queries;

    public ListShippingMethodsQueryHandler(IShippingQueries queries) => _queries = queries;

    public async Task<Result<IReadOnlyList<ShippingMethodDto>>> Handle(ListShippingMethodsQuery request, CancellationToken cancellationToken = default) =>
        Result.Success(await _queries.ListAsync(request.IncludeInactive, cancellationToken));
}

public sealed class GetShippingMethodQueryHandler : IRequestHandler<GetShippingMethodQuery, ShippingMethodDto>
{
    private readonly IShippingMethodRepository _repository;

    public GetShippingMethodQueryHandler(IShippingMethodRepository repository) => _repository = repository;

    public async Task<Result<ShippingMethodDto>> Handle(GetShippingMethodQuery request, CancellationToken cancellationToken = default)
    {
        var method = await _repository.GetByIdAsync(request.ShippingMethodId, cancellationToken);
        if (method is null)
        {
            return Result.Failure<ShippingMethodDto>(Error.NotFound("ShippingMethod.NotFound", "Shipping method was not found."));
        }

        if (!method.IsActive)
        {
            return Result.Failure<ShippingMethodDto>(Error.Conflict("ShippingMethod.Inactive", "This shipping method is no longer available."));
        }

        return Result.Success(new ShippingMethodDto(
            method.Id, method.Name, method.Description, method.Cost.Amount, method.Cost.Currency,
            method.EstimatedDaysMin, method.EstimatedDaysMax, method.IsActive));
    }
}
