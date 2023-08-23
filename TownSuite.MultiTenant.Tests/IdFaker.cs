namespace TownSuite.MultiTenant.Tests;

public class IdFaker : IUniqueIdRetriever
{
    public Task<string> GetUniqueId(ConnectionStrings con, AppSettingsConfigPairs configPairs)
    {

        if (con.Name.StartsWith("a.dns.record"))
        {
            return Task.FromResult($"tenant1");
        }
        if (con.Name.StartsWith("tenant1_"))
        {
            return Task.FromResult($"tenant1");
        }
        if (con.Name.StartsWith("tenant2_"))
        {
            return Task.FromResult($"tenant2");
        }
        if (con.Name.StartsWith("second.dns.record"))
        {
            return Task.FromResult($"tenant2");
        }
        if (con.Name.StartsWith("tenant3_"))
        {
            return Task.FromResult($"tenant3");
        }
        
        return Task.FromResult("");
    }
}