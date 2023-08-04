namespace TownSuite.MultiTenant.Console;

public class AppSettingsConfigPairs
{
    public string Id { get; init; }
    public string[] ConfigReaderUrl { get; init; }
    public string ConfigReaderUrlBearerToken { get; init; }
    public string DecryptionKey { get; init; }
    public string UniqueIdDbPattern { get; init; }
    public string SqlUniqueIdLookup { get; init; }
}