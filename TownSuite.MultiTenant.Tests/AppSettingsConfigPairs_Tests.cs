using Microsoft.Extensions.Configuration;

namespace TownSuite.MultiTenant.Tests;

public class AppSettingsConfigPairs_Tests
{
    [Test]
    public void ResolvedUniqueIdLookup_PrefersNewKey()
    {
        var cfg = new AppSettingsConfigPairs { UniqueIdLookup = "new-query", SqlUniqueIdLookup = "legacy-query" };
        Assert.That(cfg.ResolvedUniqueIdLookup, Is.EqualTo("new-query"));
    }

    [Test]
    public void ResolvedUniqueIdLookup_FallsBackToLegacyKey()
    {
        var cfg = new AppSettingsConfigPairs { SqlUniqueIdLookup = "legacy-query" };
        Assert.That(cfg.ResolvedUniqueIdLookup, Is.EqualTo("legacy-query"));
    }

    [Test]
    public void ResolvedUniqueIdLookup_EmptyWhenNeitherSet()
    {
        Assert.That(new AppSettingsConfigPairs().ResolvedUniqueIdLookup, Is.EqualTo(""));
    }

    [Test]
    public void ResolvedUniqueIdPattern_PrefersNewKey()
    {
        var cfg = new AppSettingsConfigPairs { UniqueIdPattern = ".*_new", UniqueIdDbPattern = ".*_legacy" };
        Assert.That(cfg.ResolvedUniqueIdPattern, Is.EqualTo(".*_new"));
    }

    [Test]
    public void ResolvedUniqueIdPattern_FallsBackToLegacyKey()
    {
        var cfg = new AppSettingsConfigPairs { UniqueIdDbPattern = ".*_legacy" };
        Assert.That(cfg.ResolvedUniqueIdPattern, Is.EqualTo(".*_legacy"));
    }

    [Test]
    public void LegacyKey_StillBindsFromConfiguration()
    {
        // http_reader_test.json intentionally uses the legacy "SqlUniqueIdLookup" key.
        var settings = new ConfigurationBuilder()
            .AddJsonFile("http_reader_test.json")
            .Build()
            .GetSection("TenantSettings")
            .Get<Settings>()!;

        var pair = settings.ConfigPairs[0];
        Assert.That(pair.SqlUniqueIdLookup, Is.Not.Empty);
        Assert.That(pair.ResolvedUniqueIdLookup, Is.EqualTo(pair.SqlUniqueIdLookup));
    }
}
