using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace TownSuite.MultiTenant.Tests;

public class TenantResolver_Tests
{
    private IConfiguration config;
    private Settings settings;

    [SetUp]
    public void Setup()
    {
        config = new ConfigurationBuilder()
            .AddJsonFile("http_reader_test.json")
            .AddEnvironmentVariables()
            .Build();

        settings = config.GetSection("TenantSettings").Get<Settings>();
    }
    
    [Test]
    public async Task CanResolveAllTenants_Test()
    {
        var fakeHttpWebClient = new FakeHttpClient(new HttpClient(), "", "");
        var logger = Mock.Of<ILogger<HttpConfigReader>>();
        var reader = new HttpConfigReader(logger, new IdFaker(), fakeHttpWebClient, settings);
        await reader.Refresh();

        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);
        resolver.Clear();
        Assert.That(resolver.Tenants.Count, Is.EqualTo(0));
        await resolver.ResolveAll();
        
        Assert.That(resolver.Tenants.Count, Is.EqualTo(3));
        Assert.That(resolver.Tenants.Any(t => string.Equals(t.Key, "tenant1")));
        Assert.That(resolver.Tenants.Any(t => string.Equals(t.Key, "tenant2")));
        Assert.That(resolver.Tenants.Any(t => string.Equals(t.Key, "tenant3")));
    }
}