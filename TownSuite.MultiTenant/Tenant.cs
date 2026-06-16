namespace TownSuite.MultiTenant;

public class Tenant : ICloneable, IEquatable<Tenant>
{
    public Tenant(string uniqueId)
    {
        this.UniqueId = uniqueId;
        this.Aliases = new List<string>();
    }

    public Dictionary<string, string> Connections { get; init; } = new Dictionary<string, string>();
    public IList<string> Aliases { get; init; } = new List<string>();
    public string UniqueId { get; init; }

    public object Clone()
    {
        // Deep copy the collections so mutations to the clone (e.g. adding an
        // alias) do not leak back into the original instance.
        return new Tenant(UniqueId)
        {
            Aliases = new List<string>(this.Aliases),
            Connections = new Dictionary<string, string>(this.Connections),
        };
    }

    /// <summary>
    /// Two tenants are considered equal when they share the same UniqueId and
    /// the exact same set of connection strings. Aliases are intentionally not
    /// part of equality: they are derived values that may differ between an
    /// alias-keyed clone and a freshly resolved tenant.
    /// </summary>
    public bool Equals(Tenant other)
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

        if (Connections.Count != other.Connections.Count)
        {
            return false;
        }

        foreach (var item in Connections)
        {
            if (!other.Connections.TryGetValue(item.Key, out var otherValue))
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

    public override bool Equals(object obj)
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
