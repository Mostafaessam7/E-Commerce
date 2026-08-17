using Microsoft.EntityFrameworkCore;
using Payments.Application.Payments;
using Payments.Infrastructure.Persistence;

namespace Payments.Infrastructure.Repositories;

internal sealed class PaymentsQueries : IPaymentsQueries
{
    private readonly PaymentsDbContext _db;

    public PaymentsQueries(PaymentsDbContext db) => _db = db;

    public async Task<IReadOnlyList<PaymentListItemDto>> ListAsync(Guid? orderId, CancellationToken cancellationToken = default)
    {
        var query = _db.PaymentTransactions.AsNoTracking().AsQueryable();
        if (orderId is Guid id)
        {
            query = query.Where(p => p.OrderId == id);
        }

        return await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new PaymentListItemDto(
                p.Id, p.OrderId, p.Provider, p.Status.ToString(), p.Amount.Amount, p.RefundedAmount.Amount, p.Amount.Currency, p.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
