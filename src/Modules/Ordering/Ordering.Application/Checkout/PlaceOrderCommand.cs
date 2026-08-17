using Catalog.Contracts;
using Infrastructure;
using Inventory.Contracts;
using Messaging;
using Ordering.Application.Abstractions;
using Ordering.Contracts;
using Ordering.Domain;
using Ordering.Domain.ValueObjects;
using Promotions.Contracts;
using SharedKernel.Results;

namespace Ordering.Application.Checkout;

public sealed record AddressInput(string FullName, string Phone, string Line1, string? Line2, string City, string? State, string PostalCode, string Country);

public sealed record PlaceOrderCommand(
    Guid CartId,
    Guid? CustomerId,
    string Email,
    AddressInput BillingAddress,
    AddressInput ShippingAddress,
    decimal ShippingCost,
    string? Notes) : ICommand<Guid>;

/// <summary>
/// Section 6/7/8's checkout: re-validates every line's price and availability against Catalog
/// (never trusts the cart's stale snapshot), reserves stock in Inventory per line — releasing
/// whatever already succeeded if a later line fails (Section 5: never oversell) — then places the
/// order and clears the cart, all through the shared <c>IDispatcher</c> and each module's
/// Contracts (ADR-014), never a direct cross-module reference.
/// </summary>
public sealed class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Guid>
{
    // Placeholder flat rate pending a real Tax module/config (Section 4 mentions "VAT/Tax
    // configuration" but no such module exists in the fixed 10 — revisit if/when one is added).
    private const decimal TaxRate = 0.14m;

    private readonly ICartRepository _cartRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly IDispatcher _dispatcher;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PlaceOrderCommandHandler(
        ICartRepository cartRepository,
        IOrderRepository orderRepository,
        IOrderingUnitOfWork unitOfWork,
        IDispatcher dispatcher,
        IDateTimeProvider dateTimeProvider)
    {
        _cartRepository = cartRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepository.GetByIdAsync(request.CartId, cancellationToken);
        if (cart is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Cart.NotFound", "Cart was not found."));
        }

        if (cart.Items.Count == 0)
        {
            return Result.Failure<Guid>(Error.Validation("Cart.Empty", "Cannot check out an empty cart."));
        }

        var billingResult = Address.Create(request.BillingAddress.FullName, request.BillingAddress.Phone, request.BillingAddress.Line1,
            request.BillingAddress.Line2, request.BillingAddress.City, request.BillingAddress.State, request.BillingAddress.PostalCode, request.BillingAddress.Country);
        var shippingResult = Address.Create(request.ShippingAddress.FullName, request.ShippingAddress.Phone, request.ShippingAddress.Line1,
            request.ShippingAddress.Line2, request.ShippingAddress.City, request.ShippingAddress.State, request.ShippingAddress.PostalCode, request.ShippingAddress.Country);

        if (billingResult.IsFailure)
        {
            return Result.Failure<Guid>(billingResult.Error);
        }

        if (shippingResult.IsFailure)
        {
            return Result.Failure<Guid>(shippingResult.Error);
        }

        // Re-validate price and availability for every line — never trust what's on the cart
        // (Section 6). Collected up front so we don't reserve stock for a line that's about to
        // fail the price/availability check anyway.
        var lines = new List<(Guid ProductVariantId, Guid ProductId, string ProductName, string Sku, decimal UnitPrice, string Currency, int Quantity)>();

        foreach (var item in cart.Items)
        {
            var snapshotResult = await _dispatcher.Send(new GetProductVariantSnapshotQuery(item.ProductVariantId), cancellationToken);
            if (snapshotResult.IsFailure)
            {
                return Result.Failure<Guid>(snapshotResult.Error);
            }

            var snapshot = snapshotResult.Value;
            if (!snapshot.IsPurchasable)
            {
                return Result.Failure<Guid>(Error.Conflict(
                    "Checkout.ProductUnavailable", $"'{snapshot.ProductName}' is no longer available for purchase."));
            }

            lines.Add((snapshot.ProductVariantId, snapshot.ProductId, snapshot.ProductName, snapshot.Sku,
                snapshot.SalePrice ?? snapshot.Price, snapshot.Currency, item.Quantity));
        }

        var currency = lines[0].Currency;
        var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
        var tax = Math.Round(subtotal * TaxRate, 2);

        // Never trust the cart's stored coupon code (Cart.ApplyCoupon just stores a string, no
        // validation) — re-validate and redeem it here, same rule as price/stock above. Redeeming
        // increments the coupon's usage count immediately; if anything after this point fails to
        // place the order, it must be released (ADR-014's compensation pattern, same shape as
        // stock reservation below).
        var discount = 0m;
        var redeemedCouponCode = (string?)null;

        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            var redeemResult = await _dispatcher.Send(new RedeemCouponCommand(cart.CouponCode, subtotal, currency), cancellationToken);
            if (redeemResult.IsFailure)
            {
                return Result.Failure<Guid>(redeemResult.Error);
            }

            discount = redeemResult.Value;
            redeemedCouponCode = cart.CouponCode;
        }

        var orderResult = Order.Place(
            GenerateOrderNumber(_dateTimeProvider.UtcNow), request.CustomerId, request.Email, billingResult.Value, shippingResult.Value,
            lines, request.ShippingCost, tax, discount, currency, request.Notes, _dateTimeProvider.UtcNow);

        if (orderResult.IsFailure)
        {
            if (redeemedCouponCode is not null)
            {
                await _dispatcher.Send(new ReleaseCouponCommand(redeemedCouponCode), cancellationToken);
            }

            return Result.Failure<Guid>(orderResult.Error);
        }

        var order = orderResult.Value;

        // Reserve stock per line, tagged with this order's id; if any line fails, release
        // everything already reserved for this order (compensation — Section 5, never oversell) —
        // and the coupon redemption too, for the same reason.
        var reserved = new List<(Guid ProductVariantId, int Quantity)>();

        foreach (var line in order.Items)
        {
            var reserveResult = await _dispatcher.Send(
                new ReserveStockCommand(line.ProductVariantId, line.Quantity, order.Id), cancellationToken);

            if (reserveResult.IsFailure)
            {
                foreach (var (variantId, quantity) in reserved)
                {
                    await _dispatcher.Send(new ReleaseStockCommand(variantId, quantity, order.Id), cancellationToken);
                }

                if (redeemedCouponCode is not null)
                {
                    await _dispatcher.Send(new ReleaseCouponCommand(redeemedCouponCode), cancellationToken);
                }

                return Result.Failure<Guid>(reserveResult.Error);
            }

            reserved.Add((line.ProductVariantId, line.Quantity));
        }

        await _orderRepository.AddAsync(order, cancellationToken);
        _unitOfWork.EnqueueIntegrationEvent(new OrderPlacedIntegrationEvent(order.Id, order.OrderNumber, order.CustomerId, order.Email, order.Total.Amount, order.Total.Currency));

        cart.Clear();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(order.Id);
    }

    private static string GenerateOrderNumber(DateTime utcNow) =>
        $"ORD-{utcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
}
