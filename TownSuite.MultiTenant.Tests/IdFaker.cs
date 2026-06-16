namespace TownSuite.MultiTenant.Tests;

public class IdFaker : IUniqueIdRetriever
{
    public Task<string?> GetUniqueId(ConnectionStrings con, AppSettingsConfigPairs configPairs,
        CancellationToken cancellationToken = default)
    {
        string? uniqueId = con.Name switch
        {
            var n when n.StartsWith("a.dns.record") => "tenant1",
            var n when n.StartsWith("tenant1_") => "tenant1",
            var n when n.StartsWith("tenant2_") => "tenant2",
            var n when n.StartsWith("second.dns.record") => "tenant2",
            var n when n.StartsWith("tenant3_") => "tenant3",
            var n when n.StartsWith("tenant4_") => "tenant4",
            var n when n.StartsWith("tenant5_") => "tenant5",
            var n when n.StartsWith("fifth.dns.record") => "tenant5",
            var n when n.StartsWith("tenant6_") => "tenant6",
            _ => ""
        };

        return Task.FromResult<string?>(uniqueId);
    }
}
