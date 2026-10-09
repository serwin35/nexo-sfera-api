using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed reader of customer order (ZK) lines: realization and reservation state, product, unit, prices and values.
/// </summary>
/// <remarks>
/// <para>
/// SDK 61.1 has no <c>IloscZrealizowana</c>/<c>Zarezerwowana</c>/<c>IloscZarezerwowana</c> on <c>PozycjaDokumentu</c>
/// (nor <c>Asortyment</c>/<c>Jednostka</c>/<c>CenaNetto</c>). The real members are:
/// </para>
/// <list type="bullet">
/// <item><c>StanRealizacjiZamowienia</c> (ZK lines only): <c>ZrealizowanaIlosc</c> in the base unit (the SDK sample computes
/// the open quantity as <c>IloscWJednostceBazowej - ZrealizowanaIlosc</c>), <c>ProcentowyStanRealizacji</c> as a fraction
/// (the samples test <c>&lt; 1m</c>), <c>DataOstatniejRealizacji</c>, <c>NumeryDokumentowRealizujacych</c>.</item>
/// <item><c>IloscDoRealizacji</c>: <c>PozostalaIlosc</c> still to realize (base unit), <c>BlokujRealizacje</c>.</item>
/// <item><c>Rezerwacja</c>: <c>Ilosc</c> reserved and <c>IloscZrealizowana</c> already consumed, both in the stock unit;
/// <c>Ilosciowa</c> = stock (true) vs delivery (false) reservation; <c>Termin</c> = expiry.</item>
/// </list>
/// <para>Must run on the SDK thread. Nothing here modifies SDK objects.</para>
/// </remarks>
public static class OrderReader
{
    /// <summary>Realization and reservation state of one order line, in the line unit and in the base unit.</summary>
    public sealed class LineState
    {
        public decimal Quantity { get; init; }
        public decimal QuantityInBaseUnit { get; init; }
        public decimal RealizedInBaseUnit { get; init; }
        public decimal RemainingInBaseUnit { get; init; }
        public decimal Realized { get; init; }
        public decimal Remaining { get; init; }
        public decimal? RealizationPercent { get; init; }
        public decimal ReservedInBaseUnit { get; init; }
        public decimal Reserved { get; init; }
        public string? ReservationKind { get; init; }
        public DateTime? ReservationExpiresAt { get; init; }
        public DateTime? LastRealizationDate { get; init; }
        public string? RealizingDocumentNumbers { get; init; }
        public bool RealizationBlocked { get; init; }
    }

    public static LineState ReadLine(PozycjaDokumentu line)
    {
        var quantity = SdkMember.Read(() => line.Ilosc, 0m);
        var quantityBase = SdkMember.Read(() => line.IloscWJednostceBazowej, 0m);
        var state = SdkMember.Read(() => line.StanRealizacjiZamowienia, null);
        var toRealize = SdkMember.Read(() => line.IloscDoRealizacji, null);
        var reservation = SdkMember.Read(() => line.Rezerwacja, null);

        var realizedBase = state == null ? 0m : SdkMember.Read(() => state.ZrealizowanaIlosc, null) ?? 0m;
        var fraction = state == null ? (decimal?)null : SdkMember.Read<decimal?>(() => state.ProcentowyStanRealizacji, null);
        var remainingBase = toRealize != null
            ? SdkMember.Read(() => toRealize.PozostalaIlosc, 0m)
            : Math.Max(0m, quantityBase - realizedBase);

        // Line units per base unit (e.g. 2 "op" = 24 "szt" → 1/12); without it fall back to the realization fraction.
        decimal? lineUnitsPerBase = quantityBase != 0m ? quantity / quantityBase : null;
        var realized = lineUnitsPerBase.HasValue ? realizedBase * lineUnitsPerBase.Value : quantity * (fraction ?? 0m);
        var remaining = lineUnitsPerBase.HasValue ? remainingBase * lineUnitsPerBase.Value : Math.Max(0m, quantity - realized);

        var reservedBase = 0m;
        string? kind = null;
        DateTime? expires = null;
        if (reservation != null)
        {
            reservedBase = Math.Max(0m, SdkMember.Read(() => reservation.Ilosc, 0m) - SdkMember.Read(() => reservation.IloscZrealizowana, 0m));
            kind = SdkMember.Read(() => reservation.Ilosciowa, false) ? "stock" : "delivery";
            expires = SdkMember.Read(() => reservation.Termin, null);
        }

        return new LineState
        {
            Quantity = quantity,
            QuantityInBaseUnit = quantityBase,
            RealizedInBaseUnit = realizedBase,
            RemainingInBaseUnit = remainingBase,
            Realized = Math.Round(realized, 6),
            Remaining = Math.Round(remaining, 6),
            RealizationPercent = fraction.HasValue ? Math.Round(fraction.Value * 100m, 2) : null,
            ReservedInBaseUnit = reservedBase,
            Reserved = Math.Round(lineUnitsPerBase.HasValue ? reservedBase * lineUnitsPerBase.Value : reservedBase, 6),
            ReservationKind = kind,
            ReservationExpiresAt = expires,
            LastRealizationDate = state == null ? null : SdkMember.Read(() => state.DataOstatniejRealizacji, null),
            RealizingDocumentNumbers = state == null ? null : SdkMember.Text(() => state.NumeryDokumentowRealizujacych),
            RealizationBlocked = toRealize != null && SdkMember.Read(() => toRealize.BlokujRealizacje, false),
        };
    }

    /// <summary>Overwrites the order line fields the dynamic mapper read from missing members.</summary>
    public static void EnrichLine(CustomerOrderItemDto dto, object entity)
    {
        if (entity is not PozycjaDokumentu line) return;

        var product = SdkMember.Read(() => line.AsortymentAktualny, null);
        var selected = SdkMember.Read(() => line.AsortymentWybrany, null);
        var unit = SdkMember.Read(() => line.JednostkaMiaryAs, null);
        var baseUnit = product == null ? null : SdkMember.Read(() => product.PodstawowaJednostkaMiaryAsortymentu, null);
        var price = SdkMember.Read(() => line.Cena, null);
        var value = SdkMember.Read(() => line.Wartosc, null);
        var vat = SdkMember.Read(() => line.StawkaVat, null);
        var state = ReadLine(line);

        var lp = SdkMember.Read(() => line.LP, 0);
        if (lp > 0) dto.LineNumber = lp;

        dto.ProductId = product == null ? null : SdkMember.Read<int?>(() => product.Id, null);
        dto.ProductSymbol = product == null ? null : SdkMember.Read(() => product.Symbol, null);
        dto.ProductName = product == null ? null : SdkMember.Read(() => product.Nazwa, null);
        dto.Name = (selected == null ? null : SdkMember.Text(() => selected.Nazwa)) ?? dto.ProductName ?? dto.Name;
        dto.Description = SdkMember.Read(() => line.Opis, null);

        dto.Quantity = state.Quantity;
        dto.Unit = ProductReader.UnitSymbol(unit) ?? dto.Unit;
        dto.UnitId = unit == null ? null : SdkMember.Read<int?>(() => unit.Id, null);
        dto.QuantityInBaseUnit = state.QuantityInBaseUnit;
        dto.BaseUnit = ProductReader.UnitSymbol(baseUnit);

        dto.QuantityRealized = state.Realized;
        dto.QuantityRemaining = state.Remaining;
        dto.RealizedQuantityInBaseUnit = state.RealizedInBaseUnit;
        dto.RemainingQuantityInBaseUnit = state.RemainingInBaseUnit;
        dto.RealizationPercent = state.RealizationPercent;
        dto.LastRealizationDate = state.LastRealizationDate;
        dto.RealizingDocumentNumbers = state.RealizingDocumentNumbers;
        dto.IsRealizationBlocked = state.RealizationBlocked;

        dto.IsReserved = state.ReservedInBaseUnit > 0m;
        dto.ReservedQuantity = state.Reserved;
        dto.ReservedQuantityInBaseUnit = state.ReservedInBaseUnit;
        dto.ReservationKind = state.ReservationKind;
        dto.ReservationExpiresAt = state.ReservationExpiresAt;

        if (price != null)
        {
            dto.PriceNet = SdkMember.Read(() => price.NettoPoRabacie, 0m);
            dto.PriceGross = SdkMember.Read(() => price.BruttoPoRabacie, 0m);
            dto.DiscountPercent = SdkMember.Read(() => price.RabatProcent, 0m);
            dto.DiscountValue = SdkMember.Read(() => price.RabatWartosc, 0m);
        }

        if (value != null)
        {
            dto.ValueNet = SdkMember.Read(() => value.NettoPoRabacie, 0m);
            dto.ValueVat = SdkMember.Read(() => value.VatPoRabacie, 0m);
            dto.ValueGross = SdkMember.Read(() => value.BruttoPoRabacie, 0m);
        }

        if (vat != null)
        {
            dto.VatRate = SdkMember.Text(() => vat.Symbol);
            var rate = SdkMember.Read<decimal?>(() => vat.Stawka, null);
            dto.VatPercent = rate.HasValue ? (rate.Value <= 1m ? rate.Value * 100m : rate.Value) : null;
        }
    }

    /// <summary>Row of an open customer order reservation, projected in SQL.</summary>
    public sealed class ReservationRow
    {
        public int LineId { get; set; }
        public int? ProductId { get; set; }
        public string? ProductSymbol { get; set; }
        public string? ProductName { get; set; }
        public string? LineWarehouseSymbol { get; set; }
        public string? OrderWarehouseSymbol { get; set; }
        public decimal Reserved { get; set; }
        public decimal Consumed { get; set; }
        public bool IsStockReservation { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? StockUnitSymbol { get; set; }
        public string? BaseUnitSymbol { get; set; }
        public int OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public DateTime? OrderDate { get; set; }
        public string? OrderStatus { get; set; }
    }

    /// <summary>
    /// Open reservations of customer order (ZK) lines — <c>Rezerwacja.Ilosc &gt; Rezerwacja.IloscZrealizowana</c> — as one
    /// SQL query (filter, count and page in SQL), skipping invalidated orders. Ordered by order date, newest first.
    /// </summary>
    public static IQueryable<ReservationRow> OpenReservations(Uchwyt sfera, int? productId, int? customerId, string? warehouseSymbol)
    {
        var orders = sfera.ZamowieniaOdKlientow().Dane.Wszystkie()
            .Where(d => d.StatusDokumentu == null || !d.StatusDokumentu.Uniewazniony);

        if (customerId.HasValue)
        {
            var id = customerId.Value;
            orders = orders.Where(d => d.PodmiotId == id);
        }

        if (!string.IsNullOrWhiteSpace(warehouseSymbol))
        {
            var symbol = warehouseSymbol.Trim();
            orders = orders.Where(d => d.Magazyn != null && d.Magazyn.Symbol == symbol);
        }

        var lines = orders
            .SelectMany(d => d.Pozycje)
            .Where(p => p.Rezerwacja != null && p.Rezerwacja.Ilosc > p.Rezerwacja.IloscZrealizowana);

        if (productId.HasValue)
        {
            var id = productId.Value;
            lines = lines.Where(p => p.AsortymentAktualnyId == id);
        }

        return lines
            .Select(p => new ReservationRow
            {
                LineId = p.Id,
                ProductId = p.AsortymentAktualnyId,
                ProductSymbol = p.AsortymentAktualny.Symbol,
                ProductName = p.AsortymentAktualny.Nazwa,
                LineWarehouseSymbol = p.Magazyn.Symbol,
                OrderWarehouseSymbol = p.Dokument.Magazyn.Symbol,
                Reserved = p.Rezerwacja.Ilosc,
                Consumed = p.Rezerwacja.IloscZrealizowana,
                IsStockReservation = p.Rezerwacja.Ilosciowa,
                ExpiresAt = p.Rezerwacja.Termin,
                StockUnitSymbol = p.AsortymentAktualny.JednostkaMagazynowa.JednostkaMiary.Symbol,
                BaseUnitSymbol = p.AsortymentAktualny.PodstawowaJednostkaMiaryAsortymentu.JednostkaMiary.Symbol,
                OrderId = p.Dokument.Id,
                OrderNumber = p.Dokument.NumerWewnetrzny.PelnaSygnatura,
                CustomerId = p.Dokument.PodmiotId,
                CustomerName = p.Dokument.Podmiot.NazwaSkrocona,
                OrderDate = p.Dokument.DataWydaniaWystawienia,
                OrderStatus = p.Dokument.StatusDokumentu.Nazwa,
            })
            .OrderByDescending(r => r.OrderDate)
            .ThenBy(r => r.LineId);
    }

    /// <summary>Overwrites the order header fields the dynamic mapper read from missing members.</summary>
    public static void EnrichHeader(CustomerOrderDto dto, object entity)
    {
        if (entity is not Dokument order) return;

        var value = SdkMember.Read(() => order.Wartosc, null);
        if (value != null)
        {
            dto.TotalNet = SdkMember.Read(() => value.NettoPoRabacie, 0m);
            dto.TotalGross = SdkMember.Read(() => value.BruttoPoRabacie, 0m);
        }

        dto.AmountToPay = SdkMember.Read(() => order.KwotaDoZaplaty, dto.AmountToPay);
        dto.Currency = SdkMember.Text(() => order.Waluta == null ? null : order.Waluta.Symbol) ?? dto.Currency;
        dto.IssueDate = SdkMember.Read(() => order.DataWydaniaWystawienia, null) ?? dto.IssueDate;

        var status = SdkMember.Read(() => order.StatusDokumentu, null);
        if (status != null)
        {
            dto.StatusId = SdkMember.Read(() => status.Id, dto.StatusId);
            dto.Status = SdkMember.Text(() => status.Nazwa) ?? dto.Status;
            dto.StatusSymbol = SdkMember.Text(() => status.Mnemonik) ?? dto.StatusSymbol;
            dto.IsClosed = SdkMember.Read(() => status.Zamkniety, null) ?? dto.IsClosed;
        }

        var header = SdkMember.Read(() => order.Naglowek, null);
        if (header != null)
        {
            dto.CreatedAt = SdkMember.Read<DateTimeOffset?>(() => header.Utworzono, null)?.LocalDateTime ?? dto.CreatedAt;
            dto.ModifiedAt = SdkMember.Read<DateTimeOffset?>(() => header.Zmieniono, null)?.LocalDateTime ?? dto.ModifiedAt;
        }

        var state = SdkMember.Read(() => order.StanRealizacjiZamowienia, null);
        var fraction = state == null ? (decimal?)null : SdkMember.Read<decimal?>(() => state.ProcentowyStanRealizacji, null);
        dto.RealizationPercent = fraction.HasValue ? Math.Round(fraction.Value * 100m, 2) : null;
    }
}
