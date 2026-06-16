namespace TownSuite.MultiTenant;

public record Settings
{
    public AppSettingsConfigPairs[] ConfigPairs { get; init; } = Array.Empty<AppSettingsConfigPairs>();
    public string UserAgent { get; init; } = "";
}
