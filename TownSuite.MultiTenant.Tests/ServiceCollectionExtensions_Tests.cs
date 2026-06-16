using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TownSuite.MultiTenant.Tests;

public class ServiceCollectionExtensions_Tests
{
    private static IConfiguration BuildConfig(string file)
    {
        return new ConfigurationBuilder()
            .AddJsonFile(file)
            .AddEnvironmentVariables()
            .Build();
    }

    [Test]
    public void AddTownSuiteMultiTenant_Http_ResolvesGraph()
    {
        var services = new ServiceCollection();
        services.AddTownSuiteMultiTenant(BuildConfig("http_reader_test.json"));

        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetService<Settings>(), Is.Not.Null);
        Assert.That(provider.GetService<IUniqueIdRetriever>(), Is.InstanceOf<SqlUniqueIdRetriever>());
        Assert.That(provider.GetService<TsWebClient>(), Is.Not.Null);
        Assert.That(provider.GetService<IConfigReader>(), Is.InstanceOf<HttpConfigReader>());
        Assert.That(provider.GetService<TenantResolver>(), Is.Not.Null);
    }

    [Test]
    public void AddTownSuiteMultiTenant_AppSettings_ResolvesReader()
    {
        var services = new ServiceCollection();
        services.AddTownSuiteMultiTenant(BuildConfig("appsettings_reader_test.json"),
            TenantConfigSource.AppSettings);

        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetService<IConfigReader>(), Is.InstanceOf<AppSettingsConfigReader>());
        Assert.That(provider.GetService<TenantResolver>(), Is.Not.Null);
    }

    [Test]
    public void AddTownSuiteMultiTenant_Throws_When_Section_Missing()
    {
        var services = new ServiceCollection();
        Assert.Throws<TownSuiteException>(() =>
            services.AddTownSuiteMultiTenant(BuildConfig("http_reader_test.json"),
                settingsSectionName: "DoesNotExist"));
    }
}
