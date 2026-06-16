namespace TownSuite.MultiTenant;

public record Settings
{
    public AppSettingsConfigPairs[] ConfigPairs { get; init; }
    public string UserAgent { get; init; }
}
