namespace TownSuite.MultiTenant;

public interface IUniqueIdRetriever
{
    Task<string?> GetUniqueId(ConnectionStrings con, AppSettingsConfigPairs appSettingsConfigPairs,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves the canonical unique id for a tenant connection. Supply your own
/// implementation (the library is database-agnostic; e.g. open the connection
/// and run <see cref="AppSettingsConfigPairs.UniqueIdLookup"/>, or look the id
/// up however you like).
/// </summary>
public delegate Task<string?> UniqueIdLookup(ConnectionStrings con, AppSettingsConfigPairs appSettingsConfigPairs,
    CancellationToken cancellationToken);

/// <summary>
/// Adapts a <see cref="UniqueIdLookup"/> delegate to <see cref="IUniqueIdRetriever"/>
/// so callers can provide the lookup as a lambda instead of a class.
/// </summary>
public sealed class DelegateUniqueIdRetriever : IUniqueIdRetriever
{
    private readonly UniqueIdLookup _lookup;

    public DelegateUniqueIdRetriever(UniqueIdLookup lookup)
    {
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
    }

    public Task<string?> GetUniqueId(ConnectionStrings con, AppSettingsConfigPairs appSettingsConfigPairs,
        CancellationToken cancellationToken = default) =>
        _lookup(con, appSettingsConfigPairs, cancellationToken);
}
