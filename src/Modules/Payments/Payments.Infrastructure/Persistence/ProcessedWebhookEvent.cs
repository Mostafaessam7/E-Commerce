namespace Payments.Infrastructure.Persistence;

/// <summary>
/// Idempotency ledger row — infrastructure-only, not a domain concept (see
/// <c>Payments.Application.Abstractions.IWebhookEventRepository</c>). One row per
/// (Provider, ProviderEventId) actually processed.
/// </summary>
public sealed class ProcessedWebhookEvent
{
    public ProcessedWebhookEvent(Guid id, string provider, string providerEventId, DateTime processedAtUtc)
    {
        Id = id;
        Provider = provider;
        ProviderEventId = providerEventId;
        ProcessedAtUtc = processedAtUtc;
    }

    private ProcessedWebhookEvent()
    {
    }

    public Guid Id { get; private set; }

    public string Provider { get; private set; } = null!;

    public string ProviderEventId { get; private set; } = null!;

    public DateTime ProcessedAtUtc { get; private set; }
}
