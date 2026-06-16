using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace TownSuite.MultiTenant;

public static class TenantExtensions
{
    /// <summary>
    /// Creates a <see cref="DbConnection"/> for the first connection whose name
    /// contains <paramref name="appName"/> (case-insensitive). The match is a
    /// literal substring, not a regular expression, so names containing
    /// characters like '.' match as written.
    /// </summary>
    /// <exception cref="TownSuiteException">No connection matches <paramref name="appName"/>.</exception>
    public static DbConnection CreateConnection(this Tenant tenant, string appName)
    {
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
