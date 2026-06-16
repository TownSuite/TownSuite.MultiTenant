using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace TownSuite.MultiTenant.Tests;

public class AliasResolution_Tests
{
    private const string Alias = "a.dns.record.as.tenant.townsuite.com";
    private const string UniqueId = "tenant1";

    private Settings settings = null!;

    [SetUp]
    public void Setup()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("http_reader_test.json")
            .AddEnvironmentVariables()
            .Build();

        settings = config.GetSection("TenantSettings").Get<Settings>()!;
    }

    private async Task<HttpConfigReader> LoadedReaderAsync()
    {
        var reader = new HttpConfigReader(Mock.Of<ILogger<HttpConfigReader>>(), new IdFaker(),
            new FakeHttpClient(new HttpClient(), ""), settings);
        await reader.Refresh();
        return reader;
    }

    [Test]
    public async Task ResolveUniqueId_MapsAliasToUniqueId()
    {
        var reader = await LoadedReaderAsync();

        Assert.That(reader.ResolveUniqueId(Alias), Is.EqualTo(UniqueId));
    }

    [Test]
    public async Task ResolveUniqueId_ReturnsUniqueIdUnchanged()
    {
        var reader = await LoadedReaderAsync();

        Assert.That(reader.ResolveUniqueId(UniqueId), Is.EqualTo(UniqueId));
    }

    [Test]
    public async Task ResolveUniqueId_IsCaseInsensitive()
    {
        var reader = await LoadedReaderAsync();

        Assert.That(reader.ResolveUniqueId(Alias.ToUpperInvariant()), Is.EqualTo(UniqueId));
    }

    [Test]
    public async Task ResolveUniqueId_ReturnsNullForUnknown()
    {
        var reader = await LoadedReaderAsync();

        Assert.That(reader.ResolveUniqueId("nope.example.com"), Is.Null);
    }

    [Test]
    public async Task GetConnections_ByAlias_MatchesByUniqueId()
    {
        var reader = await LoadedReaderAsync();

        var byAlias = reader.GetConnections(Alias);
        var byId = reader.GetConnections(UniqueId);

        Assert.That(byAlias.Count, Is.EqualTo(byId.Count));
        Assert.That(byAlias.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetConnection_ByAlias_ReturnsConnectionString()
    {
        var reader = await LoadedReaderAsync();

        var byAlias = reader.GetConnection(Alias, "app1");
        var byId = reader.GetConnection(UniqueId, "app1");

        Assert.That(byAlias, Is.EqualTo(byId));
        Assert.That(byAlias, Is.Not.Empty);
    }

    [Test]
    public async Task ResolveAsync_ByAlias_ReturnsTenantWithCanonicalUniqueId()
    {
        var reader = await LoadedReaderAsync();
        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);

        var tenant = (await resolver.ResolveAsync(Alias))!;

        Assert.That(tenant.UniqueId, Is.EqualTo(UniqueId));
        Assert.That(tenant.Connections.Count, Is.EqualTo(3));
        Assert.That(tenant.Aliases, Does.Contain(Alias));
    }

    [Test]
    public async Task ResolveAsync_ByAlias_CachesUnderBothAliasAndUniqueId()
    {
        var reader = await LoadedReaderAsync();
        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);

        await resolver.ResolveAsync(Alias);

        Assert.That(resolver.Tenants.ContainsKey(Alias), Is.True);
        Assert.That(resolver.Tenants.ContainsKey(UniqueId), Is.True);
        Assert.That(resolver.Tenants[Alias].UniqueId, Is.EqualTo(UniqueId));
        Assert.That(resolver.Tenants[UniqueId].UniqueId, Is.EqualTo(UniqueId));
    }

    [Test]
    public async Task Resolve_Sync_ByAlias_Works()
    {
        var reader = await LoadedReaderAsync();
        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);

        var tenant = resolver.Resolve(Alias)!;

        Assert.That(tenant.UniqueId, Is.EqualTo(UniqueId));
        Assert.That(tenant.Connections.Count, Is.EqualTo(3));
    }
}
