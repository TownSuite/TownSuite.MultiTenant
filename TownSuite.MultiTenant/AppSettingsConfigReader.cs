using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TownSuite.MultiTenant;

public class AppSettingsConfigReader : ConfigReader
{
    private readonly IConfiguration _configuration;

    public AppSettingsConfigReader(IConfiguration configuration,
        ILogger<AppSettingsConfigReader> logger, IUniqueIdRetriever uniqueIdRetriever,
        Settings settings) : base(uniqueIdRetriever, settings, logger)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Loads tenant data from the ConnectionStrings section of configuration.
    /// </summary>
    protected override async Task LoadConnectionsAsync(
        ConcurrentDictionary<string, IList<ConnectionStrings>> target, CancellationToken cancellationToken)
    {
        var connections = _configuration.GetSection("ConnectionStrings").GetChildren();
        var conns = new List<ConnectionStrings>();

        var firstSettingsRecord = _settings.ConfigPairs.FirstOrDefault();

        string pattern = firstSettingsRecord.UniqueIdDbPattern;

        var tasks = new List<Task>();
        foreach (var connection in connections)
        {
            var con = new ConnectionStrings(firstSettingsRecord.DecryptionKey)
                { Name = connection.Key, ConnStr = connection.Value };
            conns.Add(con);
            tasks.Add(InitializeUniqueIds(target, con, pattern, firstSettingsRecord, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        LogAndDrainExceptions();

        GroupDatabasesByTenant(target, conns);
    }
}
