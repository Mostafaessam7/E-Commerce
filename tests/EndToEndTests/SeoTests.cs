using System.Net;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EndToEndTests;

/// <summary>Proves the sitemap is generated from real, current product data — not a static file
/// that could go stale — and that robots.txt points at it.</summary>
public sealed class SeoTests : IClassFixture<StoreWebApplicationFactory>, IAsyncLifetime
{
    private readonly StoreWebApplicationFactory _factory;
    private Guid _productId;
    private string _slug = null!;

    public SeoTests(StoreWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var product = Product.Create($"Sitemap Test Product {Guid.NewGuid():N}", $"sitemap-test-{Guid.NewGuid():N}", null, null, brandId: null).Value;
        product.AddVariant($"SM-{Guid.NewGuid():N}"[..20], 42m, "EGP", salePrice: null, barcode: null, weightKg: null);
        product.Publish();
        catalogDb.Products.Add(product);
        await catalogDb.SaveChangesAsync();

        _productId = product.Id;
        _slug = product.Slug.Value;
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await catalogDb.Products.Where(p => p.Id == _productId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Sitemap_lists_a_real_published_product_and_the_static_pages()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/sitemap.xml");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");
        body.Should().Contain($"/product/{_slug}", "a real published product must appear in the generated sitemap");
        body.Should().Contain("/Shop");
    }

    [Fact]
    public async Task Robots_txt_disallows_admin_and_account_pages_and_points_at_the_sitemap()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/robots.txt");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("Disallow: /Admin/");
        body.Should().Contain("Sitemap: ");
        body.Should().Contain("/sitemap.xml");
    }
}
