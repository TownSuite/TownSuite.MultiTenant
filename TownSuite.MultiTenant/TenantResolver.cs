using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TownSuite.MultiTenant;

public class TenantResolver
{
    private readonly ILogger<TenantResolver> _logger;
    private readonly IConfigReader _reader;

    private readonly ConcurrentDictionary<string, Tenant> _tenants = new();

    /// <summary>
    /// Read-only view of the resolved tenants. Use <see cref="ResolveAsync"/> /
    /// <see cref="ResolveAll"/> to populate and <see cref="Clear"/> to reset.
    /// </summary>
    public IReadOnlyDictionary<string, Tenant> Tenants => _tenants;

    public TenantResolver(ILogger<TenantResolver> logger, IConfigReader reader)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
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
            t2.TryAddAlias(t2.UniqueId);
            UpdateTenantDictionary(t2.UniqueId, t2);
        }

        return t;
    }

    public async Task<Tenant?> ResolveAsync(string tenantId, bool reset = false,
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
        // tenantId may be an alias (e.g. a DNS hostname). Resolve it to the
        // canonical unique id so the Tenant is built under its real id and is
        // also reachable by that id, not just the alias that was requested.
        var uniqueId = _reader.ResolveUniqueId(tenantId) ?? tenantId;

        var connections = _reader.GetConnections(uniqueId).OrderBy(p => p.Name);

        var t = new Tenant(uniqueId);
        foreach (var connection in connections)
        {
            t.TryAddConnection(connection.Name, connection.ConnStr);
            t.TryAddAlias(connection.TenantOrAlias);
        }

        if (!t.Connections.Any())
        {
            _logger?.LogCritical(
                "Tenant {TenantId} has no connection strings.  Review the appsettings.json/environment variables.",
                tenantId);

            return t;
        }

        return ModifyTenantDictionary(tenantId, reset, t);
    }

    public Tenant? Resolve(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        return _tenants.TryGetValue(tenantId, out var resolve)
            ? resolve
            : SetupTenant(tenantId, reset: false);
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
