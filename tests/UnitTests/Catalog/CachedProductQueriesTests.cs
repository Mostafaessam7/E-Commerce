using Catalog.Application.Products;
using Catalog.Contracts;
using Catalog.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace UnitTests.Catalog;

public class CachedProductQueriesTests
{
    private static MemoryDistributedCache NewMemoryCache() => new(Options.Create(new MemoryDistributedCacheOptions()));

    private static ProductDetailsDto MakeDto(string name) =>
        new(Guid.NewGuid(), name, "some-slug", null, null, null, "Active", false, null, null, [], [], [], []);

    [Fact]
    public async Task GetBySlugAsync_hits_the_inner_query_only_once_for_repeated_calls()
    {
        var inner = new CountingProductQueries(MakeDto("First Call"));
        var cached = new CachedProductQueries(inner, NewMemoryCache());

        var first = await cached.GetBySlugAsync("some-slug");
        var second = await cached.GetBySlugAsync("some-slug");

        inner.SlugCallCount.Should().Be(1, "the second call must be served from cache, not the inner query");
        first!.Name.Should().Be("First Call");
        second!.Name.Should().Be("First Call", "a cached response must be returned verbatim, not re-fetched");
    }

    [Fact]
    public async Task GetBySlugAsync_caches_a_miss_too_so_a_repeated_lookup_for_a_nonexistent_slug_does_not_hit_the_inner_query_again()
    {
        var inner = new CountingProductQueries(result: null);
        var cached = new CachedProductQueries(inner, NewMemoryCache());

        (await cached.GetBySlugAsync("missing")).Should().BeNull();
        (await cached.GetBySlugAsync("missing")).Should().BeNull();

        inner.SlugCallCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_hits_the_inner_query_only_once_for_the_same_criteria()
    {
        var inner = new CountingProductQueries(searchResult: new ProductSearchResultDto([], 0, 1, 20));
        var cached = new CachedProductQueries(inner, NewMemoryCache());
        var criteria = new ProductSearchCriteria(SearchTerm: "shoes", Page: 1, PageSize: 20);

        await cached.SearchAsync(criteria);
        await cached.SearchAsync(criteria);

        inner.SearchCallCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_treats_different_criteria_as_different_cache_entries()
    {
        var inner = new CountingProductQueries(searchResult: new ProductSearchResultDto([], 0, 1, 20));
        var cached = new CachedProductQueries(inner, NewMemoryCache());

        await cached.SearchAsync(new ProductSearchCriteria(SearchTerm: "shoes", Page: 1, PageSize: 20));
        await cached.SearchAsync(new ProductSearchCriteria(SearchTerm: "hats", Page: 1, PageSize: 20));

        inner.SearchCallCount.Should().Be(2, "different search criteria must not collide on the same cache key");
    }

    [Fact]
    public async Task SearchAsync_skips_the_cache_entirely_for_admin_listings()
    {
        var inner = new CountingProductQueries(searchResult: new ProductSearchResultDto([], 0, 1, 20));
        var cached = new CachedProductQueries(inner, NewMemoryCache());
        var adminCriteria = new ProductSearchCriteria(Page: 1, PageSize: 20, IncludeAllStatuses: true);

        await cached.SearchAsync(adminCriteria);
        await cached.SearchAsync(adminCriteria);

        inner.SearchCallCount.Should().Be(2, "an admin listing must always see current data, never a cached snapshot");
    }

    [Fact]
    public async Task GetVariantSnapshotAsync_is_never_cached()
    {
        var inner = new CountingProductQueries(variantSnapshot: new ProductVariantSnapshotDto(Guid.NewGuid(), Guid.NewGuid(), "P", "SKU", 10m, null, "EGP", true));
        var cached = new CachedProductQueries(inner, NewMemoryCache());
        var variantId = Guid.NewGuid();

        await cached.GetVariantSnapshotAsync(variantId);
        await cached.GetVariantSnapshotAsync(variantId);

        inner.VariantCallCount.Should().Be(2, "checkout must always re-validate price/stock against fresh data, never a cached one");
    }

    private sealed class CountingProductQueries : IProductQueries
    {
        private readonly ProductDetailsDto? _result;
        private readonly ProductSearchResultDto? _searchResult;
        private readonly ProductVariantSnapshotDto? _variantSnapshot;

        public CountingProductQueries(
            ProductDetailsDto? result = null, ProductSearchResultDto? searchResult = null, ProductVariantSnapshotDto? variantSnapshot = null)
        {
            _result = result;
            _searchResult = searchResult;
            _variantSnapshot = variantSnapshot;
        }

        public int SlugCallCount { get; private set; }

        public int SearchCallCount { get; private set; }

        public int VariantCallCount { get; private set; }

        public Task<ProductDetailsDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            SlugCallCount++;
            return Task.FromResult(_result);
        }

        public Task<ProductSearchResultDto> SearchAsync(ProductSearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            SearchCallCount++;
            return Task.FromResult(_searchResult!);
        }

        public Task<ProductVariantSnapshotDto?> GetVariantSnapshotAsync(Guid productVariantId, CancellationToken cancellationToken = default)
        {
            VariantCallCount++;
            return Task.FromResult(_variantSnapshot);
        }
    }
}
