namespace TownSuite.MultiTenant;

public class AppSettingsConfigPairs
{
    public string Id { get; init; }
    public string[] ConfigReaderUrls { get; init; }
    public string ConfigReaderUrlBearerToken { get; init; }
    public string DecryptionKey { get; init; }
    public string UniqueIdDbPattern { get; init; }
    public string SqlUniqueIdLookup { get; init; }
}