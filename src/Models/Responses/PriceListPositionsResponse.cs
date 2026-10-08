using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Models.Responses;

/// <summary>
/// Page of price list positions together with the price list header read in the same SDK call,
/// so a caller can check <c>IsBase</c> / <c>Status</c> / currency without a second request.
/// </summary>
public class PriceListPositionsResponse : PagedResponse<PriceListPositionDto>
{
    public PriceListDto PriceList { get; set; } = new();

    /// <summary>
    /// With <c>productIds</c>: requested ids that have no position in this price list
    /// (unknown product or product not in the list).
    /// </summary>
    public List<int> MissingProductIds { get; set; } = new();
}
