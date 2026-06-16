namespace TownSuite.MultiTenant;

public record AppSettingsConfigPairs
{
    public string Id { get; init; } = "";
    public string[] ConfigReaderUrls { get; init; } = Array.Empty<string>();
    public string ConfigReaderUrlBearerToken { get; init; } = "";
    public string DecryptionKey { get; init; } = "";
    public string UniqueIdDbPattern { get; init; } = "";

    /// <summary>
    /// Opaque value forwarded to your <see cref="IUniqueIdRetriever"/>. For the
    /// SQL reference retriever this is the lookup query. Prefer this over the
    /// legacy <see cref="SqlUniqueIdLookup"/>.
    /// </summary>
    public string UniqueIdLookup { get; init; } = "";

    /// <summary>
    /// Legacy alias for <see cref="UniqueIdLookup"/>, still bound from
    /// configuration for backwards compatibility. Use <see cref="UniqueIdLookup"/>
    /// for new configuration.
    /// </summary>
    public string SqlUniqueIdLookup { get; init; } = "";

    /// <summary>
    /// The effective lookup value: <see cref="UniqueIdLookup"/> when set,
    /// otherwise the legacy <see cref="SqlUniqueIdLookup"/>. Retrievers should
    /// read this rather than either field directly.
    /// </summary>
    public string ResolvedUniqueIdLookup =>
        !string.IsNullOrWhiteSpace(UniqueIdLookup) ? UniqueIdLookup : SqlUniqueIdLookup;
}
