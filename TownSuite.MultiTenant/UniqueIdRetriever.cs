using Dapper;
using Microsoft.Data.SqlClient;

namespace TownSuite.MultiTenant;

public class UniqueIdRetriever : IUniqueIdRetriever
{
    private readonly string _sql;

    public UniqueIdRetriever(string sql)
    {
        _sql = sql;
    }

    public async Task<string> GetUniqueId(ConnectionStrings con)
    {
        await using var cn = new SqlConnection(con.ConnStr);

        await cn.OpenAsync();
        string uniqueId = await cn.QueryFirstOrDefaultAsync<string>(_sql);
        return uniqueId;
    }
}