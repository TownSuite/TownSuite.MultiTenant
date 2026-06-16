using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

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

    private static readonly ConcurrentDictionary<string, Regex> _patternCache = new();

    /// <summary>
    /// Immutable-by-convention snapshot of loaded tenant data. Connections are
    /// keyed by canonical unique id; AliasToUniqueId maps every alias (and each
    /// unique id, to itself) to its canonical unique id. Both are published
    /// together via a single reference swap so readers never see a mismatched or
    /// half-built pair.
    /// </summary>
    private sealed class Cache
    {
        public Cache(ConcurrentDictionary<string, IList<ConnectionStrings>> connections,
            ConcurrentDictionary<string, string> aliasToUniqueId,
            ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> appSettings)
        {
            Connections = connections;
            AliasToUniqueId = aliasToUniqueId;
            AppSettings = appSettings;
        }

        public ConcurrentDictionary<string, IList<ConnectionStrings>> Connections { get; }
        public ConcurrentDictionary<string, string> AliasToUniqueId { get; }
        public ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> AppSettings { get; }

        public static Cache CreateEmpty() =>
            new(new(), new(StringComparer.InvariantCultureIgnoreCase),
                new(StringComparer.InvariantCultureIgnoreCase));
    }

    private static readonly IReadOnlyDictionary<string, string> _emptyAppSettings =
        new Dictionary<string, string>();

    private volatile Cache _cache = Cache.CreateEmpty();
    private readonly IUniqueIdRetriever _uniqueIdRetriever;
    private readonly ILogger _logger;
    protected readonly Settings _settings;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SemaphoreSlim _lookupThrottle = new(MaxConcurrentUniqueIdLookups);

    private readonly ConcurrentBag<Exception> _exceptions = new();
    private int _lastLoadErrorCount;

    /// <summary>
    /// Number of tenant load/initialization errors encountered during the most
    /// recent load. Lets callers/health checks distinguish a clean load from a
    /// partial or total failure that left the cache empty.
    /// </summary>
    public int LastLoadErrorCount => _lastLoadErrorCount;

    protected ConfigReader(IUniqueIdRetriever uniqueIdRetriever, Settings settings, ILogger logger)
    {
        _uniqueIdRetriever = uniqueIdRetriever ?? throw new ArgumentNullException(nameof(uniqueIdRetriever));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (settings.ConfigPairs == null || settings.ConfigPairs.Length == 0)
        {
            throw new TownSuiteException(
                "TenantSettings.ConfigPairs is missing or empty. Configure at least one config pair.");
        }
    }

    /// <summary>
    /// Resolve a tenant alias (e.g. a DNS hostname) or unique id to its canonical
    /// unique id. Returns the unique id unchanged when passed one, or null when
    /// the alias/id is unknown. Matching is case-insensitive.
    /// </summary>
    public string? ResolveUniqueId(string aliasOrUniqueId)
    {
        if (string.IsNullOrWhiteSpace(aliasOrUniqueId))
        {
            return null;
        }

        return _cache.AliasToUniqueId.TryGetValue(aliasOrUniqueId, out var uniqueId) ? uniqueId : null;
    }

    public IList<ConnectionStrings> GetConnections(string tenant)
    {
        var cache = _cache;
        var key = ResolveKey(cache, tenant);

        // Return a copy so callers cannot mutate the cached list.
        return cache.Connections.TryGetValue(key, out var conns)
            ? new List<ConnectionStrings>(conns)
            : new List<ConnectionStrings>();
    }

    public string GetConnection(string tenant, string appType)
    {
        var cache = _cache;
        var key = ResolveKey(cache, tenant);

        if (!cache.Connections.TryGetValue(key, out var conns))
        {
            return "";
        }

        return conns
            .FirstOrDefault(p => string.Equals(p.AppType, appType, StringComparison.InvariantCultureIgnoreCase))
            ?.ConnStr ?? "";
    }

    public IReadOnlyDictionary<string, string> GetAppSettings(string tenant)
    {
        var cache = _cache;
        var key = ResolveKey(cache, tenant);

        return cache.AppSettings.TryGetValue(key, out var settings) ? settings : _emptyAppSettings;
    }

    private static string ResolveKey(Cache cache, string tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant) && cache.AliasToUniqueId.TryGetValue(tenant, out var uniqueId))
        {
            return uniqueId;
        }

        return tenant;
    }

    /// <summary>
    /// Force a full reload of tenant connection data. Concurrent callers are
    /// serialized so a burst of requests results in a single reload at a time.
    /// The freshly built data is swapped in atomically, so readers never observe
    /// a partially populated cache.
    /// </summary>
    public async Task Refresh(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await LoadAndSwapAsync(cancellationToken).ConfigureAwait(false);
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

            await LoadAndSwapAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task LoadAndSwapAsync(CancellationToken cancellationToken)
    {
        // Build into a private dictionary and publish it in one assignment so
        // concurrent readers always see either the previous or the new data set,
        // never a half-built one.
        var target = new ConcurrentDictionary<string, IList<ConnectionStrings>>();
        var appSettings = new ConcurrentDictionary<string, IReadOnlyDictionary<string, string>>();
        Interlocked.Exchange(ref _lastLoadErrorCount, 0);

        await LoadConnectionsAsync(target, appSettings, cancellationToken).ConfigureAwait(false);

        _cache = new Cache(target, BuildAliasIndex(target), appSettings);
    }

    /// <summary>
    /// Builds the alias -> canonical unique id lookup from loaded connections.
    /// Every unique id maps to itself; every connection's tenant/alias prefix
    /// maps to the unique id it belongs to. Unique-id identities are added first
    /// so a real unique id always wins over an alias that happens to collide.
    /// </summary>
    private static ConcurrentDictionary<string, string> BuildAliasIndex(
        ConcurrentDictionary<string, IList<ConnectionStrings>> connections)
    {
        var index = new ConcurrentDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        foreach (var uniqueId in connections.Keys)
        {
            index.TryAdd(uniqueId, uniqueId);
        }

        foreach (var entry in connections)
        {
            foreach (var con in entry.Value)
            {
                var alias = con.TenantOrAlias;
                if (!string.IsNullOrEmpty(alias))
                {
                    index.TryAdd(alias, entry.Key);
                }
            }
        }

        return index;
    }

    /// <summary>
    /// Implementation-specific load of all tenant connection strings into
    /// <paramref name="target"/> (keyed by canonical unique id) and any per-tenant
    /// app settings into <paramref name="appSettings"/>. Always invoked under the
    /// refresh lock; the base class publishes both atomically once this completes.
    /// </summary>
    protected abstract Task LoadConnectionsAsync(
        ConcurrentDictionary<string, IList<ConnectionStrings>> target,
        ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> appSettings,
        CancellationToken cancellationToken);

    /// <summary>
    /// Clear the cache without reloading.
    /// </summary>
    public void Clear()
    {
        _cache = Cache.CreateEmpty();
    }

    public bool IsSetup()
    {
        return !_cache.Connections.IsEmpty;
    }

    protected async Task InitializeUniqueIds(ConcurrentDictionary<string, IList<ConnectionStrings>> target,
        ConnectionStrings con, string pattern, AppSettingsConfigPairs configPairs,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(con.TenantOrAlias))
        {
            return;
        }

        if (!GetRegex(pattern).IsMatch(con.Name))
        {
            // Not matching the unique-id pattern is expected: these connections
            // (e.g. a tenant's secondary apps) are attached later by alias in
            // GroupDatabasesByTenant. This is normal flow, not a load error.
            _logger.LogDebug("{ConnectionName} did not match unique-id pattern {Pattern}; will group by alias.",
                con.Name, pattern);
            return;
        }

        try
        {
            await _lookupThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            string? uniqueId;
            try
            {
                uniqueId = await _uniqueIdRetriever.GetUniqueId(con, configPairs, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _lookupThrottle.Release();
            }

            if (string.IsNullOrWhiteSpace(uniqueId))
            {
                _exceptions.Add(new TownSuiteException(
                    $"Tenant {con.Name} resolved to an empty unique id. Check the configured unique-id lookup."));
                return;
            }

            AddOrUpdateCons(target, con, uniqueId);
        }
        catch (Exception ex)
        {
            _exceptions.Add(new TownSuiteException($"Failed to resolve and initialize tenant {con.Name}.", ex));
        }
    }

    private static Regex GetRegex(string pattern)
    {
        return _patternCache.GetOrAdd(pattern,
            p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled));
    }

    /// <summary>
    /// Adds a connection directly under a known canonical unique id (used when the
    /// config source already provides the tenant id, so no lookup is needed).
    /// </summary>
    protected static void AddConnection(ConcurrentDictionary<string, IList<ConnectionStrings>> target,
        ConnectionStrings con, string uniqueId) => AddOrUpdateCons(target, con, uniqueId);

    private static void AddOrUpdateCons(ConcurrentDictionary<string, IList<ConnectionStrings>> target,
        ConnectionStrings con, string uniqueId)
    {
        target.AddOrUpdate(uniqueId,
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
                    existing.SetDecryptedConnStr(con.ConnStr);
                }

                return existinglist;
            });
    }

    protected void GroupDatabasesByTenant(ConcurrentDictionary<string, IList<ConnectionStrings>> target,
        List<ConnectionStrings> conns)
    {
        // Find all connection strings that follow the {tenant/alias}_{name/dbType}
        // pattern and attach them to every tenant that already owns a connection
        // sharing the same tenant/alias prefix.
        //
        // An alias -> tenantKey index is built once up front so this runs in
        // roughly linear time instead of scanning every tenant for every
        // connection. The per-tenant alias set is stable while grouping (a
        // connection is only ever added to a tenant whose alias it already
        // matches), so the prebuilt index stays correct.
        var aliasIndex = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var entry in target)
        {
            foreach (var existing in entry.Value)
            {
                var alias = existing.TenantOrAlias;
                if (string.IsNullOrEmpty(alias))
                {
                    continue;
                }

                if (!aliasIndex.TryGetValue(alias, out var keys))
                {
                    keys = new List<string>();
                    aliasIndex[alias] = keys;
                }

                if (!keys.Contains(entry.Key))
                {
                    keys.Add(entry.Key);
                }
            }
        }

        foreach (var con in conns)
        {
            if (!string.IsNullOrEmpty(con.TenantOrAlias)
                && aliasIndex.TryGetValue(con.TenantOrAlias, out var tenantKeys))
            {
                foreach (var tenantKey in tenantKeys)
                {
                    AddOrUpdateCons(target, con, tenantKey);
                }
            }
        }
    }

    /// <summary>
    /// Logs and clears any errors accumulated during the current load, tracking
    /// the count in <see cref="LastLoadErrorCount"/>. Implementations should call
    /// this after each batch of <see cref="InitializeUniqueIds"/> work.
    /// </summary>
    protected void LogAndDrainExceptions()
    {
        while (_exceptions.TryTake(out var ex))
        {
            Interlocked.Increment(ref _lastLoadErrorCount);
            _logger.LogError(ex, "Tenant configuration load error: {ErrorMessage}", ex.Message);
        }
    }

    public IList<string> GetTenantIds()
    {
        return _cache.Connections.Keys.ToList();
    }
}
