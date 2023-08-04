using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using TownSuite.MultiTenant;

public static class TenantExtensions
{
    public static DbConnection CreateConnection(this Tenant tenant, string appName)
    {
        var regex = new Regex(appName, RegexOptions.IgnoreCase);
        return new SqlConnection(tenant.Connections.FirstOrDefault(p => regex.IsMatch(p.Key)).Value);
    }
}