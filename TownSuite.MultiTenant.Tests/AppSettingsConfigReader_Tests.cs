using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace TownSuite.MultiTenant.Tests;

public class AppSettingsConfigReader_Tests
{
    private IConfiguration config = null!;
    private Settings settings = null!;

    [SetUp]
    public void Setup()
    {
        config = new ConfigurationBuilder()
            .AddJsonFile("appsettings_reader_test.json")
            .AddEnvironmentVariables()
            .Build();

        settings = config.GetSection("TenantSettings").Get<Settings>()!;
    }

    [Test]
    public async Task Appsettings_Test()
    {
        var logger = Mock.Of<ILogger<AppSettingsConfigReader>>();
        var reader = new AppSettingsConfigReader(config, logger, new IdFaker(), settings);
        await reader.Refresh();
        var tenantOneConnections = reader.GetConnections("tenant1");

        Assert.That(tenantOneConnections.Count, Is.EqualTo(3));
        Assert.That(tenantOneConnections.FirstOrDefault(p => p.Name == "tenant1_app1")!.ConnStr,
            Is.EqualTo("PLACEHOLDER1"));
        Assert.That(tenantOneConnections.FirstOrDefault(p => p.Name == "tenant1_app2")!.ConnStr,
            Is.EqualTo("PLACEHOLDER2"));
        Assert.That(
            tenantOneConnections.FirstOrDefault(p => p.Name == "a.dns.record.as.tenant.townsuite.com_app1")!.ConnStr,
            Is.EqualTo("tenant 1 alias"));

        var tenantTwoConnections = reader.GetConnections("tenant2");
        Assert.That(tenantTwoConnections.Count, Is.EqualTo(3));
        Assert.That(tenantTwoConnections.FirstOrDefault(p => p.Name == "tenant2_app1")!.ConnStr,
            Is.EqualTo("PLACEHOLDER3"));
        Assert.That(tenantTwoConnections.FirstOrDefault(p => p.Name == "tenant2_app2")!.ConnStr,
            Is.EqualTo("PLACEHOLDER4"));
        Assert.That(
            tenantTwoConnections.FirstOrDefault(p => p.Name == "second.dns.record.as.tenant.townsuite.com_app1")!
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
        var tenant = (await resolver.ResolveAsync("tenant1"))!;
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
        var tenant = (await resolver.ResolveAsync("tenant1", reset: true))!;
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
        var tenant = resolver.Resolve("tenant1")!;
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

    [Test]
    public async Task GetConnections_ReturnsCopy_CannotMutateCache()
    {
        var logger = Mock.Of<ILogger<AppSettingsConfigReader>>();
        var reader = new AppSettingsConfigReader(config, logger, new IdFaker(), settings);
        await reader.Refresh();

        var first = reader.GetConnections("tenant1");
        var originalCount = first.Count;
        first.Clear();

        var second = reader.GetConnections("tenant1");
        Assert.That(second.Count, Is.EqualTo(originalCount));
    }

    [Test]
    public async Task LastLoadErrorCount_IsZero_OnCleanLoad()
    {
        var logger = Mock.Of<ILogger<AppSettingsConfigReader>>();
        var reader = new AppSettingsConfigReader(config, logger, new IdFaker(), settings);
        await reader.Refresh();

        // Pattern non-matches (e.g. tenant1_app2) are expected flow, not errors.
        Assert.That(reader.LastLoadErrorCount, Is.EqualTo(0));
    }

    [Test]
    public async Task LastLoadErrorCount_CountsEmptyUniqueId()
    {
        var logger = Mock.Of<ILogger<AppSettingsConfigReader>>();
        // EmptyIdFaker resolves every matching connection to an empty unique id.
        var reader = new AppSettingsConfigReader(config, logger, new EmptyIdFaker(), settings);
        await reader.Refresh();

        Assert.That(reader.LastLoadErrorCount, Is.GreaterThan(0));
        Assert.That(reader.IsSetup(), Is.False);
    }

    private sealed class EmptyIdFaker : IUniqueIdRetriever
    {
        public Task<string?> GetUniqueId(ConnectionStrings con, AppSettingsConfigPairs configPairs,
            CancellationToken cancellationToken = default) => Task.FromResult<string?>("");
    }
}
