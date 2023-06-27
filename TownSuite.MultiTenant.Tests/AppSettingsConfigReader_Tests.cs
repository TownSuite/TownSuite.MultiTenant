using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace TownSuite.MultiTenant.Tests;

public class AppSettingsConfigReader_Tests
{
    private IConfiguration config;

    [SetUp]
    public void Setup()
    {
        config = new ConfigurationBuilder()
            .AddJsonFile("appsettings_appsettings_test.json")
            .AddEnvironmentVariables()
            .Build();
    }

    [Test]
    public async Task Appsettings_Test()
    {
        var logger = Mock.Of<ILogger<AppSettingsConfigReader>>();
        var reader = new AppSettingsConfigReader(config, logger, new IdFaker());
        await reader.Refresh();
        var tenantOneConnections = reader.GetConnections("tenant1");

        Assert.That(tenantOneConnections.Count, Is.EqualTo(2));
        Assert.That(tenantOneConnections.FirstOrDefault(p => p.Name == "tenant1_app1").ConnStr,
            Is.EqualTo("PLACEHOLDER1"));
        Assert.That(tenantOneConnections.LastOrDefault(p => p.Name == "tenant1_app2").ConnStr,
            Is.EqualTo("PLACEHOLDER2"));

        var tenantTwoConnections = reader.GetConnections("tenant2");
        Assert.That(tenantTwoConnections.Count, Is.EqualTo(2));
        Assert.That(tenantTwoConnections.FirstOrDefault(p => p.Name == "tenant2_app1").ConnStr,
            Is.EqualTo("PLACEHOLDER3"));
        Assert.That(tenantTwoConnections.FirstOrDefault(p => p.Name == "tenant2_app2").ConnStr,
            Is.EqualTo("PLACEHOLDER4"));
    }

    [Test]
    public async Task WithTenantResolver_Test()
    {
        var loggerAppSettings = Mock.Of<ILogger<AppSettingsConfigReader>>();
        var reader = new AppSettingsConfigReader(config, loggerAppSettings, new IdFaker());
        await reader.Refresh();

        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);
        var tenant = await resolver.Resolve("tenant1");
        Assert.That(tenant.Connections.Count, Is.EqualTo(2));
        Assert.That(tenant.Connections.FirstOrDefault(p => p.Key == "tenant1_app1").Value, Is.EqualTo("PLACEHOLDER1"));
    }
}