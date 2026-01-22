using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Discounts and promotions endpoints
/// </summary>
[ApiController]
[Route("api/discounts")]
[Authorize]
[Tags("Discounts")]
public class DiscountsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<DiscountsController> _logger;

    public DiscountsController(ISferaService sferaService, ILogger<DiscountsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Discounts

    /// <summary>
    /// Get all discounts
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<DiscountDto>), StatusCodes.Status200OK)]
    public ActionResult<PagedResponse<DiscountDto>> GetDiscounts(
        [FromQuery] bool? activeOnly = true,
        [FromQuery] DateTime? validAt,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var rabaty = sfera.Rabaty().Dane.Wszystkie();

            if (activeOnly == true)
            {
                rabaty = rabaty.Where(r => r.Aktywny == true);
            }

            var checkDate = validAt ?? DateTime.Now;
            rabaty = rabaty.Where(r =>
                (!r.OdDaty.HasValue || r.OdDaty.Value <= checkDate) &&
                (!r.DoDaty.HasValue || r.DoDaty.Value >= checkDate));

            var totalCount = rabaty.Count();
            var items = rabaty
                .OrderBy(r => r.Symbol)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(r => MapDiscount(r, false))
                .ToList();

            return Ok(new PagedResponse<DiscountDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discounts");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving discounts", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get discount by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<DiscountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DiscountDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<DiscountDto>> GetDiscount(int id, [FromQuery] bool includeDetails = true)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var rabat = sfera.Rabaty().Dane.Wszystkie()
                .FirstOrDefault(r => r.Id == id);

            if (rabat == null)
            {
                return NotFound(ApiResponse<DiscountDto>.Error($"Discount with ID {id} not found"));
            }

            var dto = MapDiscount(rabat, includeDetails);
            return Ok(ApiResponse<DiscountDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discount {Id}", id);
            return StatusCode(500, ApiResponse<DiscountDto>.Error("Error retrieving discount", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get discount by symbol
    /// </summary>
    [HttpGet("by-symbol/{symbol}")]
    [ProducesResponseType(typeof(ApiResponse<DiscountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DiscountDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<DiscountDto>> GetDiscountBySymbol(string symbol, [FromQuery] bool includeDetails = true)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var rabat = sfera.Rabaty().Dane.Wszystkie()
                .FirstOrDefault(r => r.Symbol == symbol);

            if (rabat == null)
            {
                return NotFound(ApiResponse<DiscountDto>.Error($"Discount with symbol '{symbol}' not found"));
            }

            var dto = MapDiscount(rabat, includeDetails);
            return Ok(ApiResponse<DiscountDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discount by symbol {Symbol}", symbol);
            return StatusCode(500, ApiResponse<DiscountDto>.Error("Error retrieving discount", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get discounts applicable to a contractor
    /// </summary>
    [HttpGet("for-contractor/{contractorId}")]
    [ProducesResponseType(typeof(ApiResponse<List<DiscountDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<DiscountDto>>> GetDiscountsForContractor(int contractorId, [FromQuery] bool? activeOnly = true)
    {
        try
        {
            var sfera = _sferaService.GetSfera();

            // Verify contractor exists
            var kontrahent = sfera.Kontrahenci().Dane.Wszystkie()
                .FirstOrDefault(k => k.Id == contractorId);

            if (kontrahent == null)
            {
                return NotFound(ApiResponse<List<DiscountDto>>.Error($"Contractor with ID {contractorId} not found"));
            }

            var rabaty = sfera.Rabaty().Dane.Wszystkie();

            if (activeOnly == true)
            {
                rabaty = rabaty.Where(r => r.Aktywny == true);
            }

            // Filter by contractor
            var now = DateTime.Now;
            var applicableDiscounts = rabaty
                .Where(r => (!r.OdDaty.HasValue || r.OdDaty.Value <= now) &&
                           (!r.DoDaty.HasValue || r.DoDaty.Value >= now))
                .ToList()
                .Where(r => r.Podmioty?.Any(p => p.Id == contractorId) == true ||
                           !r.Podmioty?.Any() == true) // No subjects = applies to all
                .Select(r => MapDiscount(r, false))
                .ToList();

            return Ok(ApiResponse<List<DiscountDto>>.Ok(applicableDiscounts));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discounts for contractor {ContractorId}", contractorId);
            return StatusCode(500, ApiResponse<List<DiscountDto>>.Error("Error retrieving discounts", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Product Attributes (Cechy asortymentu)

    /// <summary>
    /// Get all product attributes/features
    /// </summary>
    [HttpGet("~/api/product-attributes")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductAttributeDto>>), StatusCodes.Status200OK)]
    [Tags("Product Attributes")]
    public ActionResult<ApiResponse<List<ProductAttributeDto>>> GetProductAttributes()
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var cechy = sfera.CechyAsortymentu().Dane.Wszystkie()
                .ToList();

            var dtos = cechy.Select(c => new ProductAttributeDto
            {
                Id = c.Id,
                Name = c.Nazwa,
                IsActive = true,
                ProductCount = c.ZbioryAsortymentu?.Count ?? 0
            }).OrderBy(c => c.Name).ToList();

            return Ok(ApiResponse<List<ProductAttributeDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product attributes");
            return StatusCode(500, ApiResponse<List<ProductAttributeDto>>.Error("Error retrieving product attributes", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product attribute by ID
    /// </summary>
    [HttpGet("~/api/product-attributes/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ProductAttributeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProductAttributeDto>), StatusCodes.Status404NotFound)]
    [Tags("Product Attributes")]
    public ActionResult<ApiResponse<ProductAttributeDto>> GetProductAttribute(int id)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var cecha = sfera.CechyAsortymentu().Dane.Wszystkie()
                .FirstOrDefault(c => c.Id == id);

            if (cecha == null)
            {
                return NotFound(ApiResponse<ProductAttributeDto>.Error($"Product attribute with ID {id} not found"));
            }

            var dto = new ProductAttributeDto
            {
                Id = cecha.Id,
                Name = cecha.Nazwa,
                IsActive = true,
                ProductCount = cecha.ZbioryAsortymentu?.Count ?? 0
            };

            return Ok(ApiResponse<ProductAttributeDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product attribute {Id}", id);
            return StatusCode(500, ApiResponse<ProductAttributeDto>.Error("Error retrieving product attribute", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Contractor Groups

    /// <summary>
    /// Get all contractor groups
    /// </summary>
    [HttpGet("~/api/contractor-groups")]
    [ProducesResponseType(typeof(ApiResponse<List<ContractorGroupDto>>), StatusCodes.Status200OK)]
    [Tags("Contractor Groups")]
    public ActionResult<ApiResponse<List<ContractorGroupDto>>> GetContractorGroups([FromQuery] bool? hierarchical = false)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var grupy = sfera.Kontrahenci().Dane.Wszystkie()
                .SelectMany(k => k.GrupyKontrahenta ?? Enumerable.Empty<GrupaKontrahenta>())
                .Distinct()
                .ToList();

            // Alternative approach - get from dedicated groups manager if available
            // For now, mapping what we can get from contractor relationships

            var dtos = grupy.Select(g => new ContractorGroupDto
            {
                Id = g.Id,
                Symbol = g.Symbol,
                Name = g.Nazwa,
                IsActive = g.Aktywna ?? true
            }).OrderBy(g => g.Symbol).Distinct().ToList();

            return Ok(ApiResponse<List<ContractorGroupDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contractor groups");
            return StatusCode(500, ApiResponse<List<ContractorGroupDto>>.Error("Error retrieving contractor groups", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get contractors in a group
    /// </summary>
    [HttpGet("~/api/contractor-groups/{groupId}/contractors")]
    [ProducesResponseType(typeof(PagedResponse<CustomerDto>), StatusCodes.Status200OK)]
    [Tags("Contractor Groups")]
    public ActionResult<PagedResponse<CustomerDto>> GetContractorsInGroup(
        int groupId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var kontrahenci = sfera.Kontrahenci().Dane.Wszystkie()
                .Where(k => k.GrupyKontrahenta != null &&
                           k.GrupyKontrahenta.Any(g => g.Id == groupId));

            var totalCount = kontrahenci.Count();
            var items = kontrahenci
                .OrderBy(k => k.NazwaSkrocona)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(k => new CustomerDto
                {
                    Id = k.Id,
                    Symbol = k.Symbol,
                    ShortName = k.NazwaSkrocona,
                    FullName = k.NazwaPelna,
                    TaxId = k.NIP,
                    IsActive = k.Aktywny ?? false
                }).ToList();

            return Ok(new PagedResponse<CustomerDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contractors in group {GroupId}", groupId);
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving contractors", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Mapping

    private static DiscountDto MapDiscount(Rabat r, bool includeDetails)
    {
        var dto = new DiscountDto
        {
            Id = r.Id,
            Symbol = r.Symbol,
            Name = r.Nazwa,
            Description = r.Opis,
            PercentValue = r.ProcentRabatu,
            AmountValue = r.KwotaRabatu,
            CurrencySymbol = r.Waluta?.Symbol,
            ValidFrom = r.OdDaty,
            ValidTo = r.DoDaty,
            IsActive = r.Aktywny ?? false,
            Priority = r.Priorytet
        };

        // Determine discount type
        if (r.ProcentRabatu.HasValue && r.ProcentRabatu > 0)
        {
            dto.Type = DiscountType.Percentage;
        }
        else if (r.KwotaRabatu.HasValue && r.KwotaRabatu > 0)
        {
            dto.Type = DiscountType.Amount;
        }

        if (includeDetails && r.Podmioty != null)
        {
            dto.Subjects = r.Podmioty.Select(p => new DiscountSubjectDto
            {
                Id = p.Id,
                SubjectType = "Contractor",
                SubjectId = p.Id,
                SubjectName = p.NazwaSkrocona
            }).ToList();
        }

        return dto;
    }

    #endregion
}
