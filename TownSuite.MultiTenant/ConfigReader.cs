using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace TownSuite.MultiTenant;

/// <summary>
/// Loads and caches tenant connection strings. Data is loaded on demand (or via
/// an explicit <see cref="Refresh"/>) and cached on the instance. Register the
/// reader as a singleton to share the cache process-wide.
/// </summary>
public abstract class ConfigReader : IConfigReader
{
    /// <summary>
    /// Caps how many tenant unique-id lookups (each opening a SQL connection)
    /// run at once so a large tenant set cannot exhaust the connection pool.
    /// </summary>
    private const int MaxConcurrentUniqueIdLookups = 8;

    protected ConcurrentDictionary<string, IList<ConnectionStrings>> _connections = new();
    private readonly IUniqueIdRetriever _uniqueIdRetriever;
    protected readonly Settings _settings;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SemaphoreSlim _lookupThrottle = new(MaxConcurrentUniqueIdLookups);

    protected ConcurrentBag<Exception> Exceptions { get; } = new();

    protected ConfigReader(IUniqueIdRetriever uniqueIdRetriever, Settings settings)
    {
        _uniqueIdRetriever = uniqueIdRetriever;
        _settings = settings;
    }

    public IList<ConnectionStrings> GetConnections(string tenant)
    {
        return _connections.TryGetValue(tenant, out var conns)
            ? conns
            : new List<ConnectionStrings>();
    }

    public string GetConnection(string tenant, string appType)
    {
        if (!_connections.TryGetValue(tenant, out var conns))
        {
            return "";
        }

        return conns
            .FirstOrDefault(p => string.Equals(p.Name.Split("_").LastOrDefault(), appType,
                StringComparison.InvariantCultureIgnoreCase))?.ConnStr ?? "";
    }

    /// <summary>
    /// Force a full reload of tenant connection data. Concurrent callers are
    /// serialized so a burst of requests results in a single reload at a time.
    /// </summary>
    public async Task Refresh(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await LoadConnectionsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Load tenant data only if nothing is cached yet. Concurrent first-time
    /// callers coalesce into a single load instead of each triggering a refresh.
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsSetup())
        {
            return;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsSetup())
            {
                return;
            }

            await LoadConnectionsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Implementation-specific load of all tenant connection strings into
    /// <see cref="_connections"/>. Always invoked under the refresh lock.
    /// </summary>
    protected abstract Task LoadConnectionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Clear the cache without reloading.
    /// </summary>
    public void Clear()
    {
        _connections.Clear();
    }

    public bool IsSetup()
    {
        return !_connections.IsEmpty;
    }

    protected async Task InitializeUniqueIds(ConnectionStrings con, string pattern, AppSettingsConfigPairs configPairs,
        CancellationToken cancellationToken = default)
    {
        string? tenant = con.Name.Split("_").FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenant))
        {
            return;
        }

        Match m = Regex.Match(con.Name, pattern, RegexOptions.IgnoreCase);
        if (!m.Success)
        {
            Exceptions.Add(new TownSuiteException($"{con.Name} did not match pattern {pattern}"));
            return;
        }

        try
        {
            await _lookupThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            string uniqueId;
            try
            {
                uniqueId = await _uniqueIdRetriever.GetUniqueId(con, configPairs, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _lookupThrottle.Release();
            }

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
            updateValueFactory: (k, existinglist) =>
            {
                var existing = existinglist.FirstOrDefault(p =>
                    string.Equals(p.Name, con.Name, StringComparison.InvariantCultureIgnoreCase));

                if (existing == null)
                {
                    existinglist.Add(con);
                }
                else
                {
                    existing.ChangeConnStr(con.ConnStr);
                }

                return existinglist;
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

    public IList<string> GetTenantIds()
    {
        return _connections.Keys.Distinct().ToList();
    }
}
