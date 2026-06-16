namespace TownSuite.MultiTenant;

public interface IConfigReader
{
    /// <summary>
    /// Return all connection strings for a tenant. Returns an empty list when the
    /// tenant is unknown.
    /// </summary>
    IList<ConnectionStrings> GetConnections(string tenant);

    /// <summary>
    /// Return a specific connection string for a tenant and appType combination,
    /// or an empty string when no match is found.
    /// </summary>
    string GetConnection(string tenant, string appType);

    IList<string> GetTenantIds();

    /// <summary>
    /// Resolve a tenant alias (e.g. a DNS hostname) or unique id to its canonical
    /// unique id. Returns the unique id unchanged when passed one, or null when
    /// the alias/id is unknown. Matching is case-insensitive.
    /// </summary>
    string? ResolveUniqueId(string aliasOrUniqueId);

    /// <summary>
    /// Force a full reload of tenant data. Safe to call concurrently; callers are
    /// serialized so only one reload runs at a time.
    /// </summary>
    Task Refresh(CancellationToken cancellationToken = default);

    /// <summary>
    /// Load tenant data only if nothing is cached yet. Concurrent first-time
    /// callers coalesce into a single load.
    /// </summary>
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

    bool IsSetup();

    /// <summary>
    /// Number of tenant load/initialization errors from the most recent load.
    /// A non-zero value alongside an otherwise empty/partial cache indicates the
    /// last load failed rather than there being no tenants configured.
    /// </summary>
    int LastLoadErrorCount { get; }

    void Clear();
}
