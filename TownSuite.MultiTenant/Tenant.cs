namespace TownSuite.MultiTenant;

public class Tenant : ICloneable, IEquatable<Tenant>
{
    private readonly Dictionary<string, string> _connections;
    private readonly List<string> _aliases;
    private readonly Dictionary<string, string> _appSettings;

    public Tenant(string uniqueId)
        : this(uniqueId, new Dictionary<string, string>(), new List<string>(),
            new Dictionary<string, string>())
    {
    }

    private Tenant(string uniqueId, Dictionary<string, string> connections, List<string> aliases,
        Dictionary<string, string> appSettings)
    {
        UniqueId = uniqueId;
        _connections = connections;
        _aliases = aliases;
        _appSettings = appSettings;
    }

    public string UniqueId { get; }

    /// <summary>
    /// Read-only view of the tenant's connection strings keyed by connection name.
    /// Use <see cref="TryAddConnection"/> to populate.
    /// </summary>
    public IReadOnlyDictionary<string, string> Connections => _connections;

    /// <summary>
    /// Read-only view of the tenant's aliases. Use <see cref="TryAddAlias"/> to populate.
    /// </summary>
    public IReadOnlyList<string> Aliases => _aliases;

    /// <summary>
    /// Read-only view of the tenant's app settings (delivered alongside the
    /// connection strings by the config source). Use <see cref="TryAddAppSetting"/>
    /// to populate.
    /// </summary>
    public IReadOnlyDictionary<string, string> AppSettings => _appSettings;

    /// <summary>
    /// Adds a connection string if one with the same name is not already present.
    /// </summary>
    /// <returns>true if added, false if a connection with that name already existed.</returns>
    public bool TryAddConnection(string name, string connectionString)
    {
        if (_connections.ContainsKey(name))
        {
            return false;
        }

        _connections.Add(name, connectionString);
        return true;
    }

    /// <summary>
    /// Adds an alias if it is not already present.
    /// </summary>
    /// <returns>true if added, false if the alias already existed.</returns>
    public bool TryAddAlias(string alias)
    {
        if (_aliases.Contains(alias))
        {
            return false;
        }

        _aliases.Add(alias);
        return true;
    }

    /// <summary>
    /// Adds an app setting if the key is not already present.
    /// </summary>
    /// <returns>true if added, false if the key already existed.</returns>
    public bool TryAddAppSetting(string key, string value)
    {
        if (_appSettings.ContainsKey(key))
        {
            return false;
        }

        _appSettings.Add(key, value);
        return true;
    }

    public object Clone()
    {
        // Deep copy the collections so mutations to the clone (e.g. adding an
        // alias) do not leak back into the original instance.
        return new Tenant(UniqueId, new Dictionary<string, string>(_connections), new List<string>(_aliases),
            new Dictionary<string, string>(_appSettings));
    }

    /// <summary>
    /// Two tenants are considered equal when they share the same UniqueId, the
    /// exact same connection strings, and the exact same app settings. Aliases are
    /// intentionally not part of equality: they are derived values that may differ
    /// between an alias-keyed clone and a freshly resolved tenant.
    /// </summary>
    public bool Equals(Tenant? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (!string.Equals(other.UniqueId, UniqueId))
        {
            return false;
        }

        return DictionariesEqual(_connections, other._connections)
               && DictionariesEqual(_appSettings, other._appSettings);
    }

    private static bool DictionariesEqual(Dictionary<string, string> a, Dictionary<string, string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var item in a)
        {
            if (!b.TryGetValue(item.Key, out var otherValue) || !string.Equals(otherValue, item.Value))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Tenant);
    }

    public override int GetHashCode()
    {
        // Only UniqueId participates so the hash code stays stable even as the
        // (mutable) collections are populated. Equality still does the full
        // connection/app-setting comparison.
        return UniqueId?.GetHashCode() ?? 0;
    }
}
