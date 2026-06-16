using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TownSuite.MultiTenant;

public class AppSettingsConfigReader : ConfigReader
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppSettingsConfigReader> _logger;

    public AppSettingsConfigReader(IConfiguration configuration,
        ILogger<AppSettingsConfigReader> logger, IUniqueIdRetriever uniqueIdRetriever,
        Settings settings) : base(uniqueIdRetriever, settings)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Loads tenant data from the ConnectionStrings section of configuration.
    /// </summary>
    protected override async Task LoadConnectionsAsync(CancellationToken cancellationToken)
    {
        var connections = _configuration.GetSection("ConnectionStrings").GetChildren();
        _connections = new ConcurrentDictionary<string, IList<ConnectionStrings>>();
        var conns = new List<ConnectionStrings>();

        var firstSettingsRecord = _settings.ConfigPairs.FirstOrDefault();

        string pattern = firstSettingsRecord.UniqueIdDbPattern;

        var tasks = new List<Task>();
        foreach (var connection in connections)
        {
            var con = new ConnectionStrings(firstSettingsRecord.DecryptionKey)
                { Name = connection.Key, ConnStr = connection.Value };
            conns.Add(con);
            tasks.Add(InitializeUniqueIds(con, pattern, firstSettingsRecord, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        LogAndDrainExceptions();

        GroupDatabasesByTenant(conns);
    }

    private void LogAndDrainExceptions()
    {
        while (Exceptions.TryTake(out var ex))
        {
            _logger.LogError(ex, "Tenant configuration load error: {ErrorMessage}", ex.Message);
        }
    }
}
