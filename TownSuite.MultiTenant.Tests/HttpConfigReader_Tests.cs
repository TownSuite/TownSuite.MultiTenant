using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace TownSuite.MultiTenant.Tests;

public class HttpConfigReader_Tests
{
    private IConfiguration config;

    [SetUp]
    public void Setup()
    {
        config = new ConfigurationBuilder()
            .AddJsonFile("http_reader_test.json")
            .AddEnvironmentVariables()
            .Build();
    }

    [Test]
    public async Task Appsettings_Test()
    {
        var fakeHttpWebClient = new FakeHttpClient(new HttpClient(), "", "");
        var logger = Mock.Of<ILogger<HttpConfigReader>>();
        var reader = new HttpConfigReader(config, logger, new IdFaker(), fakeHttpWebClient);
        await reader.Refresh();
        var tenantOneConnections = reader.GetConnections("tenant1");

        Assert.That(tenantOneConnections.Count, Is.EqualTo(3));
        Assert.That(tenantOneConnections.FirstOrDefault(p => p.Name == "tenant1_app1").ConnStr,
            Is.EqualTo("PLACEHOLDER1"));
        Assert.That(tenantOneConnections.FirstOrDefault(p => p.Name == "tenant1_app2").ConnStr,
            Is.EqualTo("PLACEHOLDER2"));
        Assert.That(
            tenantOneConnections.FirstOrDefault(p => p.Name == "tenant1_a.dns.record.as.tenant.townsuite.com_app1")
                .ConnStr,
            Is.EqualTo("tenant 1 alias"));

        var tenantTwoConnections = reader.GetConnections("tenant2");
        Assert.That(tenantTwoConnections.Count, Is.EqualTo(3));
        Assert.That(tenantTwoConnections.FirstOrDefault(p => p.Name == "tenant2_app1").ConnStr,
            Is.EqualTo("PLACEHOLDER3"));
        Assert.That(tenantTwoConnections.FirstOrDefault(p => p.Name == "tenant2_app2").ConnStr,
            Is.EqualTo("PLACEHOLDER4"));
        Assert.That(
            tenantTwoConnections.FirstOrDefault(p => p.Name == "tenant2_second.dns.record.as.tenant.townsuite.com_app1")
                .ConnStr,
            Is.EqualTo("tenant 2 alias"));
    }

    [Test]
    public async Task WithTenantResolverAsync_Test()
    {
        var fakeHttpWebClient = new FakeHttpClient(new HttpClient(), "", "");
        var logger = Mock.Of<ILogger<HttpConfigReader>>();
        var reader = new HttpConfigReader(config, logger, new IdFaker(), fakeHttpWebClient);
        await reader.Refresh();

        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);
        var tenant = await resolver.ResolveAsync("tenant1");
        Assert.That(tenant.Connections.Count, Is.EqualTo(3));
        Assert.That(tenant.Connections.FirstOrDefault(p => p.Key == "tenant1_app1").Value, Is.EqualTo("PLACEHOLDER1"));
    }
    
    [Test]
    public async Task WithTenantResolverAsyncReset_Test()
    {
        var fakeHttpWebClient = new FakeHttpClient(new HttpClient(), "", "");
        var logger = Mock.Of<ILogger<HttpConfigReader>>();
        var reader = new HttpConfigReader(config, logger, new IdFaker(), fakeHttpWebClient);
        await reader.Refresh();

        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);
        var tenant = await resolver.ResolveAsync("tenant1", reset: true);
        Assert.That(tenant.Connections.Count, Is.EqualTo(3));
        Assert.That(tenant.Connections.FirstOrDefault(p => p.Key == "tenant1_app1").Value, Is.EqualTo("PLACEHOLDER1"));
    }

    [Test]
    public async Task WithTenantResolver_Test()
    {
        var fakeHttpWebClient = new FakeHttpClient(new HttpClient(), "", "");
        var logger = Mock.Of<ILogger<HttpConfigReader>>();
        var reader = new HttpConfigReader(config, logger, new IdFaker(), fakeHttpWebClient);
        await reader.Refresh();

        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);
        var tenant = resolver.Resolve("tenant1");
        Assert.That(tenant.Connections.Count, Is.EqualTo(3));
        Assert.That(tenant.Connections.FirstOrDefault(p => p.Key == "tenant1_app1").Value, Is.EqualTo("PLACEHOLDER1"));
    }

    [Test]
    public async Task IsSetup_Test()
    {
        var fakeHttpWebClient = new FakeHttpClient(new HttpClient(), "", "");
        var logger = Mock.Of<ILogger<HttpConfigReader>>();
        var reader = new HttpConfigReader(config, logger, new IdFaker(), fakeHttpWebClient);
        Assert.That(reader.IsSetup(), Is.EqualTo(false));
        await reader.Refresh();
        Assert.That(reader.IsSetup(), Is.EqualTo(true));
    }

    [Test]
    public async Task GetConnection_Test()
    {
        var fakeHttpWebClient = new FakeHttpClient(new HttpClient(), "", "");
        var logger = Mock.Of<ILogger<HttpConfigReader>>();
        var reader = new HttpConfigReader(config, logger, new IdFaker(), fakeHttpWebClient);
        await reader.Refresh();

        var connString = reader.GetConnection("tenant3", "app1");
        Assert.That(connString, Is.EqualTo("PLACEHOLDER5"));
    }
}