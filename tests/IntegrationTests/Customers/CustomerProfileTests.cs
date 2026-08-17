using Customers.Application.Profile;
using Customers.Infrastructure;
using Customers.Infrastructure.Persistence;
using FluentAssertions;
using Infrastructure;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Security;

namespace IntegrationTests.Customers;

public sealed class CustomerProfileTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True";

    private ServiceProvider _provider = null!;
    private Guid _customerId;

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
        services.AddCustomersModule(configuration);

        _provider = services.BuildServiceProvider();
        _customerId = Guid.NewGuid();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        await db.Customers.Where(c => c.Id == _customerId).ExecuteDeleteAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task GetOrCreate_is_idempotent_and_address_lifecycle_round_trips_through_the_real_db()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var first = await dispatcher.Send(new GetOrCreateCustomerCommand(_customerId, "profile-test@example.com"));
        var second = await dispatcher.Send(new GetOrCreateCustomerCommand(_customerId, "profile-test@example.com"));
        first.Value.Id.Should().Be(second.Value.Id, "GetOrCreate must not create a duplicate profile on a second call");

        var addResult = await dispatcher.Send(new AddAddressCommand(
            _customerId, "Home", "Sara Adel", "+201000000005", "5 Test St", null, "Giza", null, "12511", "EG", IsDefault: false));
        addResult.IsSuccess.Should().BeTrue();

        var profile = (await dispatcher.Send(new GetCustomerProfileQuery(_customerId))).Value;
        profile.Addresses.Should().ContainSingle(a => a.Id == addResult.Value && a.IsDefault, "the first address ever added becomes the default");

        var updateResult = await dispatcher.Send(new UpdateProfileCommand(_customerId, "Sara Adel", "+201000000006"));
        updateResult.IsSuccess.Should().BeTrue();

        var removeResult = await dispatcher.Send(new RemoveAddressCommand(_customerId, addResult.Value));
        removeResult.IsSuccess.Should().BeTrue();

        var profileAfterRemove = (await dispatcher.Send(new GetCustomerProfileQuery(_customerId))).Value;
        profileAfterRemove.FullName.Should().Be("Sara Adel");
        profileAfterRemove.Addresses.Should().BeEmpty();
    }
}
