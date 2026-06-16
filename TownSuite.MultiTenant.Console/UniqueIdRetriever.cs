using Microsoft.Data.SqlClient;
using TownSuite.MultiTenant;

/// <summary>
/// Reference <see cref="IUniqueIdRetriever"/> implementation. Lives in the
/// host/app rather than the (database-agnostic) library: it opens the tenant
/// connection and runs the configured lookup query via raw ADO.NET. Swap the
/// provider here if you are not on SQL Server.
/// </summary>
public class UniqueIdRetriever : IUniqueIdRetriever
{
    public async Task<string?> GetUniqueId(ConnectionStrings con, AppSettingsConfigPairs configPairs,
        CancellationToken cancellationToken = default)
    {
        await using var cn = new SqlConnection(con.ConnStr);
        await cn.OpenAsync(cancellationToken);

        await using var cmd = cn.CreateCommand();
        cmd.CommandText = configPairs.ResolvedUniqueIdLookup;

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : result.ToString();
    }
}
