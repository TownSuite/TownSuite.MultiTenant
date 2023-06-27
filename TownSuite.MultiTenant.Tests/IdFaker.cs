namespace TownSuite.MultiTenant.Tests;

public class IdFaker : IUniqueIdRetriever
{
    private int count = 0;

    public Task<string> GetUniqueId()
    {
        count++;

        return Task.FromResult($"tenant{count}");
    }
}