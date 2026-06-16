using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace TownSuite.MultiTenant.Tests;

public class ConfigReaderResilience_Tests
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

    private sealed class ThrowingWebClient : TsWebClient
    {
        public ThrowingWebClient() : base(new HttpClient(), "test-agent")
        {
        }

        public override Task<ICollection<WebSearchResponse>> GetAsync(string url, string bearerToken,
            CancellationToken cancellationToken) =>
            throw new ApiException("config endpoint unavailable", 503, null);
    }

    // Resolves everything except tenant2's connections, which throw.
    private sealed class FailsTenant2 : IUniqueIdRetriever
    {
        private readonly IdFaker _inner = new();

        public Task<string?> GetUniqueId(ConnectionStrings con, AppSettingsConfigPairs configPairs,
            CancellationToken cancellationToken = default)
        {
            if (con.TenantOrAlias.StartsWith("tenant2") || con.TenantOrAlias.StartsWith("second.dns"))
            {
                throw new InvalidOperationException("simulated db failure for tenant2");
            }

            return _inner.GetUniqueId(con, configPairs, cancellationToken);
        }
    }

    [Test]
    public void Refresh_WhenEndpointFails_PropagatesAndCacheStaysEmpty()
    {
        var reader = new HttpConfigReader(Mock.Of<ILogger<HttpConfigReader>>(), new IdFaker(),
            new ThrowingWebClient(), settings);

        Assert.ThrowsAsync<ApiException>(async () => await reader.Refresh());
        Assert.That(reader.IsSetup(), Is.False);
    }

    [Test]
    public async Task Refresh_WhenSomeTenantsFail_LoadsTheRestAndCountsErrors()
    {
        var reader = new HttpConfigReader(Mock.Of<ILogger<HttpConfigReader>>(), new FailsTenant2(),
            new FakeHttpClient(new HttpClient(), ""), settings);

        await reader.Refresh();

        var ids = reader.GetTenantIds();
        Assert.That(ids, Does.Contain("tenant1"));
        Assert.That(ids, Does.Contain("tenant3"));
        Assert.That(ids, Does.Not.Contain("tenant2"));
        Assert.That(reader.LastLoadErrorCount, Is.GreaterThan(0));
        Assert.That(reader.IsSetup(), Is.True);
    }

    [Test]
    public async Task ConcurrentRefreshAndReads_StayConsistent_AndDoNotThrow()
    {
        var reader = new HttpConfigReader(Mock.Of<ILogger<HttpConfigReader>>(), new IdFaker(),
            new FakeHttpClient(new HttpClient(), ""), settings);

        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() => reader.Refresh()));
            tasks.Add(Task.Run(async () =>
            {
                await reader.EnsureLoadedAsync();
                // Reads during concurrent refresh must never see a half-built cache.
                var conns = reader.GetConnections("tenant1");
                Assert.That(conns.Count, Is.AnyOf(0, 3));
                _ = reader.GetTenantIds();
            }));
        }

        await Task.WhenAll(tasks);

        Assert.That(reader.GetConnections("tenant1").Count, Is.EqualTo(3));
        Assert.That(reader.GetTenantIds(), Does.Contain("tenant1"));
    }

    [Test]
    public async Task ConcurrentResolve_SameTenant_ReturnsConsistentResult()
    {
        var reader = new HttpConfigReader(Mock.Of<ILogger<HttpConfigReader>>(), new IdFaker(),
            new FakeHttpClient(new HttpClient(), ""), settings);
        await reader.Refresh();
        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => resolver.ResolveAsync("tenant1"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.That(results, Is.All.Not.Null);
        Assert.That(results, Is.All.Matches<Tenant?>(t => t!.Connections.Count == 3));
    }
}
