using Messaging;
using Payments.Application.Abstractions;
using SharedKernel.Results;

namespace Payments.Application.Payments;

public sealed record PaymentListItemDto(
    Guid Id, Guid OrderId, string Provider, string Status, decimal Amount, decimal RefundedAmount, string Currency, DateTime CreatedAtUtc);

/// <summary>Admin listing — every payment transaction, newest first. Optional <paramref
/// name="OrderId"/> narrows to one order's transactions (the Order admin page's "view payments"
/// link).</summary>
public sealed record ListPaymentsQuery(Guid? OrderId = null) : IQuery<IReadOnlyList<PaymentListItemDto>>;

public sealed class ListPaymentsQueryHandler : IRequestHandler<ListPaymentsQuery, IReadOnlyList<PaymentListItemDto>>
{
    private readonly IPaymentsQueries _queries;

    public ListPaymentsQueryHandler(IPaymentsQueries queries) => _queries = queries;

    public async Task<Result<IReadOnlyList<PaymentListItemDto>>> Handle(ListPaymentsQuery request, CancellationToken cancellationToken = default) =>
        Result.Success(await _queries.ListAsync(request.OrderId, cancellationToken));
}

public interface IPaymentsQueries
{
    Task<IReadOnlyList<PaymentListItemDto>> ListAsync(Guid? orderId, CancellationToken cancellationToken = default);
}
