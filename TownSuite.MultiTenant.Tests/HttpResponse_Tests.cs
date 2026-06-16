using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace TownSuite.MultiTenant.Tests;

public class HttpResponse_Tests
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

    // Returns a wrong id for every connection — proves the HTTP reader uses the
    // response's TenantId and does NOT call the retriever when TenantId is present.
    private sealed class WrongIdRetriever : IUniqueIdRetriever
    {
        public Task<string?> GetUniqueId(ConnectionStrings con, AppSettingsConfigPairs configPairs,
            CancellationToken cancellationToken = default) => Task.FromResult<string?>("WRONG");
    }

    private async Task<HttpConfigReader> LoadedReaderAsync(IUniqueIdRetriever? retriever = null)
    {
        var reader = new HttpConfigReader(Mock.Of<ILogger<HttpConfigReader>>(), retriever ?? new IdFaker(),
            new FakeHttpClient(new HttpClient(), ""), settings);
        await reader.Refresh();
        return reader;
    }

    [Test]
    public async Task TenantId_IsAuthoritative_RetrieverNotUsed()
    {
        var reader = await LoadedReaderAsync(new WrongIdRetriever());

        // Grouped under the response's TenantId, not the retriever's "WRONG".
        Assert.That(reader.GetTenantIds(), Does.Contain("tenant1"));
        Assert.That(reader.GetTenantIds(), Does.Not.Contain("WRONG"));
        Assert.That(reader.GetConnections("tenant1").Count, Is.EqualTo(3));
    }

    [Test]
    public async Task AppSettings_AreCaptured_FromResponse()
    {
        var reader = await LoadedReaderAsync();

        var appSettings = reader.GetAppSettings("tenant1");
        Assert.That(appSettings["FeatureX"], Is.EqualTo("enabled"));
        Assert.That(appSettings["Region"], Is.EqualTo("us-east"));
    }

    [Test]
    public async Task AppSettings_AreResolvableByAlias()
    {
        var reader = await LoadedReaderAsync();

        var byAlias = reader.GetAppSettings("a.dns.record.as.tenant.townsuite.com");
        Assert.That(byAlias["FeatureX"], Is.EqualTo("enabled"));
    }

    [Test]
    public async Task AppSettings_EmptyForTenantWithout()
    {
        var reader = await LoadedReaderAsync();

        Assert.That(reader.GetAppSettings("tenant3"), Is.Empty);
    }

    [Test]
    public async Task ResolvedTenant_ExposesAppSettings()
    {
        var reader = await LoadedReaderAsync();
        var resolver = new TenantResolver(Mock.Of<ILogger<TenantResolver>>(), reader);

        var tenant = (await resolver.ResolveAsync("tenant1"))!;

        Assert.That(tenant.AppSettings["FeatureX"], Is.EqualTo("enabled"));
        Assert.That(tenant.AppSettings["Region"], Is.EqualTo("us-east"));
    }
}
