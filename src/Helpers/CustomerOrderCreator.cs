using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed write path of <c>POST /api/customer-orders</c>: creates a ZK through
/// <c>IZamowieniaOdKlientow.UtworzZamowienieOdKlienta()</c>, adds the lines through <c>IPozycjeDokumentu</c> and sets the
/// line prices on <c>PozycjaDokumentu.Cena</c>.
/// </summary>
/// <remarks>
/// <para>
/// The late-bound version set <c>Dokument.DataWystawienia</c>, which does not exist, so every request with
/// <c>issueDate</c> failed with 500. It set line prices on <c>CenaNetto</c> / <c>CenaBrutto</c> inside <c>try/catch</c>,
/// so the prices were dropped. It called <c>DodajUslugeJednorazowa(name, price, quantity)</c>, whose parameters are
/// (name, quantity, unit symbol), so manual lines were dropped. Unknown products were skipped without an error.
/// </para>
/// <para>
/// All-or-nothing: the customer, the recipient, the warehouse and every product are resolved (one SQL query each) before
/// the document is created. Any problem returns errors and nothing is created; a refused <c>Zapisz()</c> discards the
/// document. Must run on the SDK thread.
/// </para>
/// </remarks>
public static class CustomerOrderCreator
{
    /// <summary>Most lines in one request.</summary>
    public const int MaxItems = 500;

    /// <summary>Result of <see cref="Create"/>. <see cref="OrderId"/> is set only when the ZK was saved.</summary>
    public sealed class Outcome<TDto>
    {
        public int? OrderId { get; set; }
        public TDto? Order { get; set; }
        public List<string> Errors { get; } = new();
    }

    /// <summary>Request validation that needs no SDK access. One message per problem, prefixed with the JSON path.</summary>
    public static List<string> Validate(CreateCustomerOrderRequest? request)
    {
        var errors = new List<string>();
        if (request == null)
        {
            errors.Add("body: the request is required.");
            return errors;
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            errors.Add("items: at least one item is required.");
            return errors;
        }

        if (request.Items.Count > MaxItems)
        {
            errors.Add($"items: at most {MaxItems} items per request, got {request.Items.Count}.");
        }

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            if (item == null)
            {
                errors.Add($"items[{i}]: the item is empty.");
                continue;
            }

            if (item.ProductId == null && string.IsNullOrWhiteSpace(item.ProductSymbol) && string.IsNullOrWhiteSpace(item.Name))
            {
                errors.Add($"items[{i}]: one of productId, productSymbol or name is required.");
            }

            if (item.ProductId != null && !string.IsNullOrWhiteSpace(item.ProductSymbol))
            {
                errors.Add($"items[{i}]: send productId or productSymbol, not both.");
            }

            if (item.Quantity <= 0)
            {
                errors.Add($"items[{i}].quantity: must be greater than 0.");
            }

            if (item.PriceNet.HasValue && item.PriceGross.HasValue)
            {
                errors.Add($"items[{i}]: send priceNet or priceGross, not both.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Creates and saves the ZK. <paramref name="saveErrors"/> reads the errors of a refused save and
    /// <paramref name="map"/> maps the saved document (both are the controller's helpers).
    /// </summary>
    public static Outcome<TDto> Create<TDto>(Uchwyt sfera, CreateCustomerOrderRequest request,
        Func<object, List<string>> saveErrors, Func<object, TDto> map)
    {
        var outcome = new Outcome<TDto>();
        var errors = outcome.Errors;

        var customer = ResolveCustomer(sfera, request, errors);
        Podmiot? recipient = null;
        if (request.RecipientId is int recipientId)
        {
            recipient = sfera.Podmioty().Dane.Wszystkie().FirstOrDefault(p => p.Id == recipientId);
            if (recipient == null) errors.Add($"recipientId: contractor {recipientId} not found.");
        }

        var warehouse = ResolveWarehouse(sfera, request, errors);
        var products = ResolveProducts(sfera, request.Items, errors);
        if (errors.Count > 0) return outcome;

        using var zk = sfera.ZamowieniaOdKlientow().UtworzZamowienieOdKlienta();
        var dane = zk.Dane;

        if (customer != null) dane.Podmiot = customer;
        if (recipient != null) dane.Odbiorca = recipient;
        if (warehouse != null) dane.Magazyn = warehouse;
        if (request.IssueDate is DateTime issueDate) dane.DataWydaniaWystawienia = issueDate;
        if (request.DeadlineDate is DateTime deadlineDate) dane.TerminRealizacji = deadlineDate;
        if (!string.IsNullOrEmpty(request.ExternalNumber)) dane.NumerZewnetrzny = request.ExternalNumber;
        if (!string.IsNullOrEmpty(request.ReferenceNumber)) dane.NumerReferencyjny = request.ReferenceNumber;
        if (!string.IsNullOrEmpty(request.Title)) dane.Tytul = request.Title;
        if (!string.IsNullOrEmpty(request.Notes)) dane.Uwagi = request.Notes;
        ApplyLegacyOptions(zk, sfera, request);

        var fromNet = (int)dane.WyliczenieVAT == (int)WyliczeniaVAT.Netto;
        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            PozycjaDokumentu? line;
            if (products.TryGetValue(i, out var product))
            {
                var unit = product.JednostkaSprzedazy ?? product.PodstawowaJednostkaMiaryAsortymentu;
                line = zk.Pozycje.Dodaj(product, item.Quantity, unit);
            }
            else
            {
                // A one-off service with the unit of the service template; the parameters are (name, quantity).
                line = zk.Pozycje.DodajUslugeJednorazowa(item.Name!.Trim(), item.Quantity);
            }

            if (line == null)
            {
                errors.Add($"items[{i}]: Nexo did not add the line.");
                continue;
            }

            if (!string.IsNullOrEmpty(item.Description)) line.Opis = item.Description;
            ApplyPrice(line, item, fromNet, i, errors);
        }

        // Not saved on any error: disposing the business object discards the document.
        if (errors.Count > 0) return outcome;

        if (!zk.Zapisz())
        {
            errors.AddRange(saveErrors(zk));
            if (errors.Count == 0) errors.Add("Nexo refused to save the order.");
            return outcome;
        }

        outcome.OrderId = zk.Dane.Id;
        outcome.Order = map(zk.Dane);
        return outcome;
    }

    /// <summary>
    /// Sets the price before discount in the calculation mode of the document (<c>WyliczenieVAT</c>): a price of the
    /// other kind is converted with the VAT rate of the line, rounded to 0.01. Without a price the Nexo price list price stays.
    /// </summary>
    private static void ApplyPrice(PozycjaDokumentu line, CreateCustomerOrderItemRequest item, bool fromNet, int index, List<string> errors)
    {
        if (item.PriceNet.HasValue || item.PriceGross.HasValue)
        {
            var givenNet = item.PriceNet.HasValue;
            var price = item.PriceNet ?? item.PriceGross!.Value;
            if (givenNet != fromNet)
            {
                var rate = line.StawkaVat?.Stawka;
                if (rate == null)
                {
                    errors.Add($"items[{index}]: the line has no VAT rate, so the {(givenNet ? "net" : "gross")} price cannot be converted; " +
                               $"send {(fromNet ? "priceNet" : "priceGross")} (this order is calculated from {(fromNet ? "net" : "gross")}).");
                    return;
                }

                price = givenNet
                    ? Math.Round(price * (1 + rate.Value), 2, MidpointRounding.AwayFromZero)
                    : Math.Round(price / (1 + rate.Value), 2, MidpointRounding.AwayFromZero);
            }

            if (fromNet) line.Cena.NettoPrzedRabatem = price;
            else line.Cena.BruttoPrzedRabatem = price;
        }

        // The request is in percent (0-100); Nexo keeps the discount as a fraction (0.0861 = 8.61 %, read on a live ZD line).
        if (item.DiscountPercent is decimal discount) line.Cena.RabatProcent = discount / 100m;
    }

    /// <summary>The customer by id, or the single contractor with the NIP (raw or without separators).</summary>
    private static Podmiot? ResolveCustomer(Uchwyt sfera, CreateCustomerOrderRequest request, List<string> errors)
    {
        var podmioty = sfera.Podmioty().Dane.Wszystkie();
        if (request.CustomerId is int customerId)
        {
            var byId = podmioty.FirstOrDefault(p => p.Id == customerId);
            if (byId == null) errors.Add($"customerId: contractor {customerId} not found.");
            return byId;
        }

        if (string.IsNullOrWhiteSpace(request.CustomerNIP)) return null;

        var raw = request.CustomerNIP.Trim();
        var compact = new string(raw.Where(char.IsLetterOrDigit).ToArray());
        var matches = podmioty.Where(p => p.NIP == raw || p.NIP == compact).Take(3).ToList();
        if (matches.Count == 1) return matches[0];

        errors.Add(matches.Count == 0
            ? $"customerNIP: no contractor with NIP {raw}."
            : $"customerNIP: NIP {raw} belongs to several contractors ({string.Join(", ", matches.Select(p => p.Id))}); send customerId.");
        return null;
    }

    private static Magazyn? ResolveWarehouse(Uchwyt sfera, CreateCustomerOrderRequest request, List<string> errors)
    {
        var magazyny = sfera.Magazyny().Dane.Wszystkie();
        if (request.WarehouseId is int warehouseId)
        {
            var byId = magazyny.FirstOrDefault(m => m.Id == warehouseId);
            if (byId == null) errors.Add($"warehouseId: warehouse {warehouseId} not found.");
            return byId;
        }

        if (string.IsNullOrWhiteSpace(request.WarehouseSymbol)) return null;

        var symbol = request.WarehouseSymbol.Trim();
        var bySymbol = magazyny.FirstOrDefault(m => m.Symbol == symbol);
        if (bySymbol == null) errors.Add($"warehouseSymbol: warehouse {symbol} not found.");
        return bySymbol;
    }

    /// <summary>Products of the lines by item index (one SQL query for ids, one for symbols). Lines without a product are absent.</summary>
    private static Dictionary<int, Asortyment> ResolveProducts(Uchwyt sfera, List<CreateCustomerOrderItemRequest> items, List<string> errors)
    {
        var ids = items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).Distinct().ToList();
        var symbols = items.Where(i => i.ProductId == null && !string.IsNullOrWhiteSpace(i.ProductSymbol))
            .Select(i => i.ProductSymbol!.Trim()).Distinct().ToList();

        var asortymenty = sfera.Asortymenty().Dane.Wszystkie();
        var byId = ids.Count == 0
            ? new Dictionary<int, Asortyment>()
            : asortymenty.Where(a => ids.Contains(a.Id)).ToList().ToDictionary(a => a.Id);
        var bySymbol = symbols.Count == 0
            ? new Dictionary<string, List<Asortyment>>()
            : asortymenty.Where(a => symbols.Contains(a.Symbol)).ToList()
                .GroupBy(a => a.Symbol, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var result = new Dictionary<int, Asortyment>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.ProductId is int productId)
            {
                if (byId.TryGetValue(productId, out var product)) result[i] = product;
                else errors.Add($"items[{i}].productId: product {productId} not found.");
            }
            else if (!string.IsNullOrWhiteSpace(item.ProductSymbol))
            {
                var symbol = item.ProductSymbol.Trim();
                if (!bySymbol.TryGetValue(symbol, out var found)) errors.Add($"items[{i}].productSymbol: product {symbol} not found.");
                else if (found.Count > 1) errors.Add($"items[{i}].productSymbol: {symbol} belongs to several products ({string.Join(", ", found.Select(a => a.Id))}).");
                else result[i] = found[0];
            }
        }

        return result;
    }

    /// <summary>
    /// Reservation type, split payment and price level, set late-bound exactly as before (best effort, unverified members).
    /// WolfFire does not send them.
    /// </summary>
    private static void ApplyLegacyOptions(IZamowienieOdKlienta zk, Uchwyt sfera, CreateCustomerOrderRequest request)
    {
        dynamic zamowienie = zk;
        if (request.ReservationType.HasValue)
        {
            try { zamowienie.Dane.TypRezerwacji = request.ReservationType.Value; } catch { }
        }

        if (request.SplitPayment.HasValue)
        {
            try { zamowienie.Dane.MechanizmPodzielonejPlatnosci = request.SplitPayment.Value; } catch { }
        }

        if (request.PriceLevelId.HasValue)
        {
            var poziom = DynamicPropertyHelper.FindById(sfera.Cenniki(), request.PriceLevelId.Value);
            if (poziom != null)
            {
                try { zamowienie.Dane.PoziomCen = poziom; } catch { }
            }
        }
    }
}
