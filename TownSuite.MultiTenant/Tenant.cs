namespace TownSuite.MultiTenant;

public class Tenant : ICloneable, IEquatable<Tenant>
{
    private readonly Dictionary<string, string> _connections;
    private readonly List<string> _aliases;

    public Tenant(string uniqueId)
        : this(uniqueId, new Dictionary<string, string>(), new List<string>())
    {
    }

    private Tenant(string uniqueId, Dictionary<string, string> connections, List<string> aliases)
    {
        UniqueId = uniqueId;
        _connections = connections;
        _aliases = aliases;
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

    public object Clone()
    {
        // Deep copy the collections so mutations to the clone (e.g. adding an
        // alias) do not leak back into the original instance.
        return new Tenant(UniqueId, new Dictionary<string, string>(_connections), new List<string>(_aliases));
    }

    /// <summary>
    /// Two tenants are considered equal when they share the same UniqueId and
    /// the exact same set of connection strings. Aliases are intentionally not
    /// part of equality: they are derived values that may differ between an
    /// alias-keyed clone and a freshly resolved tenant.
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

        if (_connections.Count != other._connections.Count)
        {
            return false;
        }

        foreach (var item in _connections)
        {
            if (!other._connections.TryGetValue(item.Key, out var otherValue))
            {
                return false;
            }

            if (!string.Equals(otherValue, item.Value))
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
        // (mutable) connection collection is populated. Equality still does the
        // full connection comparison.
        return UniqueId?.GetHashCode() ?? 0;
    }
}
