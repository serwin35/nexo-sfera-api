using System.Reflection;

namespace NexoSferaApi.Helpers;

/// <summary>
/// The commit and build time baked into the bridge assembly at build (<c>AssemblyMetadata</c>, see the csproj), so the
/// running build can be named without a shell on the host.
/// </summary>
public static class BuildInfo
{
    public const string Unknown = "unknown";
    public const string CommitKey = "GitCommit";
    public const string DirtyKey = "GitDirty";
    public const string BuiltAtKey = "BuildTimeUtc";

    /// <summary>What the build recorded; <see cref="Unknown"/> for a value the build could not read (no git).</summary>
    public sealed record Values(string Commit, bool? Dirty, string BuiltAtUtc);

    public static Values Read(Assembly assembly) => Read(assembly.GetCustomAttributes<AssemblyMetadataAttribute>());

    public static Values Read(IEnumerable<AssemblyMetadataAttribute> metadata)
    {
        var values = metadata
            .GroupBy(m => m.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);

        string Get(string key) =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : Unknown;

        var commit = Get(CommitKey);
        // A commit is 40 (SHA-1) or 64 (SHA-256) hex characters; anything else is not one.
        if (!(commit.Length is 40 or 64 && commit.All(Uri.IsHexDigit)))
        {
            commit = Unknown;
        }

        bool? dirty = Get(DirtyKey) switch
        {
            "true" => true,
            "false" => false,
            _ => null,
        };

        return new Values(commit.ToLowerInvariant(), dirty, Get(BuiltAtKey));
    }

    /// <summary>The informational version of an assembly (the Nexo SDK one carries "62.0.0.9537+hash").</summary>
    public static string VersionOf(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? assembly.GetName().Version?.ToString()
        ?? Unknown;
}
