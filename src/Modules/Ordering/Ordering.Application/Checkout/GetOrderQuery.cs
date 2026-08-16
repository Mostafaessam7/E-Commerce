using Messaging;
using Ordering.Application.Abstractions;
using Ordering.Contracts;
using Ordering.Domain;
using SharedKernel.Results;

namespace Ordering.Application.Checkout;

public sealed record OrderItemDto(Guid Id, string ProductName, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    string Email,
    string Status,
    string PaymentStatus,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Tax,
    decimal Discount,
    decimal Total,
    string Currency,
    IReadOnlyList<OrderItemDto> Items);

public sealed record GetOrderQuery(Guid OrderId) : IQuery<OrderDto>;

public sealed class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderDto>
{
    private readonly IOrderRepository _repository;

    public GetOrderQueryHandler(IOrderRepository repository) => _repository = repository;

    public async Task<Result<OrderDto>> Handle(GetOrderQuery request, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(Error.NotFound("Order.NotFound", "Order was not found."));
        }

        return Result.Success(new OrderDto(
            order.Id,
            order.OrderNumber,
            order.Email,
            order.Status.ToString(),
            order.PaymentStatus.ToString(),
            order.Subtotal.Amount,
            order.ShippingCost.Amount,
            order.Tax.Amount,
            order.Discount.Amount,
            order.Total.Amount,
            order.Total.Currency,
            order.Items.Select(i => new OrderItemDto(i.Id, i.ProductName, i.Sku, i.UnitPrice.Amount, i.Quantity, i.LineTotal.Amount)).ToList()));
    }
}

public sealed class GetOrderContactInfoQueryHandler : IRequestHandler<GetOrderContactInfoQuery, OrderContactInfoDto>
{
    private readonly IOrderRepository _repository;

    public GetOrderContactInfoQueryHandler(IOrderRepository repository) => _repository = repository;

    public async Task<Result<OrderContactInfoDto>> Handle(GetOrderContactInfoQuery request, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderContactInfoDto>(Error.NotFound("Order.NotFound", "Order was not found."));
        }

        return Result.Success(new OrderContactInfoDto(order.Id, order.OrderNumber, order.Email, order.Total.Amount, order.Total.Currency));
    }
}
