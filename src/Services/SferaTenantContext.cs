using System.Security.Claims;

namespace NexoSferaApi.Services;

/// <summary>
/// Per-request tenant information resolved from the authenticated API key's claims
/// (see ApiKeyAuthenticationHandler). All values are optional - a key without overrides
/// works on the default database with the default operator and context.
/// </summary>
public sealed record SferaTenantContext(
    string? Database,
    string? NexoLogin,
    string? NexoPassword,
    string? Warehouse,
    string? Branch)
{
    public static readonly SferaTenantContext Default = new(null, null, null, null, null);

    public static SferaTenantContext FromPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return Default;
        }

        return new SferaTenantContext(
            Database: principal.FindFirst("NexoDatabase")?.Value,
            NexoLogin: principal.FindFirst("NexoLogin")?.Value,
            NexoPassword: principal.FindFirst("NexoPassword")?.Value,
            Warehouse: principal.FindFirst("NexoWarehouse")?.Value,
            Branch: principal.FindFirst("NexoBranch")?.Value);
    }
}
