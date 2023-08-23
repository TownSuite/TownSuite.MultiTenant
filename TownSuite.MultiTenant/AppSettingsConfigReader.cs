using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TownSuite.MultiTenant;

public class AppSettingsConfigReader : ConfigReader
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private readonly ILogger<AppSettingsConfigReader> _logger;

    public AppSettingsConfigReader(Microsoft.Extensions.Configuration.IConfiguration configuration,
        ILogger<AppSettingsConfigReader> logger, IUniqueIdRetriever uniqueIdRetriever,
        Settings settings) : base(uniqueIdRetriever, settings)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public override string GetConnection(string tenant, string appType)
    {
        var connectionString = _connections[tenant]
            .FirstOrDefault(p => string.Equals(p.Name.Split("_").LastOrDefault(), appType,
                StringComparison.InvariantCultureIgnoreCase))?.ConnStr;

        return connectionString ?? "";
    }

    /// <summary>
    /// The Refresh method will load the data.   This should be called by middleware on startup and can be called manually if needed.
    /// This method should be called before the GetConnection method is called.
    /// </summary>
    public override async Task Refresh()
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
            tasks.Add(InitializeUniqueIds(con, pattern, firstSettingsRecord));
        }

        foreach (var task in tasks)
        {
            await task;
        }

        if (Exceptions.Any())
        {
            foreach (var ex in Exceptions)
            {
                _logger.LogError(ex.Message, ex);
            }
        }

        GroupDatabasesByTenant(conns);
    }
}