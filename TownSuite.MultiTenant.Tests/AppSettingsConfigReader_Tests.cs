using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace TownSuite.MultiTenant.Tests;

public class AppSettingsConfigReader_Tests
{
    private IConfiguration config;
    private Settings settings;

    [SetUp]
    public void Setup()
    {
        config = new ConfigurationBuilder()
            .AddJsonFile("appsettings_reader_test.json")
            .AddEnvironmentVariables()
            .Build();

        string pattern = config.GetSection("TenantSettings").GetSection("UniqueIdDbPattern").Value;
        string sql = config.GetSection("TenantSettings").GetSection("SqlUniqueIdLookup").Value;
        string decryptionKey = config.GetSection("TenantSettings").GetSection("DecryptionKey").Value;
        settings = new Settings()
        {
            UniqueIdDbPattern = pattern,
            DecryptionKey = decryptionKey
        };
    }

    [Test]
    public async Task Appsettings_Test()
    {
        var logger = Mock.Of<ILogger<AppSettingsConfigReader>>();
        var reader = new AppSettingsConfigReader(config, logger, new IdFaker(), settings);
        await reader.Refresh();
        var tenantOneConnections = reader.GetConnections("tenant1");

        Assert.That(tenantOneConnections.Count, Is.EqualTo(3));
        Assert.That(tenantOneConnections.FirstOrDefault(p => p.Name == "tenant1_app1").ConnStr,
            Is.EqualTo("PLACEHOLDER1"));
        Assert.That(tenantOneConnections.FirstOrDefault(p => p.Name == "tenant1_app2").ConnStr,
            Is.EqualTo("PLACEHOLDER2"));
        Assert.That(
            tenantOneConnections.FirstOrDefault(p => p.Name == "a.dns.record.as.tenant.townsuite.com_app1").ConnStr,
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
    }

    [Test]
    public async Task WithTenantResolverAsync_Test()
    {
        var loggerAppSettings = Mock.Of<ILogger<AppSettingsConfigReader>>();
        var reader = new AppSettingsConfigReader(config, loggerAppSettings, new IdFaker(), settings);
        await reader.Refresh();

        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);
        var tenant = await resolver.ResolveAsync("tenant1");
        Assert.That(tenant.Connections.Count, Is.EqualTo(3));
        Assert.That(tenant.Connections.FirstOrDefault(p => p.Key == "tenant1_app1").Value, Is.EqualTo("PLACEHOLDER1"));
    }

    [Test]
    public async Task WithTenantResolverAsyncRest_Test()
    {
        var loggerAppSettings = Mock.Of<ILogger<AppSettingsConfigReader>>();
        var reader = new AppSettingsConfigReader(config, loggerAppSettings, new IdFaker(), settings);
        await reader.Refresh();

        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);
        var tenant = await resolver.ResolveAsync("tenant1", reset: true);
        Assert.That(tenant.Connections.Count, Is.EqualTo(3));
        Assert.That(tenant.Connections.FirstOrDefault(p => p.Key == "tenant1_app1").Value, Is.EqualTo("PLACEHOLDER1"));
    }

    [Test]
    public async Task WithTenantResolver_Test()
    {
        var loggerAppSettings = Mock.Of<ILogger<AppSettingsConfigReader>>();
        var reader = new AppSettingsConfigReader(config, loggerAppSettings, new IdFaker(), settings);
        await reader.Refresh();

        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);
        var tenant = resolver.Resolve("tenant1");
        Assert.That(tenant.Connections.Count, Is.EqualTo(3));
        Assert.That(tenant.Connections.FirstOrDefault(p => p.Key == "tenant1_app1").Value, Is.EqualTo("PLACEHOLDER1"));
    }

    [Test]
    public async Task IsSetup_Test()
    {
        var loggerAppSettings = Mock.Of<ILogger<AppSettingsConfigReader>>();
        var reader = new AppSettingsConfigReader(config, loggerAppSettings, new IdFaker(), settings);

        reader.Clear();
        Assert.That(reader.IsSetup(), Is.EqualTo(false));
        await reader.Refresh();
        Assert.That(reader.IsSetup(), Is.EqualTo(true));
    }

    [Test]
    public async Task GetConnection_Test()
    {
        var loggerAppSettings = Mock.Of<ILogger<AppSettingsConfigReader>>();
        var reader = new AppSettingsConfigReader(config, loggerAppSettings, new IdFaker(), settings);
        await reader.Refresh();

        var connString = reader.GetConnection("tenant3", "app1");
        Assert.That(connString, Is.EqualTo("PLACEHOLDER5"));
    }
}