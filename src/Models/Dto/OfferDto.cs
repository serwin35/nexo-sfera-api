namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Offer (Oferta OE) DTO
/// </summary>
public class OfferDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }

    // Customer info
    public int? CustomerId { get; set; }
    public string? CustomerSymbol { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerTaxId { get; set; }

    // Status
    public string? Status { get; set; }
    public bool IsClosed { get; set; }
    public bool IsAccepted { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public DateTime? LastStatusChangeDate { get; set; }

    // Sales rep
    public int? SalesRepId { get; set; }
    public string? SalesRepName { get; set; }

    // Realization
    public int? DaysToRealization { get; set; }
    public DateTime? RealizationDate { get; set; }

    // Description
    public string? ShortDescription { get; set; }
    public string? EndDescription { get; set; }

    // Values
    public decimal NetValue { get; set; }
    public decimal GrossValue { get; set; }
    public decimal TaxValue { get; set; }
    public string? CurrencySymbol { get; set; }

    // Items
    public int ItemCount { get; set; }
    public List<OfferItemDto>? Items { get; set; }

    /// <summary>
    /// Creation timestamp (SDK 60.0.0: ProcesOfertowy.Utworzono)
    /// </summary>
    public DateTimeOffset? CreatedAt { get; set; }
}

/// <summary>
/// Offer item (position) DTO
/// </summary>
public class OfferItemDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }

    // Product info
    public int? ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public string? ProductDescription { get; set; }

    // Quantity
    public decimal Quantity { get; set; }
    public string? UnitSymbol { get; set; }

    // Prices
    public decimal UnitPriceNet { get; set; }
    public decimal UnitPriceGross { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountValue { get; set; }

    // Values
    public decimal NetValue { get; set; }
    public decimal GrossValue { get; set; }
    public decimal TaxValue { get; set; }
    public string? VatRateSymbol { get; set; }
    public decimal? VatRate { get; set; }
}

/// <summary>
/// Offer summary DTO for listing
/// </summary>
public class OfferSummaryDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string? CustomerName { get; set; }
    public string? Status { get; set; }
    public bool IsClosed { get; set; }
    public bool IsAccepted { get; set; }
    public decimal NetValue { get; set; }
    public decimal GrossValue { get; set; }
    public string? CurrencySymbol { get; set; }
    public int ItemCount { get; set; }
}

/// <summary>
/// Create offer request
/// </summary>
public class CreateOfferRequest
{
    public int CustomerId { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int? SalesRepId { get; set; }
    public int? DaysToRealization { get; set; }
    public string? Description { get; set; }
    public List<CreateOfferItemRequest> Items { get; set; } = new();

    /// <summary>
    /// Whether to reserve document number before saving.
    /// If true: calls ZarezerwujNumer() before Zapisz() - number is reserved immediately, may create gaps if save fails.
    /// If false (default): number is assigned automatically during Zapisz() - no gaps in numbering.
    /// </summary>
    public bool ReserveNumber { get; set; } = false;
}

/// <summary>
/// Create offer item request
/// </summary>
public class CreateOfferItemRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPriceNet { get; set; }
    public decimal? DiscountPercent { get; set; }
}
