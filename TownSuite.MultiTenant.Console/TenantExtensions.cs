using System.Data.Common;
using Microsoft.Data.SqlClient;
using TownSuite.MultiTenant;

public static class TenantExtensions
{
    /// <summary>
    /// Creates a SQL Server <see cref="DbConnection"/> for the connection whose
    /// name matches <paramref name="appName"/>. The connection-string lookup is
    /// provided by the (provider-agnostic) library; constructing the concrete
    /// provider connection is the host's responsibility.
    /// </summary>
    public static DbConnection CreateConnection(this Tenant tenant, string appName)
    {
        return new SqlConnection(tenant.GetConnectionString(appName));
    }
}
