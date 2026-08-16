using EventBus;
using Microsoft.EntityFrameworkCore;
using Payments.Domain;
using Persistence;

namespace Payments.Infrastructure.Persistence;

public sealed class PaymentsDbContext : AppDbContextBase
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options)
        : base(options)
    {
    }

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();

    protected override string SchemaName => "payments";

    public void EnqueueIntegrationEvent(IIntegrationEvent integrationEvent) => EnqueueOutboxMessage(integrationEvent);
}
