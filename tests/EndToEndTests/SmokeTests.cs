using System.Net;
using FluentAssertions;

namespace EndToEndTests;

public sealed class SmokeTests : IClassFixture<StoreWebApplicationFactory>
{
    private readonly StoreWebApplicationFactory _factory;

    public SmokeTests(StoreWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Home_page_loads_through_the_real_pipeline()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy_against_the_real_shared_database()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be("Healthy");
    }
}
