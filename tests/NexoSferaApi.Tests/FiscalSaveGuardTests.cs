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
    /// <summary>
    /// Every form of a save past the validation: the SDK alternatives, <c>Zatwierdz()</c> followed by another
    /// <c>Zapisz()</c>, and <c>ObjectContext.SaveChanges</c> by reflection or <c>dynamic</c>. Production code has no
    /// legitimate use of any of them.
    /// </summary>
    private static readonly string[] ForbiddenSaves =
    {
        "ZapiszBezWalidacji",
        "ZapiszZatwierdzone",
        ".Zatwierdz(",
        "SaveChanges(",
        "\"SaveChanges\"",
    };

    /// <summary>Text of a save exception (any variable named save…Ex / save…Exception / save…Error) and any <c>?.Message</c>.</summary>
    private static readonly Regex[] Leaks =
    {
        new(@"(?i)\bsave\w*(?:ex|exception|error)\b\s*(?:\?\.|\.)\s*(?:Message|ToString\s*\()", RegexOptions.CultureInvariant),
        new(@"\?\.Message\b", RegexOptions.CultureInvariant),
    };

    [Fact]
    public void NoWritePathSavesPastTheValidation()
    {
        var offenders = ProductionSources()
            .SelectMany(CodeLines)
            .Where(entry => ForbiddenSaves.Any(pattern => entry.line.Contains(pattern, StringComparison.Ordinal)))
            .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.number}: {entry.line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0, "Save without validation:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void ASaveExceptionIsNotSentToTheClient()
    {
        // Only a log statement may carry the exception text; anything else can end up in a response.
        var offenders = ProductionSources()
            .SelectMany(CodeLines)
            .Where(entry => !entry.inLog && Leaks.Any(leak => leak.IsMatch(entry.line)))
            .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.number}: {entry.line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0, "SDK exception text in a response:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void TheGuardsSeeTheMutationsReviewMade()
    {
        string[] mutants =
        {
            "faktura.Zatwierdz(); var retry = faktura.Zapisz();",
            "((dynamic)faktura.Dokument.ObjectContext).SaveChanges();",
            "var save = context.GetType().GetMethod(\"SaveChanges\", Type.EmptyTypes);",
        };
        Assert.All(mutants, mutant => Assert.Contains(ForbiddenSaves, pattern => mutant.Contains(pattern, StringComparison.Ordinal)));

        string[] leaks =
        {
            "return (false, null, $\"Receipt save exception: {saveException.Message}\", errors);",
            "errors.Add(saveException?.Message ?? \"\");",
            "return (false, null, \"RW failed\", new List<string> { saveEx.ToString() });",
            "errors.Add(ex?.Message);",
        };
        Assert.All(leaks, leak => Assert.Contains(Leaks, pattern => pattern.IsMatch(leak)));
        Assert.DoesNotContain(Leaks, pattern => pattern.IsMatch("string docNumber = faktura.PodajPodgladNumeru()?.ToString() ?? \"\";"));
    }

    /// <summary>
    /// Code lines with their file and number; comment lines are left out, and <c>inLog</c> marks every line of a
    /// <c>_logger.</c> statement, also its continuation lines.
    /// </summary>
    private static IEnumerable<(string file, string line, int number, bool inLog)> CodeLines(string file)
    {
        var inLog = false;
        var number = 0;
        foreach (var line in File.ReadLines(file))
        {
            number++;
            var trimmed = line.Trim();
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            var logLine = inLog || trimmed.Contains("_logger.", StringComparison.Ordinal);
            yield return (file, line, number, logLine);
            inLog = logLine && !trimmed.EndsWith(";", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Controllers, helpers, services and MCP tools; the development-only diagnostics controller is out of scope.
    /// </summary>
    private static IEnumerable<string> ProductionSources()
    {
        var source = FindSourceRoot();
        var files = new[] { "Controllers", "Helpers", "Services", "Mcp" }
            .Select(folder => Path.Combine(source, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(file => !Path.GetFileName(file).Equals("DiagnosticsController.cs", StringComparison.Ordinal))
            .ToList();

        Assert.Contains(files, file => file.Contains(Path.Combine(source, "Services"), StringComparison.Ordinal));
        Assert.Contains(files, file => file.Contains(Path.Combine(source, "Mcp"), StringComparison.Ordinal));
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
