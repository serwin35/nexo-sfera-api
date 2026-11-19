using System.Globalization;
using System.Text.RegularExpressions;
using InsERT.Moria.Numeracja;

namespace NexoSferaApi.Helpers;

/// <summary>
/// The number a client states outright for a new sales invoice or receipt (<c>documentNumber</c>): typed Nexo manual
/// numbering through <see cref="IObiektNumerowany"/>, which <c>DokumentSprzedazyBO</c> implements publicly.
/// </summary>
/// <remarks>
/// <para>
/// Historical imports (ILUO → Nexo) need the number the customer got. Without it a document missing below a number
/// Nexo already has (FS 24 when the series has FS 28) can only take the next free number.
/// </para>
/// <para>
/// Fails loudly, never silently: the series must allow manual numbers (<see cref="IZrodloNumerow.MoznaNadacNumerRecznie"/>),
/// <see cref="IObiektNumerowany.ZarezerwujNumer(int)"/> must not throw or warn, and the preview must show the requested
/// number. A number that is reserved but not saved is given back (<see cref="IObiektNumerowany.ZwolnijNumer"/>) when the
/// reservation is disposed, so a refused save leaves no hole and the number can be tried again.
/// </para>
/// <para>
/// Usage: create one per document (<c>using var</c>) after the business object, call <see cref="Reserve"/> after the
/// dates are set (the series follows the date), and <see cref="MarkSaved"/> once <c>Zapisz()</c> succeeded. Must run on the
/// SDK thread.
/// </para>
/// </remarks>
public sealed class ExplicitDocumentNumber : IDisposable
{
    /// <summary>Request field name, used in error messages.</summary>
    public const string Field = "documentNumber";

    /// <summary>Endpoints (relative to <c>/api/documents</c>) that apply <see cref="Field"/>.</summary>
    public static readonly IReadOnlyList<string> SupportedEndpoints = new[] { "sales-invoice", "receipt" };

    /// <summary>Error for an endpoint that cannot apply <see cref="Field"/>.</summary>
    public static readonly string Unsupported =
        $"{Field} is supported only by POST /api/documents/{string.Join(" and POST /api/documents/", SupportedEndpoints)}";

    /// <summary>Error for a stated number without <c>issueDate</c>.</summary>
    public const string NeedsIssueDate = Field + " needs issueDate: the series of the number follows the issue date";

    private readonly ILogger _logger;
    private readonly string _tag;
    private IObiektNumerowany? _document;
    private bool _saved;

    public ExplicitDocumentNumber(ILogger logger, string tag)
    {
        _logger = logger;
        _tag = tag;
    }

    /// <summary>The requested number once it is reserved; null before (or without) a stated number.</summary>
    public int? Number { get; private set; }

    /// <summary>
    /// True once a stated number is reserved. The save path must then refuse instead of trying saves without
    /// validation, which could keep a number Nexo refused.
    /// </summary>
    public bool IsReserved => _document != null;

    /// <summary>
    /// Reserves <paramref name="number"/> in the series of <paramref name="document"/>.
    /// Returns null when the number is reserved, or the error for the client (nothing is reserved then).
    /// With <paramref name="issueDate"/>, the period in the reserved number (year and month) must be the issue date's.
    /// </summary>
    public string? Reserve(object document, int number, DateTime? issueDate = null)
    {
        if (_document != null)
        {
            throw new InvalidOperationException("A number is already reserved for this document.");
        }

        if (number < 1)
        {
            return $"{Field} must be 1 or more, got {number}";
        }

        if (document is not IObiektNumerowany numbered)
        {
            _logger.LogWarning("[{Tag}] {Type} does not implement IObiektNumerowany", _tag, document?.GetType().FullName ?? "(null)");
            return $"{Field}: this document type cannot take a stated number";
        }

        IZrodloNumerow? source;
        bool manualAllowed;
        try
        {
            source = numbered.ZrodloNumerowRezerwujace;
            manualAllowed = source?.MoznaNadacNumerRecznie ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Tag}] Reading the numbering of the series failed", _tag);
            return $"{Field} {number}: the numbering of the series could not be read";
        }

        if (source == null)
        {
            return $"{Field} {number}: the document has no numbering source";
        }

        if (!manualAllowed)
        {
            return $"{Field} {number}: the numbering of this series does not allow manual numbers " +
                   "(allow manual numbering for the document type in Nexo, or leave documentNumber out)";
        }

        try
        {
            numbered.ZarezerwujNumer(number);
        }
        catch (NumeracjaException ex)
        {
            // A numbering refusal from Nexo (for example a taken number); its message is for the user.
            _logger.LogWarning(ex, "[{Tag}] Nexo refused number {Number}", _tag, number);
            TryRelease(numbered, number);
            return $"{Field} {number}: Nexo refused the number: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Tag}] Reserving number {Number} failed", _tag, number);
            TryRelease(numbered, number);
            return $"{Field} {number}: the number could not be reserved";
        }

        string? warnings = null;
        string? preview = null;
        int? reserved = null;
        try
        {
            warnings = numbered.SprawdzOstrzezeniaRezerwacji();
            var podglad = numbered.PodajPodgladNumeru();
            preview = podglad?.ToString();
            reserved = ParseNumber(podglad?.Numer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Tag}] Checking the reservation of number {Number} failed", _tag, number);
            TryRelease(numbered, number);
            return $"{Field} {number}: the reserved number could not be confirmed";
        }

        if (!string.IsNullOrWhiteSpace(warnings))
        {
            _logger.LogWarning("[{Tag}] Reservation of number {Number} has warnings: {Warnings}", _tag, number, warnings);
            TryRelease(numbered, number);
            return $"{Field} {number}: Nexo reported a problem with the number: {warnings.Trim()}";
        }

        if (reserved != number)
        {
            _logger.LogError("[{Tag}] Requested number {Number}, Nexo reserved {Preview}", _tag, number, preview ?? "(none)");
            TryRelease(numbered, number);
            return $"{Field} {number}: Nexo reserved {preview ?? "no number"} instead";
        }

        var period = issueDate.HasValue ? PeriodMismatch(preview, issueDate.Value) : null;
        if (period != null)
        {
            _logger.LogError("[{Tag}] Number {Preview} is not in the period of {IssueDate:yyyy-MM-dd}", _tag, preview, issueDate);
            TryRelease(numbered, number);
            return $"{Field} {number}: {period}";
        }

        _document = numbered;
        Number = number;
        _logger.LogInformation("[{Tag}] Reserved stated number {Number}: {Preview}", _tag, number, preview);
        return null;
    }

    /// <summary>
    /// Marks the document as saved, so the number stays. Logs an error when the saved document shows a different number;
    /// the document is saved by then, so the client must check the number in the response.
    /// </summary>
    public void MarkSaved()
    {
        if (_document == null)
        {
            return;
        }

        _saved = true;
        try
        {
            var podglad = _document.PodajPodgladNumeru();
            if (ParseNumber(podglad?.Numer) != Number)
            {
                _logger.LogError("[{Tag}] Saved with {Saved}, requested number {Number}", _tag, podglad?.ToString() ?? "(none)", Number);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Tag}] Reading the saved number failed", _tag);
        }
    }

    /// <summary>Gives the number back when it was reserved here and the document was not saved.</summary>
    public void Dispose()
    {
        if (_document != null && !_saved)
        {
            TryRelease(_document, Number);
        }

        _document = null;
    }

    private static readonly Regex YearMonth = new(@"(?<!\d)(?<year>\d{4})/(?<month>\d{1,2})(?!\d)", RegexOptions.CultureInvariant);
    private static readonly Regex MonthYear = new(@"(?<!\d)(?<month>\d{1,2})/(?<year>\d{4})(?!\d)", RegexOptions.CultureInvariant);
    private static readonly Regex ShortYearMonth = new(@"(?<!\d)(?<year>\d{2})/(?<month>\d{1,2})(?!\d)", RegexOptions.CultureInvariant);
    private static readonly Regex YearOnly = new(@"(?<!\d)(?<year>(19|20)\d{2})(?!\d)", RegexOptions.CultureInvariant);

    /// <summary>
    /// The error when the period in a number preview ("FS 2024/06/24", "FS 24/06/2024", "FS 24/06/7") is not the one
    /// of <paramref name="issueDate"/>; null when it matches or the preview shows no period.
    /// </summary>
    /// <remarks>
    /// Looked up in order: yyyy/MM, MM/yyyy, yy/MM, then a lone year. A month outside 1-12 does not count as a period.
    /// </remarks>
    public static string? PeriodMismatch(string? preview, DateTime issueDate)
    {
        if (string.IsNullOrWhiteSpace(preview))
        {
            return null;
        }

        foreach (var pattern in new[] { YearMonth, MonthYear, ShortYearMonth })
        {
            foreach (Match match in pattern.Matches(preview))
            {
                var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
                var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
                if (month is < 1 or > 12)
                {
                    continue;
                }

                if (year < 100)
                {
                    year += 2000;
                }

                return year == issueDate.Year && month == issueDate.Month
                    ? null
                    : $"the number {preview.Trim()} belongs to {year:D4}-{month:D2}, the issue date to {issueDate:yyyy-MM}";
            }
        }

        var lone = YearOnly.Match(preview);
        if (lone.Success && int.Parse(lone.Groups["year"].Value, CultureInfo.InvariantCulture) != issueDate.Year)
        {
            return $"the number {preview.Trim()} belongs to {lone.Groups["year"].Value}, the issue date to {issueDate:yyyy}";
        }

        return null;
    }

    /// <summary>Digits of <see cref="PodgladNumeru.Numer"/> as a number ("00024" → 24); null when it has none.</summary>
    public static int? ParseNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private void TryRelease(IObiektNumerowany document, int? number)
    {
        try
        {
            document.ZwolnijNumer();
            _logger.LogInformation("[{Tag}] Released number {Number}", _tag, number);
        }
        catch (Exception ex)
        {
            // A reservation that stays can block the number; the log is the only trace of it.
            _logger.LogError(ex, "[{Tag}] Releasing number {Number} failed", _tag, number);
        }
    }
}
