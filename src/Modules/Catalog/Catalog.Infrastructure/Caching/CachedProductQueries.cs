using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Catalog.Application.Products;
using Catalog.Contracts;
using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.Infrastructure.Caching;

/// <summary>
/// Decorates the real <see cref="IProductQueries"/> with a Redis-backed read-through cache for
/// the two storefront-facing reads (product detail page, storefront search/listing) — the first
/// real reader of the Redis container docker-compose.yml has provisioned since Phase 13.
///
/// <see cref="GetVariantSnapshotAsync"/> is deliberately never cached: it's what checkout
/// re-validates price/stock-availability against on every request (ADR-014) — a stale price or
/// "still active" flag there is a financial-correctness bug, not a performance tradeoff worth
/// making. Admin listings (<see cref="ProductSearchCriteria.IncludeAllStatuses"/>) skip the cache
/// too — an admin who just published or archived a product needs to see that immediately, not up
/// to <see cref="SearchTtl"/> stale.
/// </summary>
internal sealed class CachedProductQueries : IProductQueries
{
    private static readonly TimeSpan SlugTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SearchTtl = TimeSpan.FromSeconds(30);

    private readonly IProductQueries _inner;
    private readonly IDistributedCache _cache;

    public CachedProductQueries(IProductQueries inner, IDistributedCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public Task<ProductVariantSnapshotDto?> GetVariantSnapshotAsync(Guid productVariantId, CancellationToken cancellationToken = default) =>
        _inner.GetVariantSnapshotAsync(productVariantId, cancellationToken);

    public async Task<ProductDetailsDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var key = $"catalog:product:slug:{slug.Trim().ToLowerInvariant()}";

        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            // Empty string is the "confirmed not found" sentinel — IDistributedCache has no
            // separate way to distinguish "cached null" from "no entry", so a 404 gets cached too
            // (avoids hammering the DB for a slug that doesn't exist, same TTL as a hit).
            return cached.Length == 0 ? null : JsonSerializer.Deserialize<ProductDetailsDto>(cached);
        }

        var result = await _inner.GetBySlugAsync(slug, cancellationToken);
        await _cache.SetStringAsync(
            key,
            result is null ? string.Empty : JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = SlugTtl },
            cancellationToken);

        return result;
    }

    public async Task<ProductSearchResultDto> SearchAsync(ProductSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        if (criteria.IncludeAllStatuses)
        {
            return await _inner.SearchAsync(criteria, cancellationToken);
        }

        var key = $"catalog:product:search:{ComputeCriteriaKey(criteria)}";

        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<ProductSearchResultDto>(cached)!;
        }

        var result = await _inner.SearchAsync(criteria, cancellationToken);
        await _cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = SearchTtl },
            cancellationToken);

        return result;
    }

    /// <summary>Every filter/sort/paging field folded into one key — TTL-only invalidation (no
    /// write-side eviction) means a stale search result self-heals within <see cref="SearchTtl"/>
    /// rather than needing every product-write handler to know which cache keys it might have
    /// invalidated.</summary>
    private static string ComputeCriteriaKey(ProductSearchCriteria criteria)
    {
        var raw = string.Join(
            '|',
            criteria.SearchTerm, criteria.CategoryId, criteria.BrandId, criteria.MinPrice, criteria.MaxPrice,
            criteria.FeaturedOnly, criteria.Page, criteria.PageSize, criteria.SortBy);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
