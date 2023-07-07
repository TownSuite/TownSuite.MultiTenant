namespace TownSuite.MultiTenant;

public class Settings
{
    public string UniqueIdDbPattern { get; init; }
    public string DecryptionKey { get; init; }
    public string[] ConfigReaderUrls { get; init; }
}