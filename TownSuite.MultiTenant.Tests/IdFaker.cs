namespace TownSuite.MultiTenant.Tests;

public class IdFaker : IUniqueIdRetriever
{
    public Task<string> GetUniqueId(ConnectionStrings con)
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

        
        return Task.FromResult("");
    }
}