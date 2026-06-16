using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace TownSuite.MultiTenant.Tests;

public class ResolverContract_Tests
{
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

    private async Task<TenantResolver> LoadedResolverAsync()
    {
        var reader = new HttpConfigReader(Mock.Of<ILogger<HttpConfigReader>>(), new IdFaker(),
            new FakeHttpClient(new HttpClient(), ""), settings);
        await reader.Refresh();
        return new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task ResolveAsync_NullOrWhitespace_ReturnsNull(string? tenantId)
    {
        var resolver = await LoadedResolverAsync();
        Assert.That(await resolver.ResolveAsync(tenantId!), Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Resolve_NullOrWhitespace_ReturnsNull(string? tenantId)
    {
        var resolver = await LoadedResolverAsync();
        Assert.That(resolver.Resolve(tenantId!), Is.Null);
    }

    [Test]
    public async Task GetConnections_UnknownTenant_ReturnsEmpty()
    {
        var reader = new HttpConfigReader(Mock.Of<ILogger<HttpConfigReader>>(), new IdFaker(),
            new FakeHttpClient(new HttpClient(), ""), settings);
        await reader.Refresh();

        Assert.That(reader.GetConnections("does-not-exist"), Is.Empty);
    }

    [Test]
    public async Task GetConnection_UnknownTenant_ReturnsEmptyString()
    {
        var reader = new HttpConfigReader(Mock.Of<ILogger<HttpConfigReader>>(), new IdFaker(),
            new FakeHttpClient(new HttpClient(), ""), settings);
        await reader.Refresh();

        Assert.That(reader.GetConnection("does-not-exist", "app1"), Is.EqualTo(""));
    }

    [Test]
    public async Task GetConnection_UnknownAppType_ReturnsEmptyString()
    {
        var reader = new HttpConfigReader(Mock.Of<ILogger<HttpConfigReader>>(), new IdFaker(),
            new FakeHttpClient(new HttpClient(), ""), settings);
        await reader.Refresh();

        Assert.That(reader.GetConnection("tenant1", "no-such-app"), Is.EqualTo(""));
    }
}
