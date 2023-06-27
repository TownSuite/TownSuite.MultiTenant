using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TownSuite.MultiTenant;

public class AppSettingsConfigReader : IConfigReader
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private readonly ILogger<AppSettingsConfigReader> _logger;
    private readonly IUniqueIdRetriever _uniqueIdRetriever;
    ConcurrentDictionary<string, IList<ConnectionStrings>> _connections;

    public AppSettingsConfigReader(Microsoft.Extensions.Configuration.IConfiguration configuration,
        ILogger<AppSettingsConfigReader> logger, IUniqueIdRetriever uniqueIdRetriever)
    {
        _configuration = configuration;
        _logger = logger;
        _uniqueIdRetriever = uniqueIdRetriever;
    }

    public virtual IList<ConnectionStrings> GetConnections(string tenant)
    {
        return _connections[tenant];
    }

    public bool IsSetup()
    {
        return _connections != null && _connections.Any();
    }

    public virtual string GetConnection(string tenant, string appType)
    {
        var connectionString = _connections[tenant]
            .FirstOrDefault(p => string.Equals(p.Name, appType, StringComparison.InvariantCultureIgnoreCase))?.ConnStr;

        return connectionString ?? "";
    }

    /// <summary>
    /// The Refresh method will load the data.   This should be called by middleware on startup and can be called manually if needed.
    /// This method should be called before the GetConnection method is called.
    /// </summary>
    public virtual async Task Refresh()
    {
        var connections = _configuration.GetSection("ConnectionStrings").GetChildren();
        _connections = new ConcurrentDictionary<string, IList<ConnectionStrings>>();
        var conns = new List<ConnectionStrings>();

        string pattern = _configuration.GetSection("TenantSettings__UniqueIdDbPattern").Value;
        string sql = _configuration.GetSection("TenantSettings__SqlUniqueIdLookup").Value;

        var tasks = new List<Task>();
        foreach (var connection in connections)
        {
            var con = new ConnectionStrings() { Name = connection.Key, ConnStr = connection.Value };
            conns.Add(con);
            tasks.Add(InitializeUniqueIds(con, pattern, sql));
        }

        foreach (var task in tasks)
        {
            await task;
        }

        GroupDatabasesByTenant(conns);
    }

    private async Task InitializeUniqueIds(ConnectionStrings con, string pattern, string sql)
    {
        string? tenant = con.Name.Split("_").FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenant))
        {
            return;
        }

        Match m = Regex.Match(con.Name, pattern, RegexOptions.IgnoreCase);
        if (!m.Success)
        {
            return;
        }

        try
        {
            string uniqueId = await _uniqueIdRetriever.GetUniqueId(con);

            _connections.AddOrUpdate(uniqueId,
                addValueFactory: (key) => new List<ConnectionStrings>() { con },
                updateValueFactory: (s, list) =>
                {
                    list.Add(con);
                    return list;
                });
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                $"Failed to resolve and initialize tenant {con.Name}.",
                ex);
        }
    }

    private void GroupDatabasesByTenant(List<ConnectionStrings> conns)
    {
        // This method will find all connection strings that follow the {tenant/alias}_{name/dbType} pattern
        // that were not found to match the DatabaseWithUniqueId pattern
        // and add them to the _connections dictionary with the UniqueId as the key.

        foreach (var con in conns)
        {
            string conTenantOrAlias = con.Name.Split("_").FirstOrDefault();

            foreach (var tenantKey in _connections.Keys)
            {
                var found = _connections[tenantKey].Any(c =>
                    string.Equals(c.Name.Split("_").FirstOrDefault(),
                        conTenantOrAlias, StringComparison.InvariantCultureIgnoreCase));

                if (!found)
                {
                    continue;
                }

                _connections.AddOrUpdate(tenantKey,
                    addValueFactory: (key) => new List<ConnectionStrings>() { con },
                    updateValueFactory: (s, list) =>
                    {
                        if (!list.Contains(con)) list.Add(con);
                        return list;
                    });
            }
        }
    }
}