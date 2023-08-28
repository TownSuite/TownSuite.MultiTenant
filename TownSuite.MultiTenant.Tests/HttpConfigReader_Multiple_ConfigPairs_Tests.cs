using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace TownSuite.MultiTenant.Tests;

public class HttpConfigReader_Multiple_ConfigPairs_Tests
{
    private IConfiguration config;
    private Settings settings;

    [SetUp]
    public void Setup()
    {
        config = new ConfigurationBuilder()
            .AddJsonFile("http_reader_multiple_configpairs_test.json")
            .AddEnvironmentVariables()
            .Build();

        settings = config.GetSection("TenantSettings").Get<Settings>();
    }

    [Test]
    public async Task Appsettings_Test()
    {
        var fakeHttpWebClient = new FakeHttpClient(new HttpClient(), "");
        var logger = Mock.Of<ILogger<HttpConfigReader>>();
        var reader = new HttpConfigReader(logger, new IdFaker(), fakeHttpWebClient, settings);
        await reader.Refresh();
        var tenantOneConnections = reader.GetConnections("tenant1");
        
        Assert.That(tenantOneConnections.Count, Is.EqualTo(3));
        Assert.That(tenantOneConnections.FirstOrDefault(p => p.Name == "tenant1_app1").ConnStr,
            Is.EqualTo("PLACEHOLDER1"));
        Assert.That(tenantOneConnections.FirstOrDefault(p => p.Name == "tenant1_app2").ConnStr,
            Is.EqualTo("PLACEHOLDER2"));
        Assert.That(
            tenantOneConnections.FirstOrDefault(p => p.Name == "a.dns.record.as.tenant.townsuite.com_app1")
                .ConnStr,
            Is.EqualTo("tenant 1 alias"));

        var tenantTwoConnections = reader.GetConnections("tenant2");
        Assert.That(tenantTwoConnections.Count, Is.EqualTo(3));
        Assert.That(tenantTwoConnections.FirstOrDefault(p => p.Name == "tenant2_app1").ConnStr,
            Is.EqualTo("PLACEHOLDER3"));
        Assert.That(tenantTwoConnections.FirstOrDefault(p => p.Name == "tenant2_app2").ConnStr,
            Is.EqualTo("PLACEHOLDER4"));
        Assert.That(
            tenantTwoConnections.FirstOrDefault(p => p.Name == "second.dns.record.as.tenant.townsuite.com_app1")
                .ConnStr,
            Is.EqualTo("tenant 2 alias"));
        
        
        var tenantFifthConnections = reader.GetConnections("tenant5");
        Assert.That(tenantFifthConnections.Count, Is.EqualTo(3));
        Assert.That(tenantFifthConnections.FirstOrDefault(p => p.Name == "tenant5_app1").ConnStr,
            Is.EqualTo("PLACEHOLDER8"));
        Assert.That(tenantFifthConnections.FirstOrDefault(p => p.Name == "tenant5_app2").ConnStr,
            Is.EqualTo("PLACEHOLDER9"));
        Assert.That(
            tenantFifthConnections.FirstOrDefault(p => p.Name == "fifth.dns.record.as.tenant.townsuite.com_app1")
                .ConnStr,
            Is.EqualTo("tenant 5 alias"));
    }
}