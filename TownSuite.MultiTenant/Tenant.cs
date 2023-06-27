using System.Data.Common;

namespace TownSuite.MultiTenant;

public class Tenant : ICloneable
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
        return new Tenant(UniqueId)
        {
            Aliases = this.Aliases,
            Connections = this.Connections,
        };
    }

    public bool Equals(Tenant obj)
    {
        if ((obj is Tenant) == false)
        {
            return false;
        }

        var other = obj as Tenant;

        if (other == null)
        {
            return false;
        }

        if (string.Equals(other.UniqueId, UniqueId) == false)
        {
            return false;
        }

        foreach (var item in Connections)
        {
            if (other.Connections.ContainsKey(item.Key) == false)
            {
                return false;
            }

            if (string.Equals(other.Connections[item.Key], item.Value) == false)
            {
                return false;
            }
        }

        return true;
    }
}