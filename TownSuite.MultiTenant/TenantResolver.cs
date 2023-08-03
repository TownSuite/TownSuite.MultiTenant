using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TownSuite.MultiTenant;

public class TenantResolver
{
    private readonly ILogger<TenantResolver> _logger;
    private readonly IConfigReader _reader;

    static readonly ConcurrentDictionary<string, Tenant> _tenants =
        new ConcurrentDictionary<string, Tenant>();

    public  ConcurrentDictionary<string, Tenant> Tenants => _tenants;

    public TenantResolver(ILogger<TenantResolver> logger, IConfigReader reader)
    {
        _logger = logger;
        _reader = reader;
    }

    private void UpdateTenantDictionary(string tenantId, Tenant t)
    {
        if (_tenants.AddOrUpdate(tenantId, t, (k, o) => t) == null)
        {
            _logger.LogWarning($"Failed to add/update TenantResolver for {tenantId}");
        }
    }

    Tenant ModifyTenantDictionary(string tenantId, bool reset, Tenant t)
    {
        if (_tenants.ContainsKey(tenantId) && reset)
        {
            // The tenant already exists in the dictionary.
            // If the values are equal nothing has changed
            // and leave it alone.  The ITenant instances
            // are meant to be long lived and not wiped out.
            var original = _tenants[tenantId];

            if (original.Equals(t))
            {
                t = original;
            }
            else
            {
                // If the values have changed something is different.
                UpdateTenantDictionary(tenantId, t);
            }
        }
        else
        {
            // first time through
            UpdateTenantDictionary(tenantId, t);
        }

        if (t.UniqueId != tenantId && !_tenants.ContainsKey(t.UniqueId))
        {
            // if we are using a tenant id such as "developer.townsuite.com" in the appsettings.json
            // this must be converted to the unique id.
            // Auto fill in settings for tenant for that unique id.  This avoids the need to also
            // have a duplicated connectionstring with the unique id.
            var t2 = t.Clone() as Tenant;
            t2.Aliases.Add(t2.UniqueId);
            UpdateTenantDictionary(t2.UniqueId, t2);
        }

        return t;
    }

    public async Task<Tenant> ResolveAsync(string tenantId, bool reset = false)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        if (reset == false && _tenants.TryGetValue(tenantId, out var async))
        {
            return async;
        }

        if (!_reader.IsSetup())
        {
            await _reader.Refresh();
        }

        return SetupTenant(tenantId, reset);
    }

    private Tenant SetupTenant(string tenantId, bool reset)
    {
        var connections = _reader.GetConnections(tenantId);

        var t = new Tenant(tenantId);
        if (connections != null)
        {
            foreach (var connection in connections)
            {
                if (!t.Connections.ContainsKey(connection.Name))
                {
                    t.Connections.Add(connection.Name, connection.ConnStr);
                }

                string alias = connection.Name.Split("_")[0];
                if (!t.Aliases.Contains(alias))
                {
                    t.Aliases.Add(alias);
                }
            }
        }

        if (!t.Connections.Any())
        {
            _logger?.LogCritical(
                $"Tenant {t.UniqueId} has no connection strings.  Review the appsettings.json/environment variables.");

            return t;
        }

        return ModifyTenantDictionary(tenantId, reset, t) as Tenant;
    }

    public Tenant Resolve(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        if (_tenants.TryGetValue(tenantId, out var resolve))
        {
            return resolve;
        }

        return SetupTenant(tenantId, reset: false);
    }

    public async Task ResolveAll()
    {
        if (!_reader.IsSetup())
        {
            await _reader.Refresh();
        }

        foreach (var uniqueId in _reader.GetTenantIds())
        {
            try
            {
                await ResolveAsync(uniqueId, reset: true);
            }
            catch (Exception ex)
            {
                // Log invalid tenants as a critical error but let the program continue with valid tenants.
                _logger.LogCritical(ex, $"Failed to retrieve resolve tenant {uniqueId}");
            }
        }
    }

    public void Clear()
    {
        _tenants.Clear();
    }
}