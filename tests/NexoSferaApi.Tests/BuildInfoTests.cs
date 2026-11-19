using System.Reflection;
using NexoSferaApi.Helpers;
using Xunit;

namespace NexoSferaApi.Tests;

/// <summary>GET /api/health/version names the running build from the metadata the build baked in.</summary>
public sealed class BuildInfoTests
{
    private const string Commit = "fec2a9f0123456789abcdef0123456789abcdef0";

    private static AssemblyMetadataAttribute Meta(string key, string? value) => new(key, value);

    [Fact]
    public void ReadsTheCommitTheDirtyFlagAndTheBuildTime()
    {
        var values = BuildInfo.Read(new[]
        {
            Meta(BuildInfo.CommitKey, Commit.ToUpperInvariant() + "\n"),
            Meta(BuildInfo.DirtyKey, "false"),
            Meta(BuildInfo.BuiltAtKey, "2026-11-19T02:40:00Z"),
        });

        Assert.Equal(new BuildInfo.Values(Commit, false, "2026-11-19T02:40:00Z"), values);
    }

    [Fact]
    public void WhatTheBuildCouldNotRecordIsUnknown()
    {
        var values = BuildInfo.Read(new[] { Meta(BuildInfo.CommitKey, ""), Meta(BuildInfo.DirtyKey, "") });

        Assert.Equal(new BuildInfo.Values(BuildInfo.Unknown, null, BuildInfo.Unknown), values);
    }

    [Theory]
    [InlineData("fatal: not a git repository (or any of the parent directories): .git")]
    [InlineData("fec2a9f")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void SomethingElseThanACommitIsNotReportedAsOne(string text)
    {
        Assert.Equal(BuildInfo.Unknown, BuildInfo.Read(new[] { Meta(BuildInfo.CommitKey, text) }).Commit);
    }

    [Fact]
    public void TheVersionOfAnAssemblyPrefersItsInformationalVersion()
    {
        var assembly = typeof(BuildInfoTests).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.Equal(informational ?? assembly.GetName().Version?.ToString(), BuildInfo.VersionOf(assembly));
    }
}
