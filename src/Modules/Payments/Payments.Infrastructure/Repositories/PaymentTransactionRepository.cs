using Microsoft.EntityFrameworkCore;
using Payments.Application.Abstractions;
using Payments.Domain;
using Payments.Infrastructure.Persistence;

namespace Payments.Infrastructure.Repositories;

internal sealed class PaymentTransactionRepository : IPaymentTransactionRepository
{
    private readonly PaymentsDbContext _db;

    public PaymentTransactionRepository(PaymentsDbContext db) => _db = db;

    public async Task AddAsync(PaymentTransaction payment, CancellationToken cancellationToken = default) =>
        await _db.PaymentTransactions.AddAsync(payment, cancellationToken);

    public Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.PaymentTransactions.Include(p => p.Refunds).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
}
