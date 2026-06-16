using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TownSuite.MultiTenant;

public class TenantResolver
{
    private readonly ILogger<TenantResolver> _logger;
    private readonly IConfigReader _reader;

    private readonly ConcurrentDictionary<string, Tenant> _tenants = new();

    public ConcurrentDictionary<string, Tenant> Tenants => _tenants;

    public TenantResolver(ILogger<TenantResolver> logger, IConfigReader reader)
    {
        _logger = logger;
        _reader = reader;
    }

    private void UpdateTenantDictionary(string tenantId, Tenant t)
    {
        _tenants.AddOrUpdate(tenantId, t, (k, o) => t);
    }

    Tenant ModifyTenantDictionary(string tenantId, bool reset, Tenant t)
    {
        if (reset && _tenants.TryGetValue(tenantId, out var original))
        {
            // The tenant already exists in the dictionary.
            // If the values are equal nothing has changed
            // and leave it alone.  The ITenant instances
            // are meant to be long lived and not wiped out.
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
            var t2 = (Tenant)t.Clone();
            t2.Aliases.Add(t2.UniqueId);
            UpdateTenantDictionary(t2.UniqueId, t2);
        }

        return t;
    }

    public async Task<Tenant> ResolveAsync(string tenantId, bool reset = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        if (reset == false && _tenants.TryGetValue(tenantId, out var cached))
        {
            return cached;
        }

        await _reader.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        return SetupTenant(tenantId, reset);
    }

    private Tenant SetupTenant(string tenantId, bool reset)
    {
        var connections = _reader.GetConnections(tenantId).OrderBy(p => p.Name);

        var t = new Tenant(tenantId);
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

        if (!t.Connections.Any())
        {
            _logger?.LogCritical(
                "Tenant {TenantId} has no connection strings.  Review the appsettings.json/environment variables.",
                t.UniqueId);

            return t;
        }

        return ModifyTenantDictionary(tenantId, reset, t);
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

    public async Task ResolveAll(CancellationToken cancellationToken = default)
    {
        await _reader.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        foreach (var uniqueId in _reader.GetTenantIds())
        {
            try
            {
                await ResolveAsync(uniqueId, reset: true, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log invalid tenants as a critical error but let the program continue with valid tenants.
                _logger.LogCritical(ex, "Failed to retrieve resolve tenant {TenantId}", uniqueId);
            }
        }
    }

    public void Clear()
    {
        _tenants.Clear();
        _reader?.Clear();
    }
}
