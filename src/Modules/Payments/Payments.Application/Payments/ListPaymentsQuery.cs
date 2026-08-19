using Messaging;
using Ordering.Contracts;
using Payments.Application.Abstractions;
using SharedKernel.Results;

namespace Payments.Application.Payments;

public sealed record PaymentListItemDto(
    Guid Id, Guid OrderId, string Provider, string Status, decimal Amount, decimal RefundedAmount, string Currency,
    DateTime CreatedAtUtc, string? OrderNumber = null);

/// <summary>Admin listing — every payment transaction, newest first. Optional <paramref
/// name="OrderId"/> narrows to one order's transactions (the Order admin page's "view payments"
/// link).</summary>
public sealed record ListPaymentsQuery(Guid? OrderId = null) : IQuery<IReadOnlyList<PaymentListItemDto>>;

/// <summary>
/// Enriches each row with the order's human-readable number via
/// <see cref="GetOrderContactInfoQuery"/> (ADR-014) — <see cref="IPaymentsQueries"/> itself only
/// ever knows Payments' own data (a plain <c>OrderId</c> Guid, no FK/navigation into Ordering).
/// Before this (Phase 33, ADR-044) the admin Payments page linked to the order by its raw Guid,
/// which nobody can recognize an order by.
/// </summary>
public sealed class ListPaymentsQueryHandler : IRequestHandler<ListPaymentsQuery, IReadOnlyList<PaymentListItemDto>>
{
    private readonly IPaymentsQueries _queries;
    private readonly IDispatcher _dispatcher;

    public ListPaymentsQueryHandler(IPaymentsQueries queries, IDispatcher dispatcher)
    {
        _queries = queries;
        _dispatcher = dispatcher;
    }

    public async Task<Result<IReadOnlyList<PaymentListItemDto>>> Handle(ListPaymentsQuery request, CancellationToken cancellationToken = default)
    {
        var payments = await _queries.ListAsync(request.OrderId, cancellationToken);

        var enriched = new List<PaymentListItemDto>(payments.Count);
        foreach (var payment in payments)
        {
            var contactInfo = await _dispatcher.Send(new GetOrderContactInfoQuery(payment.OrderId), cancellationToken);
            enriched.Add(contactInfo.IsSuccess ? payment with { OrderNumber = contactInfo.Value.OrderNumber } : payment);
        }

        return Result.Success<IReadOnlyList<PaymentListItemDto>>(enriched);
    }
}

public interface IPaymentsQueries
{
    Task<IReadOnlyList<PaymentListItemDto>> ListAsync(Guid? orderId, CancellationToken cancellationToken = default);
}
