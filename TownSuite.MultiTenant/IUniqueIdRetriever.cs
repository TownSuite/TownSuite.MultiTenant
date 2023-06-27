namespace TownSuite.MultiTenant;

public interface IUniqueIdRetriever
{
    Task<string> GetUniqueId(ConnectionStrings con);
}