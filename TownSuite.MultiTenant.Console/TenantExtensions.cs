using System.Data.Common;
using Microsoft.Data.SqlClient;
using TownSuite.MultiTenant;

public static class TenantExtensions
{
    public static DbConnection CreateConnection(this Tenant tenant, string appName)
    {
        // appName is treated as a literal connection-key fragment, not a regex,
        // so values containing characters like '.' match as written.
        var match = tenant.Connections
            .FirstOrDefault(p => p.Key.Contains(appName, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(match.Key))
        {
            throw new TownSuiteException(
                $"Tenant {tenant.UniqueId} has no connection matching app '{appName}'.");
        }

        return new SqlConnection(match.Value);
    }
}
