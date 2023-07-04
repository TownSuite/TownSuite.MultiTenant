using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace TownSuite.MultiTenant;

public abstract class ConfigReader : IConfigReader
{
    protected ConcurrentDictionary<string, IList<ConnectionStrings>> _connections;
    private readonly IUniqueIdRetriever _uniqueIdRetriever;

    private TsWebClient _webClient;

    public List<Exception> Exceptions { get; private set; } = new List<Exception>();

    protected ConfigReader(IUniqueIdRetriever uniqueIdRetriever)
    {
        _uniqueIdRetriever = uniqueIdRetriever;
    }

    public IList<ConnectionStrings> GetConnections(string tenant)
    {
        return _connections[tenant];
    }

    public abstract string GetConnection(string tenant, string appType);

    /// <summary>
    /// Return a list of all connections.
    /// </summary>
    /// <returns>key=UniqueTenantId</returns>
    public abstract Task Refresh();

    public bool IsSetup()
    {
        return _connections != null && _connections.Any();
    }
    
    protected async Task InitializeUniqueIds(ConnectionStrings con, string pattern)
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

            AddOrUpdateCons(con, uniqueId);
        }
        catch (Exception ex)
        {
            Exceptions.Add(new TownSuiteException($"Failed to resolve and initialize tenant {con.Name}.", ex));
        }
    }


    private void AddOrUpdateCons(ConnectionStrings con, string uniqueId)
    {
        _connections.AddOrUpdate(uniqueId,
            addValueFactory: (key) => new List<ConnectionStrings>() { con },
            updateValueFactory: (s, list) =>
            {
                if (!list.Any(p => string.Equals(p.Name, con.Name, StringComparison.InvariantCultureIgnoreCase)))
                {
                    list.Add(con);
                }
                else
                {
                    var tmp = list.FirstOrDefault(p =>
                        string.Equals(p.Name, con.Name, StringComparison.InvariantCultureIgnoreCase));

                    tmp.ChangeConnStr(con.ConnStr);
                }

                return list;
            });
    }

    protected void GroupDatabasesByTenant(List<ConnectionStrings> conns)
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

                AddOrUpdateCons(con, tenantKey);
            }
        }
    }
}