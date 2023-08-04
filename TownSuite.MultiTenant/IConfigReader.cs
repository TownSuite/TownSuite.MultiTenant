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
    
    Task Refresh();

    bool IsSetup();

    void Clear();
}