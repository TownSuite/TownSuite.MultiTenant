namespace TownSuite.MultiTenant;

public interface IConfigReader
{
    /// <summary>
    /// Return a specific connection string for a tenant and apptype combination.
    /// </summary>
    /// <returns></returns>
    IList<ConnectionStrings> GetConnections(string tenant);

    string GetConnection(string tenant, string appType);
    
    IList<string> GetTenantIds();

    /// <summary>
    /// Return a list of all connections.
    /// </summary>
    /// <returns>key=UniqueTenantId</returns>
    Task Refresh();

    bool IsSetup();
}