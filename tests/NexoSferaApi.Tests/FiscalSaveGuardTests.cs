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
    /// <c>Zapisz()</c>, and <c>ObjectContext.SaveChanges</c> in any form (<c>SaveChangesAsync().Wait()</c>, a method
    /// group, <c>nameof</c>, a name for reflection). Production code has no legitimate use of any of them.
    /// </summary>
    private static readonly Regex[] ForbiddenSaves =
    {
        new(@"\bZapiszBezWalidacji\b", RegexOptions.CultureInvariant),
        new(@"\bZapiszZatwierdzone\b", RegexOptions.CultureInvariant),
        new(@"\.\s*Zatwierdz\s*\(", RegexOptions.CultureInvariant),
        new(@"\bSaveChanges\w*\b", RegexOptions.CultureInvariant),
    };

    /// <summary>Text of a save exception (any variable named save…Ex / save…Exception / save…Error) and any <c>?.Message</c>.</summary>
    private static readonly Regex[] Leaks =
    {
        new(@"(?i)\bsave\w*(?:ex|exception|error)\b\s*(?:\?\.|\.)\s*(?:Message|ToString\s*\()", RegexOptions.CultureInvariant),
        new(@"\?\.Message\b", RegexOptions.CultureInvariant),
    };

    /// <summary>
    /// The FS, PA and RW write paths, REST and MCP. In these a save exception is found by its place, not by its name:
    /// see <see cref="SaveCatchLeaks"/>.
    /// </summary>
    private static readonly (string File, string Method)[] SavePaths =
    {
        ("Controllers/DocumentsController.cs", "CreateSalesInvoice"),
        ("Controllers/DocumentsController.cs", "CreateReceipt"),
        ("Controllers/WarehouseDocumentsController.cs", "CreateRW"),
        ("Controllers/WarehouseDocumentsController.cs", "CreateRWFromOrder"),
        ("Mcp/Tools/DocumentTools.cs", "CreateSalesInvoice"),
        ("Mcp/Tools/DocumentTools.cs", "CreateReceipt"),
    };

    private static readonly Regex LogStatement = new(@"\b_?logger\.", RegexOptions.CultureInvariant);
    private static readonly Regex Try = new(@"\btry\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex SaveCall = new(@"\.\s*Zapisz\s*\(", RegexOptions.CultureInvariant);
    private static readonly Regex Throw = new(@"\bthrow\b", RegexOptions.CultureInvariant);

    private static readonly Regex Catch = new(
        @"\G\s*catch\b\s*(?:\(\s*[\w.<>?]+(?:\s+(?<name>\w+))?\s*\))?\s*(?:when\s*\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\))?\s*\{",
        RegexOptions.CultureInvariant);

    [Fact]
    public void NoWritePathSavesPastTheValidation()
    {
        var offenders = ProductionSources()
            .SelectMany(CodeLines)
            .Where(entry => ForbiddenSaves.Any(pattern => pattern.IsMatch(entry.line)))
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
    public void AnFsPaOrRwSaveExceptionStaysInTheLogWhateverItsName()
    {
        var source = FindSourceRoot();
        var offenders = SavePaths
            .SelectMany(path => SaveCatchLeaks(File.ReadAllText(Path.Combine(source, path.File)), path.Method)
                .Select(offender => $"{path.File} {path.Method}: {offender}"))
            .ToList();

        Assert.True(offenders.Count == 0, "Save exception outside the log:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void TheGuardsSeeTheMutationsReviewMade()
    {
        string[] mutants =
        {
            "faktura.Zatwierdz(); var retry = faktura.Zapisz();",
            "((dynamic)faktura.Dokument.ObjectContext).SaveChanges();",
            "var save = context.GetType().GetMethod(\"SaveChanges\", Type.EmptyTypes);",
            "((dynamic)faktura.Dokument.ObjectContext).SaveChangesAsync().Wait();",
            "Func<int> save = context.SaveChanges;",
            "var save = context.GetType().GetMethod(nameof(ObjectContext.SaveChanges));",
        };
        Assert.All(mutants, mutant => Assert.Contains(ForbiddenSaves, pattern => pattern.IsMatch(mutant)));
        Assert.DoesNotContain(ForbiddenSaves, pattern => pattern.IsMatch("saveResult = faktura.Zapisz(); // ChangesSavedCompleted"));

        string[] leaks =
        {
            "return (false, null, $\"Receipt save exception: {saveException.Message}\", errors);",
            "errors.Add(saveException?.Message ?? \"\");",
            "return (false, null, \"RW failed\", new List<string> { saveEx.ToString() });",
            "errors.Add(ex?.Message);",
        };
        Assert.All(leaks, leak => Assert.Contains(Leaks, pattern => pattern.IsMatch(leak)));
        Assert.DoesNotContain(Leaks, pattern => pattern.IsMatch("string docNumber = faktura.PodajPodgladNumeru()?.ToString() ?? \"\";"));

        // The FS save catch names its exception "ex": the name rule cannot tell it from any other catch.
        string[] saveCatchMutants =
        {
            "detailErrors.Add(ex.Message);",
            "detailErrors.Add($\"Zapisz() threw: {ex}\");",
            "detailErrors.Add(ex.InnerException?.ToString() ?? \"\");",
            "var reason = ex.GetBaseException(); detailErrors.Add(reason.Message);",
            "failure = ex;",
            "throw;",
            "throw new InvalidOperationException(\"save failed\", ex);",
        };
        Assert.Empty(SaveCatchLeaks(WritePath(string.Empty), "CreateSalesInvoice"));
        Assert.All(saveCatchMutants, mutant => Assert.NotEmpty(SaveCatchLeaks(WritePath(mutant), "CreateSalesInvoice")));
        Assert.NotEmpty(SaveCatchLeaks(WritePath(string.Empty).Replace("try\n", "if (true)\n").Replace("catch (Exception ex)\n", "if (false)\n"), "CreateSalesInvoice"));
    }

    [Fact]
    public void AWritePathThatMovedFailsTheGuard()
    {
        // A renamed or split method must update SavePaths, never drop out of the check.
        Assert.ThrowsAny<Exception>(() => SaveCatchLeaks(WritePath(string.Empty), "CreateReceipt"));
        Assert.ThrowsAny<Exception>(() => SaveCatchLeaks(WritePath(string.Empty).Replace("faktura.Zapisz()", "faktura.Przelicz()"), "CreateSalesInvoice"));
    }

    /// <summary>
    /// A write path shaped like the FS one, with <paramref name="mutant"/> in the save catch. The response reads
    /// <c>failure</c>, so a copy of the exception to it shows.
    /// </summary>
    private static string WritePath(string mutant) => $$"""
        public async Task<ActionResult> CreateSalesInvoice([FromBody] CreateDocumentRequest request)
        {
            try
            {
                var result = await _sferaService.ExecuteWithLockAsync(() =>
                {
                    var detailErrors = new List<string>();
                    Exception? failure = null;
                    object? saveResult = null;
                    try
                    {
                        saveResult = faktura.Zapisz();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[FS-v2] Zapisz() threw exception: {Msg}", ex.Message);
                        var inner = ex.InnerException;
                        while (inner != null)
                        {
                            _logger.LogError("[FS-v2] Inner exception: {Msg}", (object)inner.Message);
                            inner = inner.InnerException;
                        }
                        {{mutant}}
                    }

                    if (failure != null)
                    {
                        detailErrors.Add("failed: " + failure.Message);
                    }

                    return (saveResult != null, detailErrors);
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sales invoice");
                return StatusCode(500, ex.Message);
            }
        }
        """.Replace("\r\n", "\n");

    /// <summary>
    /// Offending lines of one write path: every <c>Zapisz()</c> in <paramref name="method"/> sits in a <c>try</c>, and
    /// the catch variables of the innermost such <c>try</c>, whatever their names, reach only log statements.
    /// </summary>
    /// <remarks>
    /// A copy of the exception (<c>saveException = ex</c>, <c>var inner = ex.InnerException</c>) is followed: one
    /// declared in the catch within the catch, another to the end of the block holding the <c>try</c>. Outside a log
    /// statement, its <c>Message</c>, <c>StackTrace</c>, <c>ToString()</c> or the variable in an interpolation is a
    /// leak. So is a <c>throw</c> in the catch: it hands the exception to an outer catch, and those return
    /// <c>ex.Message</c>. An exception passed to a helper method is not followed.
    /// </remarks>
    private static List<string> SaveCatchLeaks(string text, string method)
    {
        var code = CodeOnly(text);
        var lines = text.Split('\n');
        var codeLines = code.Split('\n');
        var inLog = CodeLines(string.Empty, lines).ToDictionary(entry => entry.number, entry => entry.inLog);
        var (bodyStart, bodyEnd) = MethodBody(code, method);
        var saves = SaveCall.Matches(code, bodyStart).Where(match => match.Index < bodyEnd).Select(match => match.Index).ToList();
        Assert.True(saves.Count > 0, $"{method} has no Zapisz() any more: update {nameof(SavePaths)}");

        var tries = Try.Matches(code, bodyStart)
            .Where(match => match.Index < bodyEnd)
            .Select(match => match.Index + match.Length - 1)
            .Select(open => (open, close: MatchingBrace(code, open)))
            .ToList();

        var offenders = new List<string>();
        foreach (var save in saves)
        {
            var enclosing = tries.Where(block => block.open < save && save < block.close).ToList();
            if (enclosing.Count == 0)
            {
                offenders.Add($"line {LineOf(code, save)}: Zapisz() outside a try");
                continue;
            }

            var tryClose = enclosing.MaxBy(block => block.open).close;
            var blockLast = LineOf(code, MatchingBrace(code, EnclosingBlock(code, tryClose)));
            for (var position = tryClose + 1; Catch.Match(code, position) is { Success: true } clause;)
            {
                var catchClose = MatchingBrace(code, clause.Index + clause.Length - 1);
                var (first, last) = (LineOf(code, clause.Index + clause.Length - 1), LineOf(code, catchClose));
                for (var number = first; number <= last; number++)
                {
                    if (Throw.IsMatch(codeLines[number - 1]))
                    {
                        offenders.Add($"line {number}: throw in a save catch: {lines[number - 1].Trim()}");
                    }
                }

                if (clause.Groups["name"].Success)
                {
                    var (inCatch, inBlock) = Copies(clause.Groups["name"].Value, lines, first, last, blockLast);
                    var blockText = inBlock.Select(ExceptionText).ToList();
                    var catchText = inCatch.Select(ExceptionText).Concat(blockText).ToList();
                    for (var number = first; number <= blockLast; number++)
                    {
                        if (!inLog.GetValueOrDefault(number) && codeLines[number - 1].Trim().Length > 0
                            && (number <= last ? catchText : blockText).Any(pattern => pattern.IsMatch(lines[number - 1])))
                        {
                            offenders.Add($"line {number}: {lines[number - 1].Trim()}");
                        }
                    }
                }

                position = catchClose + 1;
            }
        }

        return offenders.Distinct().ToList();
    }

    /// <summary>
    /// The catch variable and its copies: those declared in the catch (lines <paramref name="first"/> to
    /// <paramref name="last"/>) live there, the others to <paramref name="blockLast"/>.
    /// </summary>
    private static (HashSet<string> inCatch, HashSet<string> inBlock) Copies(string name, string[] lines, int first, int last, int blockLast)
    {
        var inCatch = new HashSet<string> { name };
        var inBlock = new HashSet<string>();
        for (var grown = true; grown;)
        {
            grown = false;
            for (var number = first; number <= blockLast; number++)
            {
                foreach (var source in (number <= last ? inCatch.Concat(inBlock) : inBlock).ToList())
                {
                    var copy = Regex.Match(lines[number - 1], $@"(?<declared>(?:\bvar|[\w>?\]])\s+)?\b(?<target>\w+)\s*=\s*{source}\b(?:\s*\??\.\s*\w+(?:\s*\(\s*\))?)*\s*;");
                    var target = copy.Groups["target"].Value;
                    if (copy.Success && !inCatch.Contains(target) && !inBlock.Contains(target))
                    {
                        (copy.Groups["declared"].Success && number <= last ? inCatch : inBlock).Add(target);
                        grown = true;
                    }
                }
            }
        }

        return (inCatch, inBlock);
    }

    /// <summary>The text of an exception variable: Message, StackTrace or ToString() at the end of a member chain, or an interpolation.</summary>
    private static Regex ExceptionText(string name) => new(
        $@"\b{name}\b(?:\s*\??\.\s*\w+(?:\s*\(\s*\))?)*?\s*\??\.\s*(?:Message|StackTrace|ToString\s*\()|\{{{name}[}},:]",
        RegexOptions.CultureInvariant);

    /// <summary>Opening and closing brace of the one method declared with this name.</summary>
    private static (int start, int end) MethodBody(string code, string method)
    {
        var declarations = Regex.Matches(code, $@"(?m)^[ \t]*(?:(?:public|private|protected|internal|static|async|override)\s+)+[^;=(\n]*\b{method}\s*\(");
        Assert.True(declarations.Count == 1, $"Expected one declaration of {method}, found {declarations.Count}: update {nameof(SavePaths)}");

        var open = code.IndexOf('{', MatchingParenthesis(code, declarations[0].Index + declarations[0].Length - 1));
        return (open, MatchingBrace(code, open));
    }

    private static int MatchingBrace(string code, int open) => Matching(code, open, '{', '}');

    private static int MatchingParenthesis(string code, int open) => Matching(code, open, '(', ')');

    private static int Matching(string code, int open, char opening, char closing)
    {
        var depth = 0;
        for (var index = open; index < code.Length; index++)
        {
            if (code[index] == opening)
            {
                depth++;
            }
            else if (code[index] == closing && --depth == 0)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Unbalanced '{opening}' at {open}");
    }

    /// <summary>The opening brace of the block that holds the statement ending at <paramref name="position"/>.</summary>
    private static int EnclosingBlock(string code, int position)
    {
        var depth = 0;
        for (var index = position; index >= 0; index--)
        {
            if (code[index] == '}')
            {
                depth++;
            }
            else if (code[index] == '{' && --depth < 0)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"No block holds {position}");
    }

    private static int LineOf(string code, int position) => code.AsSpan(0, position).Count('\n') + 1;

    /// <summary>
    /// The text with comments, string and character literals blanked out (same length, line breaks kept), so braces,
    /// parentheses and keywords can be counted. An interpolation hole is blanked with its string.
    /// </summary>
    private static string CodeOnly(string text)
    {
        var code = text.ToCharArray();
        for (var index = 0; index < text.Length;)
        {
            var (quote, verbatim) = StringQuote(text, index);
            int end;
            if (string.CompareOrdinal(text, index, "//", 0, 2) == 0)
            {
                end = text.IndexOf('\n', index) is var newline and >= 0 ? newline : text.Length;
            }
            else if (string.CompareOrdinal(text, index, "/*", 0, 2) == 0)
            {
                end = text.IndexOf("*/", index + 2, StringComparison.Ordinal) is var close and >= 0 ? close + 2 : text.Length;
            }
            else if (text[index] == '\'')
            {
                end = LiteralEnd(text, index, '\'', verbatim: false);
            }
            else if (quote >= 0 && string.CompareOrdinal(text, quote, "\"\"\"", 0, 3) == 0)
            {
                end = text.IndexOf("\"\"\"", quote + 3, StringComparison.Ordinal) is var raw and >= 0 ? raw + 3 : text.Length;
            }
            else if (quote >= 0)
            {
                end = LiteralEnd(text, quote, '"', verbatim);
            }
            else
            {
                index++;
                continue;
            }

            for (var blank = index; blank < end; blank++)
            {
                if (code[blank] != '\n' && code[blank] != '\r')
                {
                    code[blank] = ' ';
                }
            }

            index = end;
        }

        return new string(code);
    }

    /// <summary>The opening quote of a string literal starting at <paramref name="index"/> ("…", $"…", @"…", $@"…"), or -1.</summary>
    private static (int quote, bool verbatim) StringQuote(string text, int index)
    {
        var quote = index;
        var verbatim = false;
        while (quote < text.Length && (text[quote] == '$' || text[quote] == '@'))
        {
            verbatim |= text[quote] == '@';
            quote++;
        }

        return quote < text.Length && text[quote] == '"' ? (quote, verbatim) : (-1, false);
    }

    private static int LiteralEnd(string text, int quote, char delimiter, bool verbatim)
    {
        for (var index = quote + 1; index < text.Length; index++)
        {
            if (verbatim && text[index] == '"' && index + 1 < text.Length && text[index + 1] == '"')
            {
                index++;
            }
            else if (!verbatim && text[index] == '\\')
            {
                index++;
            }
            else if (text[index] == delimiter || (!verbatim && text[index] == '\n'))
            {
                return index + 1;
            }
        }

        return text.Length;
    }

    /// <summary>
    /// Code lines with their file and number; comment lines are left out, and <c>inLog</c> marks every line of a
    /// <c>_logger.</c> / <c>logger.</c> statement, also its continuation lines.
    /// </summary>
    private static IEnumerable<(string file, string line, int number, bool inLog)> CodeLines(string file) =>
        CodeLines(file, File.ReadLines(file));

    private static IEnumerable<(string file, string line, int number, bool inLog)> CodeLines(string file, IEnumerable<string> lines)
    {
        var inLog = false;
        var number = 0;
        foreach (var line in lines)
        {
            number++;
            var trimmed = line.Trim();
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            var logLine = inLog || LogStatement.IsMatch(trimmed);
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
