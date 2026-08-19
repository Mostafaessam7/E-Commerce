using Catalog.Application.Products;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests.Catalog;

/// <summary>
/// End-to-end against the real local SQL Server DB (see docs/database.md) — not EF Core InMemory.
/// This exact class of bug (a NullReferenceException from EF Core's query shaper on a nested
/// record-constructor projection, only reproducing against the SqlServer provider's real SQL
/// translation, not InMemory) is why: <see cref="ProductQueries.GetBySlugAsync"/> crashed against
/// this provider while looking fine under InMemory. Requires `dotnet ef database update` to have
/// been run for CatalogDbContext first.
///
/// Uses a fresh <see cref="CatalogDbContext"/> per phase (write, then read), mirroring how a real
/// request gets a fresh scoped DbContext — reusing one tracked context across an artificial
/// "second update" produced misleading concurrency-check noise unrelated to the code under test.
/// </summary>
public sealed class ProductCatalogFlowTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True";

    private Guid _createdProductId;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        var product = await db.Products.FindAsync(_createdProductId);
        if (product is not null)
        {
            db.Products.Remove(product);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Creating_a_product_then_querying_it_by_slug_round_trips_correctly()
    {
        var slug = $"integration-test-{Guid.NewGuid():N}";

        await using (var writeDb = CreateContext())
        {
            var product = Product.Create("Integration Test Product", slug, "short desc", "long desc", brandId: null).Value;
            product.AddVariant("ITEST-SKU-1", 99.99m, "USD", salePrice: 79.99m, barcode: null, weightKg: 0.5m);
            product.Publish();

            writeDb.Products.Add(product);
            await writeDb.SaveChangesAsync();

            _createdProductId = product.Id;
        }

        await using var readDb = CreateContext();
        var queries = new ProductQueries(readDb);

        var dto = await queries.GetBySlugAsync(slug);

        dto.Should().NotBeNull();
        dto!.Name.Should().Be("Integration Test Product");
        dto.Status.Should().Be("Active");
        dto.Variants.Should().ContainSingle(v => v.Sku == "ITEST-SKU-1" && v.SalePrice == 79.99m);

        var searchResult = await queries.SearchAsync(new ProductSearchCriteria(SearchTerm: "Integration Test Product"));
        searchResult.Items.Should().Contain(i => i.Slug == slug);
    }

    [Fact]
    public async Task Adding_then_removing_a_product_image_round_trips_and_promotes_the_next_primary()
    {
        var slug = $"integration-test-{Guid.NewGuid():N}";
        Guid firstImageId;
        Guid secondImageId;

        await using (var writeDb = CreateContext())
        {
            var product = Product.Create("Image Test Product", slug, "short desc", "long desc", brandId: null).Value;
            product.AddVariant("ITEST-IMG-1", 49.99m, "USD", salePrice: null, barcode: null, weightKg: null);
            writeDb.Products.Add(product);
            await writeDb.SaveChangesAsync();
            _createdProductId = product.Id;
        }

        await using (var db1 = CreateContext())
        {
            var product = await db1.Products.Include(p => p.Images).FirstAsync(p => p.Id == _createdProductId);
            product.AddImage("/uploads/products/x/first.jpg", "first", isPrimary: true);
            product.AddImage("/uploads/products/x/second.jpg", "second", isPrimary: false);
            await db1.SaveChangesAsync();

            firstImageId = product.Images.Single(i => i.Url.EndsWith("first.jpg", StringComparison.Ordinal)).Id;
            secondImageId = product.Images.Single(i => i.Url.EndsWith("second.jpg", StringComparison.Ordinal)).Id;
        }

        await using (var db2 = CreateContext())
        {
            var product = await db2.Products.Include(p => p.Images).FirstAsync(p => p.Id == _createdProductId);
            product.Images.Should().HaveCount(2);
            product.Images.Single(i => i.Id == firstImageId).IsPrimary.Should().BeTrue();

            var removeResult = product.RemoveImage(firstImageId);
            removeResult.IsSuccess.Should().BeTrue();
            await db2.SaveChangesAsync();
        }

        await using var readDb = CreateContext();
        var reloaded = await readDb.Products.Include(p => p.Images).FirstAsync(p => p.Id == _createdProductId);
        reloaded.Images.Should().ContainSingle(i => i.Id == secondImageId);
        reloaded.Images.Single().IsPrimary.Should().BeTrue("removing the primary image should promote the remaining one");
    }

    private static CatalogDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(ConnectionString).Options);
}
