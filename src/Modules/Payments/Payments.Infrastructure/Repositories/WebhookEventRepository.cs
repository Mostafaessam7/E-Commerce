using Microsoft.EntityFrameworkCore;
using Payments.Application.Abstractions;
using Payments.Infrastructure.Persistence;

namespace Payments.Infrastructure.Repositories;

internal sealed class WebhookEventRepository : IWebhookEventRepository
{
    private readonly PaymentsDbContext _db;

    public WebhookEventRepository(PaymentsDbContext db) => _db = db;

    public Task<bool> HasBeenProcessedAsync(string provider, string providerEventId, CancellationToken cancellationToken = default) =>
        _db.ProcessedWebhookEvents.AnyAsync(e => e.Provider == provider && e.ProviderEventId == providerEventId, cancellationToken);

    public async Task MarkProcessedAsync(string provider, string providerEventId, DateTime processedAtUtc, CancellationToken cancellationToken = default) =>
        await _db.ProcessedWebhookEvents.AddAsync(
            new ProcessedWebhookEvent(Guid.NewGuid(), provider, providerEventId, processedAtUtc), cancellationToken);
}
