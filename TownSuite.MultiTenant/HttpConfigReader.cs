using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TownSuite.MultiTenant;

public class HttpConfigReader : ConfigReader
{
    private readonly TsWebClient _webClient;

    public HttpConfigReader(ILogger<HttpConfigReader> logger, IUniqueIdRetriever uniqueIdRetriever,
        TsWebClient webClient,
        Settings settings) : base(uniqueIdRetriever, settings, logger)
    {
        _webClient = webClient;
    }

    /// <summary>
    /// Loads tenant data from the configured HTTP endpoints.
    ///
    /// When a response carries a <see cref="WebSearchResponse.TenantId"/> it is the
    /// authoritative canonical id: the response's connections are grouped under it
    /// directly and its <see cref="WebSearchResponse.AppSettings"/> are captured —
    /// no per-connection unique-id lookup is needed. Responses without a TenantId
    /// fall back to resolving the id via the injected <see cref="IUniqueIdRetriever"/>.
    /// </summary>
    protected override async Task LoadConnectionsAsync(
        ConcurrentDictionary<string, IList<ConnectionStrings>> target,
        ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> appSettings,
        CancellationToken cancellationToken)
    {
        foreach (var configPair in _settings.ConfigPairs)
        {
            foreach (var configReaderUrl in configPair.ConfigReaderUrls)
            {
                var tenants = await _webClient
                    .GetAsync(configReaderUrl, configPair.ConfigReaderUrlBearerToken, cancellationToken)
                    .ConfigureAwait(false);

                string pattern = configPair.ResolvedUniqueIdPattern;

                // Connections from responses without a TenantId are resolved via
                // the retriever (legacy path) and grouped by alias afterwards.
                var lookupConns = new List<ConnectionStrings>();
                var lookupTasks = new List<Task>();

                foreach (var tenant in tenants)
                {
                    var conns = tenant.Connections
                        .Select(c => new ConnectionStrings(configPair.DecryptionKey)
                            { Name = c.Key, ConnStr = c.Value })
                        .ToList();

                    if (!string.IsNullOrWhiteSpace(tenant.TenantId))
                    {
                        foreach (var con in conns)
                        {
                            AddConnection(target, con, tenant.TenantId);
                        }

                        MergeAppSettings(appSettings, tenant.TenantId, tenant.AppSettings);
                    }
                    else
                    {
                        foreach (var con in conns)
                        {
                            lookupConns.Add(con);
                            lookupTasks.Add(InitializeUniqueIds(target, con, pattern, configPair, cancellationToken));
                        }
                    }
                }

                await Task.WhenAll(lookupTasks).ConfigureAwait(false);

                LogAndDrainExceptions();

                GroupDatabasesByTenant(target, lookupConns);
            }
        }
    }

    private static void MergeAppSettings(
        ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> appSettings,
        string tenantId, ICollection<KeyValuePairOfStringAndString>? incoming)
    {
        if (incoming == null || incoming.Count == 0)
        {
            return;
        }

        appSettings.AddOrUpdate(tenantId,
            _ => Merge(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), incoming),
            (_, existing) => Merge(
                new Dictionary<string, string>((IDictionary<string, string>)existing,
                    StringComparer.OrdinalIgnoreCase),
                incoming));

        static IReadOnlyDictionary<string, string> Merge(Dictionary<string, string> dict,
            ICollection<KeyValuePairOfStringAndString> items)
        {
            foreach (var item in items)
            {
                dict[item.Key] = item.Value;
            }

            return dict;
        }
    }
}
