using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TownSuite.MultiTenant;

/// <summary>
/// Where tenant connection data is read from.
/// </summary>
public enum TenantConfigSource
{
    /// <summary>Read tenant data from the configured HTTP endpoints (<see cref="HttpConfigReader"/>).</summary>
    Http,

    /// <summary>Read tenant data from the ConnectionStrings section (<see cref="AppSettingsConfigReader"/>).</summary>
    AppSettings
}

public static class ServiceCollectionExtensions
{
    private const string TsWebClientName = "TownSuite.MultiTenant.TsWebClient";

    /// <summary>
    /// Registers everything needed to resolve multi-tenant connection strings,
    /// using the supplied <paramref name="uniqueIdLookup"/> delegate to resolve a
    /// tenant's canonical unique id (the library is database-agnostic, so you
    /// provide this — e.g. open the connection and run a query).
    /// </summary>
    public static IServiceCollection AddTownSuiteMultiTenant(
        this IServiceCollection services,
        IConfiguration configuration,
        UniqueIdLookup uniqueIdLookup,
        TenantConfigSource source = TenantConfigSource.Http,
        string settingsSectionName = "TenantSettings") =>
        services.AddTownSuiteMultiTenant(configuration, new DelegateUniqueIdRetriever(uniqueIdLookup), source,
            settingsSectionName);

    /// <summary>
    /// Registers everything needed to resolve multi-tenant connection strings:
    /// <see cref="Settings"/>, the supplied <see cref="IUniqueIdRetriever"/>, an
    /// <see cref="IConfigReader"/> for the chosen <paramref name="source"/>, and
    /// the <see cref="TenantResolver"/>. All are registered as singletons so the
    /// tenant cache is shared process-wide.
    /// </summary>
    public static IServiceCollection AddTownSuiteMultiTenant(
        this IServiceCollection services,
        IConfiguration configuration,
        IUniqueIdRetriever uniqueIdRetriever,
        TenantConfigSource source = TenantConfigSource.Http,
        string settingsSectionName = "TenantSettings")
    {
        if (uniqueIdRetriever is null)
        {
            throw new ArgumentNullException(nameof(uniqueIdRetriever));
        }

        var settings = configuration.GetSection(settingsSectionName).Get<Settings>()
                       ?? throw new TownSuiteException(
                           $"Configuration section '{settingsSectionName}' is missing or could not be bound to Settings.");

        // The readers depend on ILogger<T>; AppSettingsConfigReader also depends on
        // IConfiguration. Register both defensively (no-ops if the host already did)
        // so the graph resolves regardless of host setup.
        services.AddLogging();
        services.TryAddSingleton(configuration);

        services.AddSingleton(settings);
        services.AddSingleton(uniqueIdRetriever);

        switch (source)
        {
            case TenantConfigSource.Http:
                // Pass the factory (not a captured HttpClient) so TsWebClient
                // resolves a fresh client per request and the handler pool is
                // managed (DNS refresh, no socket exhaustion).
                services.AddHttpClient(TsWebClientName);
                services.AddSingleton<TsWebClient>(sp =>
                    new TsWebClient(sp.GetRequiredService<IHttpClientFactory>(), TsWebClientName, settings.UserAgent));
                services.AddSingleton<IConfigReader, HttpConfigReader>();
                break;

            case TenantConfigSource.AppSettings:
                services.AddSingleton<IConfigReader, AppSettingsConfigReader>();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported tenant config source.");
        }

        services.AddSingleton<TenantResolver>();
        return services;
    }
}
