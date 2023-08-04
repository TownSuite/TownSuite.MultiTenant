namespace TownSuite.MultiTenant.Console;

public class AppSettingsConfigPairs
{
    public string[] ConfigReaderUrl { get; set; }
    public string ConfigReaderUrlBearerToken { get; set; }
    public string DecryptionKey { get; set; }
    public string UniqueIdDbPattern { get; set; }
    public string SqlUniqueIdLookup { get; set; }
}