using System.Security.Cryptography;
using System.Text;

namespace NexoSferaApi.Services;

/// <summary>
/// Identity of a tenant for caches of SDK data: the resolved database plus the per-key operator and working context
/// (warehouse, branch) from <see cref="SferaTenantContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// The bridge serves several companies from one process (<see cref="SferaConnectionPool"/> keeps one connection per
/// database; <see cref="SferaServiceRouter"/> applies the API key's operator and context per request). Any cache of data
/// read through the SDK must therefore be partitioned by this key; otherwise one tenant's request can be answered with
/// another tenant's data (this happened with the stock snapshots before the fix).
/// </para>
/// <para>
/// The password is deliberately not part of the key, and the key is a SHA-256 hash, so it can be logged without exposing
/// the login. Parts are length-prefixed, so no value can imitate a separator. Values are compared exactly (ordinal): a
/// difference in case or an unset value versus the default only yields a separate partition, never a shared one.
/// </para>
/// <para>
/// Regression check (no test project in this repo, COD-1083): <c>For("A", null, null, null)</c> must differ from
/// <c>For("B", null, null, null)</c>, from <c>For("A", "op", null, null)</c>, from <c>For("A", null, "MG", null)</c> and
/// from <c>For("A", null, null, "O1")</c>, and <c>For("A|B", null, null, null)</c> from <c>For("A", "B", null, null)</c>.
/// </para>
/// </remarks>
public static class TenantCacheKey
{
    /// <summary>Builds the key. <paramref name="database"/> is the resolved database name (never null).</summary>
    public static string For(string database, string? nexoLogin, string? warehouse, string? branch)
    {
        ArgumentNullException.ThrowIfNull(database);

        var builder = new StringBuilder("tenant-v1");
        Append(builder, "db", database.Trim());
        Append(builder, "login", nexoLogin);
        Append(builder, "warehouse", warehouse);
        Append(builder, "branch", branch);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>Key of the current request's tenant on the given connection.</summary>
    public static string For(string database, SferaTenantContext tenant) =>
        For(database, tenant.NexoLogin, tenant.Warehouse, tenant.Branch);

    private static void Append(StringBuilder builder, string name, string? value)
    {
        builder.Append('\n').Append(name).Append('=');
        if (value == null)
        {
            builder.Append("null");
            return;
        }

        builder.Append(value.Length).Append(':').Append(value);
    }
}
