using Dapper;
using Microsoft.Data.SqlClient;

namespace TownSuite.MultiTenant;

public class SqlUniqueIdRetriever : IUniqueIdRetriever
{
    public async Task<string> GetUniqueId(ConnectionStrings con, AppSettingsConfigPairs configPairs)
    {
        await using var cn = new SqlConnection(con.ConnStr);

        await cn.OpenAsync();
        string uniqueId = await cn.QueryFirstOrDefaultAsync<string>(configPairs.SqlUniqueIdLookup);
        return uniqueId;
    }
}