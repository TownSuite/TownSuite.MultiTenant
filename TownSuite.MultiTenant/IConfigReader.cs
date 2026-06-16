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

    void Clear();
}
