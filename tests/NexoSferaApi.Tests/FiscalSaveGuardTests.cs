using System.Text.RegularExpressions;
using Xunit;

namespace NexoSferaApi.Tests;

/// <summary>
/// A refused <c>Zapisz()</c> of a fiscal document is final: the bridge returns the validation errors and never saves the
/// document another way. A document saved past its validation (a number Nexo refused, no buyer, broken totals) is worse
/// than none, so the fallbacks removed in 2026-11 must not come back.
/// </summary>
public sealed class FiscalSaveGuardTests
{
    private static readonly string[] ForbiddenSaves =
    {
        "ZapiszBezWalidacji",
        "ZapiszZatwierdzone",
        "\"SaveChanges\"",
    };

    [Fact]
    public void NoWritePathSavesPastTheValidation()
    {
        var offenders = ProductionSources()
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, number: index + 1)))
            .Where(entry => !entry.line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Where(entry => ForbiddenSaves.Any(pattern => entry.line.Contains(pattern, StringComparison.Ordinal)))
            .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.number}: {entry.line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0, "Save without validation:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void ASaveExceptionIsNotSentToTheClient()
    {
        var leak = new Regex(@"\$""[^""]*\{saveException\.Message\}|new List<string> \{ saveException\.Message \}");
        var offenders = ProductionSources()
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, number: index + 1)))
            .Where(entry => leak.IsMatch(entry.line))
            .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.number}: {entry.line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0, "SDK exception text in a response:\n" + string.Join("\n", offenders));
    }

    /// <summary>Controllers and helpers; the development-only diagnostics controller is out of scope.</summary>
    private static IEnumerable<string> ProductionSources()
    {
        var source = FindSourceRoot();
        var files = Directory.EnumerateFiles(Path.Combine(source, "Controllers"), "*.cs")
            .Concat(Directory.EnumerateFiles(Path.Combine(source, "Helpers"), "*.cs"))
            .Where(file => !Path.GetFileName(file).Equals("DiagnosticsController.cs", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(files);
        return files;
    }

    private static string FindSourceRoot()
    {
        // NEXO_SOURCE_ROOT lets the guard run from a build outside the repository layout.
        var configured = Environment.GetEnvironmentVariable("NEXO_SOURCE_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "NexoSferaApi.csproj")))
            {
                return Path.Combine(current.FullName, "src");
            }
        }

        throw new DirectoryNotFoundException("Could not find the NexoSferaApi source directory.");
    }
}
