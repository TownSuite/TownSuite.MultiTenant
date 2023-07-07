using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TownSuite.MultiTenant;

public class HttpConfigReader : ConfigReader
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private readonly ILogger<HttpConfigReader> _logger;
    private readonly IUniqueIdRetriever _uniqueIdRetriever;

    private TsWebClient _webClient;

    public HttpConfigReader(Microsoft.Extensions.Configuration.IConfiguration configuration,
        ILogger<HttpConfigReader> logger, IUniqueIdRetriever uniqueIdRetriever,
        TsWebClient webClient,
        Settings settings) : base(uniqueIdRetriever, settings)
    {
        _configuration = configuration;
        _logger = logger;
        _uniqueIdRetriever = uniqueIdRetriever;
        _webClient = webClient;
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
        var configReaderUrls = _settings.ConfigReaderUrls;
        _connections = new ConcurrentDictionary<string, IList<ConnectionStrings>>();

        foreach (var configReaderUrl in configReaderUrls)
        {
            var tenants = await _webClient.GetAsync(configReaderUrl, System.Threading.CancellationToken.None);
            var conns = new List<ConnectionStrings>();

            string pattern = _settings.UniqueIdDbPattern;

            var tasks = new List<Task>();
            foreach (var tenant in tenants)
            {
                foreach (var connection in tenant.Connections)
                {
                    var con = new ConnectionStrings(_settings.DecryptionKey)
                        { Name = $"{tenant.TenantId}_{connection.Key}", ConnStr = connection.Value };
                    conns.Add(con);
                    tasks.Add(InitializeUniqueIds(con, pattern));
                }
            }

            foreach (var task in tasks)
            {
                await task;
            }
            
            if (Exceptions.Any())
            {
                foreach(var ex in Exceptions)
                {
                    _logger.LogError(ex.Message, ex);
                }
            }

            GroupDatabasesByTenant(conns);
        }
    }
}