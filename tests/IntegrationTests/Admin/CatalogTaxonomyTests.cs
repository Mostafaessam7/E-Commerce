using Catalog.Application.Brands;
using Catalog.Application.Categories;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using FluentAssertions;
using Infrastructure;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Security;

namespace IntegrationTests.Admin;

/// <summary>
/// Proves Phase 21's Brand/Category admin management is real, not just wiring: create actually
/// persists, the active-only default excludes a deactivated row, and IncludeInactive brings it
/// back — the same "list defaults to what the storefront would show" shape as every other
/// admin listing in this system.
/// </summary>
public sealed class CatalogTaxonomyTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True";

    private ServiceProvider _provider = null!;
    private Guid _brandId;
    private Guid _categoryId;

    public Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("ConnectionStrings:Database", ConnectionString)])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSecurityCore();
        services.AddMessagingCore();
        services.AddCatalogModule(configuration);

        _provider = services.BuildServiceProvider();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Brands.Where(b => b.Id == _brandId).ExecuteDeleteAsync();
        await db.Categories.IgnoreQueryFilters().Where(c => c.Id == _categoryId).ExecuteDeleteAsync();

        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Created_brand_is_active_by_default_and_disappears_from_the_active_only_list_once_deactivated()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var createResult = await dispatcher.Send(new CreateBrandCommand($"Test Brand {Guid.NewGuid():N}", $"test-brand-{Guid.NewGuid():N}"));
        createResult.IsSuccess.Should().BeTrue();
        _brandId = createResult.Value;

        var activeOnly = await dispatcher.Send(new ListBrandsQuery());
        activeOnly.Value.Should().Contain(b => b.Id == _brandId && b.IsActive);

        var deactivateResult = await dispatcher.Send(new DeactivateBrandCommand(_brandId));
        deactivateResult.IsSuccess.Should().BeTrue();

        var afterDeactivate = await dispatcher.Send(new ListBrandsQuery());
        afterDeactivate.Value.Should().NotContain(b => b.Id == _brandId, "the active-only default must exclude a deactivated brand");

        var includeInactive = await dispatcher.Send(new ListBrandsQuery(IncludeInactive: true));
        includeInactive.Value.Should().Contain(b => b.Id == _brandId && !b.IsActive);
    }

    [Fact]
    public async Task Created_category_can_be_reactivated_after_deactivation()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var createResult = await dispatcher.Send(
            new CreateCategoryCommand($"Test Category {Guid.NewGuid():N}", $"test-cat-{Guid.NewGuid():N}", ParentId: null));
        _categoryId = createResult.Value;

        (await dispatcher.Send(new DeactivateCategoryCommand(_categoryId))).IsSuccess.Should().BeTrue();
        (await dispatcher.Send(new ListCategoriesQuery())).Value.Should().NotContain(c => c.Id == _categoryId);

        (await dispatcher.Send(new ActivateCategoryCommand(_categoryId))).IsSuccess.Should().BeTrue();
        (await dispatcher.Send(new ListCategoriesQuery())).Value.Should().Contain(c => c.Id == _categoryId && c.IsActive);
    }
}
