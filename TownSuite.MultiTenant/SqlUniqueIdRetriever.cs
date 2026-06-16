using Dapper;
using Microsoft.Data.SqlClient;

namespace TownSuite.MultiTenant;

public class SqlUniqueIdRetriever : IUniqueIdRetriever
{
    public async Task<string?> GetUniqueId(ConnectionStrings con, AppSettingsConfigPairs configPairs,
        CancellationToken cancellationToken = default)
    {
        await using var cn = new SqlConnection(con.ConnStr);

        await cn.OpenAsync(cancellationToken);
        string? uniqueId = await cn.QueryFirstOrDefaultAsync<string>(
            new CommandDefinition(configPairs.SqlUniqueIdLookup, cancellationToken: cancellationToken));
        return uniqueId;
    }
}
