using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed correction of an existing sales document: an invoice correction (KFS) of an FS, or a receipt return (ZW) of a
/// PA, through <c>IKorektyDokumentowSprzedazy</c> and <c>IPozycjeKorektDokumentu.SprobujKorygowacPozycjeWedlugId</c>.
/// </summary>
/// <remarks>
/// <para>
/// All-or-nothing, fails loudly: every line names a position of the corrected document and Nexo must accept every
/// line, the issue date must stay where it was set, and a refused <c>Zapisz()</c> returns its validation errors. A field
/// this path cannot apply (correction reason, unit, price) is refused with its name, never ignored.
/// </para>
/// <para>
/// A line states the quantity change (<c>quantityCorrection</c>); the quantity after the correction is the original
/// quantity plus the change and must not go below zero. ILUO keeps a correction line as the state after the correction,
/// so a full return is <c>quantityCorrection = -originalQuantity</c>.
/// </para>
/// <para>Must run on the SDK thread.</para>
/// </remarks>
public static class SalesCorrectionWriter
{
    public enum Kind
    {
        InvoiceCorrection,
        ReceiptReturn,
    }

    /// <summary>A position of the corrected document.</summary>
    public sealed record OriginalLine(int Id, decimal Quantity, string? Symbol);

    /// <summary>A position to correct and its quantity after the correction.</summary>
    public sealed record PlannedLine(int OriginalPositionId, decimal QuantityAfter);

    /// <summary>Result of <see cref="Create"/>.</summary>
    public sealed class Outcome
    {
        public bool NotFound { get; init; }
        public string Message { get; init; } = string.Empty;
        public List<string> Errors { get; } = new();
        public dynamic? Document { get; init; }
        public bool Success => Document != null && Errors.Count == 0;
    }

    /// <summary>Document symbols each kind corrects.</summary>
    public static string CorrectedSymbol(Kind kind) => kind == Kind.ReceiptReturn ? "PA" : "FS";

    /// <summary>
    /// Checks that need no SDK: there are lines, each names one position, and no line asks for a field this path
    /// cannot apply. Returns the errors (empty when the request may go on).
    /// </summary>
    public static List<string> CheckRequest(CreateCorrectionRequest request)
    {
        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.CorrectionReason))
        {
            errors.Add("correctionReason: not applied to a correction of an existing document yet; leave it empty");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            errors.Add("items: at least one corrected line is required");
            return errors;
        }

        var seen = new HashSet<int>();
        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            if (!item.OriginalPositionId.HasValue)
            {
                errors.Add($"items[{i}].originalPositionId is required for a correction of an existing document");
            }
            else if (!seen.Add(item.OriginalPositionId.Value))
            {
                errors.Add($"items[{i}].originalPositionId {item.OriginalPositionId} is corrected twice");
            }

            if (item.QuantityCorrection == 0)
            {
                errors.Add($"items[{i}].quantityCorrection is 0: the line changes nothing");
            }

            if (item.PriceNetCorrection.HasValue)
            {
                errors.Add($"items[{i}].priceNetCorrection: price corrections are not applied yet");
            }

            if (!string.IsNullOrWhiteSpace(item.Unit))
            {
                errors.Add($"items[{i}].unit: the unit of a corrected line cannot change here");
            }

            if (!string.IsNullOrWhiteSpace(item.ItemCorrectionReason))
            {
                errors.Add($"items[{i}].itemCorrectionReason: not applied yet; leave it empty");
            }
        }

        return errors;
    }

    /// <summary>
    /// Plans the lines against the positions of the corrected document (no SDK). A line must name a position of that
    /// document, match its product when it states one, and must not take the quantity below zero.
    /// </summary>
    public static (List<PlannedLine> Plan, List<string> Errors) Plan(IReadOnlyCollection<OriginalLine> originals,
        IReadOnlyList<CreateCorrectionItemRequest> items)
    {
        var byId = originals.ToDictionary(l => l.Id);
        var plan = new List<PlannedLine>();
        var errors = new List<string>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!item.OriginalPositionId.HasValue || !byId.TryGetValue(item.OriginalPositionId.Value, out var original))
            {
                errors.Add($"items[{i}].originalPositionId {item.OriginalPositionId} is not a position of the corrected document");
                continue;
            }

            var symbol = item.ProductSymbol?.Trim();
            if (!string.IsNullOrEmpty(symbol) && !string.Equals(symbol, original.Symbol, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"items[{i}].productSymbol {symbol} differs from position {original.Id} ({original.Symbol})");
                continue;
            }

            var after = original.Quantity + item.QuantityCorrection;
            if (after < 0)
            {
                errors.Add($"items[{i}]: position {original.Id} has {original.Quantity}, a change of {item.QuantityCorrection} goes below zero");
                continue;
            }

            plan.Add(new PlannedLine(original.Id, after));
        }

        return (plan, errors);
    }

    /// <summary>Creates and saves the correction; nothing is saved unless every step succeeded.</summary>
    public static Outcome Create(Uchwyt sfera, Kind kind, CreateCorrectionRequest request, ILogger logger)
    {
        var expected = CorrectedSymbol(kind);
        var id = request.OriginalDocumentId!.Value;
        var original = sfera.DokumentySprzedazy().Dane.Wszystkie().FirstOrDefault(d => d.Id == id);
        if (original == null)
        {
            return new Outcome { NotFound = true, Message = $"Original document with ID {id} not found" };
        }

        if (!string.Equals(original.Symbol, expected, StringComparison.OrdinalIgnoreCase))
        {
            var refused = new Outcome { Message = $"Document {id} is {original.Symbol}; this endpoint corrects {expected}" };
            refused.Errors.Add($"originalDocumentId: {original.Symbol} cannot be corrected here");
            return refused;
        }

        var originals = original.Pozycje
            .Select(p => new OriginalLine(p.Id, p.Ilosc, p.AsortymentAktualny?.Symbol))
            .ToList();
        var (plan, planErrors) = Plan(originals, request.Items);
        if (planErrors.Count > 0)
        {
            var refused = new Outcome { Message = "The correction lines do not fit the corrected document" };
            refused.Errors.AddRange(planErrors);
            return refused;
        }

        var korekty = sfera.KorektyDokumentowSprzedazy();
        using var korekta = kind == Kind.ReceiptReturn
            ? korekty.UtworzZwrotDoParagonu(original)
            : korekty.UtworzKorekteFakturySprzedazy(original);
        var dane = korekta.Dane;
        var tag = kind == Kind.ReceiptReturn ? "ZW" : "KFS";

        if (request.IssueDate.HasValue)
        {
            dane.DataWydaniaWystawienia = request.IssueDate.Value;
        }

        if (!string.IsNullOrEmpty(request.Notes))
        {
            dane.Uwagi = request.Notes;
        }

        var errors = new List<string>();
        foreach (var line in plan)
        {
            var wynik = korekta.Pozycje.SprobujKorygowacPozycjeWedlugId(line.OriginalPositionId, line.QuantityAfter, out PozycjaKorekty _);
            if (wynik == null || !wynik.Wynik)
            {
                errors.Add($"position {line.OriginalPositionId}: {wynik?.Komunikat ?? "Nexo did not correct the position"}");
            }
        }

        if (request.IssueDate.HasValue && dane.DataWydaniaWystawienia?.Date != request.IssueDate.Value.Date)
        {
            errors.Add($"issueDate: Nexo moved the issue date to {dane.DataWydaniaWystawienia:yyyy-MM-dd}");
        }

        // A KFS of a paper (non-KSeF) invoice stays paper: the SDK does not document that a correction takes the form of
        // the corrected invoice, so it is set here, guarded and read back like invoiceForm on sales-invoice.
        string? carriedForm = kind == Kind.InvoiceCorrection ? InvoiceForm.CarriedToCorrection(original.FormaFaktury) : null;
        if (carriedForm != null)
        {
            string? formError = InvoiceForm.Apply(dane, carriedForm);
            if (formError != null)
            {
                errors.Add(formError);
            }
        }

        if (errors.Count > 0)
        {
            var refused = new Outcome { Message = $"The {tag} was not saved" };
            refused.Errors.AddRange(errors);
            return refused;
        }

        if (!korekta.Zapisz())
        {
            var refused = new Outcome { Message = $"Nexo refused the {tag}" };
            refused.Errors.AddRange(SdkErrors.Of(korekta));
            if (refused.Errors.Count == 0)
            {
                refused.Errors.Add("Nexo refused the save without naming a reason");
            }

            return refused;
        }

        logger.LogInformation("[{Tag}] Created a correction of document {OriginalId}, {Lines} line(s)", tag, id, plan.Count);
        return new Outcome { Document = dane, Message = $"{tag} created successfully" };
    }
}
