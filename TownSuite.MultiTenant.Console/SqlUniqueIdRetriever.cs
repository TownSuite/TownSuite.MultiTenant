using Microsoft.Data.SqlClient;
using TownSuite.MultiTenant;

/// <summary>
/// SQL Server implementation of <see cref="IUniqueIdRetriever"/>. Lives in the
/// host/app rather than the (database-agnostic) library: it opens the tenant
/// connection and runs the configured lookup query via raw ADO.NET.
/// </summary>
public class SqlUniqueIdRetriever : IUniqueIdRetriever
{
    public async Task<string?> GetUniqueId(ConnectionStrings con, AppSettingsConfigPairs configPairs,
        CancellationToken cancellationToken = default)
    {
        await using var cn = new SqlConnection(con.ConnStr);
        await cn.OpenAsync(cancellationToken);

        await using var cmd = cn.CreateCommand();
        cmd.CommandText = configPairs.SqlUniqueIdLookup;

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : result.ToString();
    }
}
