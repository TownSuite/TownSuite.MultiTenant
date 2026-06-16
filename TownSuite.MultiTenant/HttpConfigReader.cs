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
    /// </summary>
    protected override async Task LoadConnectionsAsync(
        ConcurrentDictionary<string, IList<ConnectionStrings>> target, CancellationToken cancellationToken)
    {
        foreach (var configPair in _settings.ConfigPairs)
        {
            foreach (var configReaderUrl in configPair.ConfigReaderUrls)
            {
                var tenants = await _webClient
                    .GetAsync(configReaderUrl, configPair.ConfigReaderUrlBearerToken, cancellationToken)
                    .ConfigureAwait(false);
                var conns = new List<ConnectionStrings>();

                string pattern = configPair.UniqueIdDbPattern;

                var tasks = new List<Task>();
                foreach (var tenant in tenants)
                {
                    foreach (var connection in tenant.Connections)
                    {
                        var con = new ConnectionStrings(configPair.DecryptionKey)
                            { Name = $"{connection.Key}", ConnStr = connection.Value };
                        conns.Add(con);
                        tasks.Add(InitializeUniqueIds(target, con, pattern, configPair, cancellationToken));
                    }
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);

                LogAndDrainExceptions();

                GroupDatabasesByTenant(target, conns);
            }
        }
    }
}
