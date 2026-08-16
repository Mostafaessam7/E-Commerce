using EventBus;
using Payments.Domain;

namespace Payments.Application.Abstractions;

public interface IPaymentTransactionRepository
{
    Task AddAsync(PaymentTransaction payment, CancellationToken cancellationToken = default);

    Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Idempotency ledger — Section 9's "duplicate webhook / delayed webhook / retry" handling.
/// One row per (Provider, ProviderEventId) actually processed; a redelivery of the same event id
/// is recognized here before it ever reaches domain logic.
/// </summary>
public interface IWebhookEventRepository
{
    Task<bool> HasBeenProcessedAsync(string provider, string providerEventId, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(string provider, string providerEventId, DateTime processedAtUtc, CancellationToken cancellationToken = default);
}

public interface IPaymentsUnitOfWork
{
    void EnqueueIntegrationEvent(IIntegrationEvent integrationEvent);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
