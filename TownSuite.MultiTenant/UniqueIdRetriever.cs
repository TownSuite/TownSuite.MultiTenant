using Dapper;
using Microsoft.Data.SqlClient;

namespace TownSuite.MultiTenant;

public class UniqueIdRetriever : IUniqueIdRetriever
{
    private readonly string _connectionString;
    private readonly string _sql;

    public UniqueIdRetriever(string connectionString, string sql)
    {
        _connectionString = connectionString;
        _sql = sql;
    }

    public async Task<string> GetUniqueId()
    {
        await using var cn = new SqlConnection(_connectionString);

        await cn.OpenAsync();
        string uniqueId = await cn.QueryFirstOrDefaultAsync<string>(_sql);
        return uniqueId;
    }
}