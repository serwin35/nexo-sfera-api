using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

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
        [FromQuery] DateTime? validAt = null,
        [FromQuery] bool? activeOnly = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var rabatyManager = sfera.Rabaty();
            var allRabaty = ((IEnumerable<dynamic>)rabatyManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                allRabaty = allRabaty.Where(r => DynamicPropertyHelper.GetBool(r, "Aktywny")).ToList();
            }

            var checkDate = validAt ?? DateTime.Now;
            allRabaty = allRabaty.Where(r =>
            {
                var odDaty = DynamicPropertyHelper.GetDateTime(r, "OdDaty");
                var doDaty = DynamicPropertyHelper.GetDateTime(r, "DoDaty");
                return (!odDaty.HasValue || odDaty.Value <= checkDate) &&
                       (!doDaty.HasValue || doDaty.Value >= checkDate);
            }).ToList();

            var totalCount = allRabaty.Count;
            var pagedRabaty = allRabaty
                .OrderBy(r => DynamicPropertyHelper.GetString(r, "Symbol"))
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<DiscountDto>();
            foreach (var r in pagedRabaty)
            {
                items.Add(MapDiscount(r, false));
            }

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
            dynamic sfera = _sferaService.GetSfera();
            var rabatyManager = sfera.Rabaty();
            var allRabaty = ((IEnumerable<dynamic>)rabatyManager.Dane.Wszystkie()).ToList();
            var rabat = allRabaty.FirstOrDefault(r => DynamicPropertyHelper.GetId(r) == id);

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
            dynamic sfera = _sferaService.GetSfera();
            var rabatyManager = sfera.Rabaty();
            var allRabaty = ((IEnumerable<dynamic>)rabatyManager.Dane.Wszystkie()).ToList();
            var rabat = allRabaty.FirstOrDefault(r =>
                DynamicPropertyHelper.GetString(r, "Symbol") == symbol);

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
            dynamic sfera = _sferaService.GetSfera();

            // Verify contractor exists
            var kontrahenciManager = sfera.Kontrahenci();
            var allKontrahenci = ((IEnumerable<dynamic>)kontrahenciManager.Dane.Wszystkie()).ToList();
            var kontrahent = allKontrahenci.FirstOrDefault(k => DynamicPropertyHelper.GetId(k) == contractorId);

            if (kontrahent == null)
            {
                return NotFound(ApiResponse<List<DiscountDto>>.Error($"Contractor with ID {contractorId} not found"));
            }

            var rabatyManager = sfera.Rabaty();
            var allRabaty = ((IEnumerable<dynamic>)rabatyManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                allRabaty = allRabaty.Where(r => DynamicPropertyHelper.GetBool(r, "Aktywny")).ToList();
            }

            // Filter by contractor
            var now = DateTime.Now;
            var applicableDiscounts = allRabaty
                .Where(r =>
                {
                    var odDaty = DynamicPropertyHelper.GetDateTime(r, "OdDaty");
                    var doDaty = DynamicPropertyHelper.GetDateTime(r, "DoDaty");
                    return (!odDaty.HasValue || odDaty.Value <= now) &&
                           (!doDaty.HasValue || doDaty.Value >= now);
                })
                .Where(r =>
                {
                    var podmioty = DynamicPropertyHelper.GetCollection(r, "Podmioty").ToList();
                    // No subjects = applies to all, OR contractor is in the list
                    if (!podmioty.Any()) return true;
                    foreach (var p in podmioty)
                    {
                        if (DynamicPropertyHelper.GetId(p) == contractorId) return true;
                    }
                    return false;
                })
                .ToList();

            var applicableDiscountDtos = new List<DiscountDto>();
            foreach (var r in applicableDiscounts)
            {
                applicableDiscountDtos.Add(MapDiscount(r, false));
            }

            return Ok(ApiResponse<List<DiscountDto>>.Ok(applicableDiscountDtos));
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
            dynamic sfera = _sferaService.GetSfera();
            var cechyManager = sfera.CechyAsortymentu();
            var allCechy = ((IEnumerable<dynamic>)cechyManager.Dane.Wszystkie()).ToList();

            var dtos = new List<ProductAttributeDto>();
            foreach (var c in allCechy)
            {
                var zbiory = DynamicPropertyHelper.GetCollection(c, "ZbioryAsortymentu");
                dtos.Add(new ProductAttributeDto
                {
                    Id = DynamicPropertyHelper.GetId(c),
                    Name = DynamicPropertyHelper.GetString(c, "Nazwa"),
                    IsActive = true,
                    ProductCount = zbiory.Count()
                });
            }
            dtos = dtos.OrderBy(c => c.Name).ToList();

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
            dynamic sfera = _sferaService.GetSfera();
            var cechyManager = sfera.CechyAsortymentu();
            var allCechy = ((IEnumerable<dynamic>)cechyManager.Dane.Wszystkie()).ToList();
            var cecha = allCechy.FirstOrDefault(c => DynamicPropertyHelper.GetId(c) == id);

            if (cecha == null)
            {
                return NotFound(ApiResponse<ProductAttributeDto>.Error($"Product attribute with ID {id} not found"));
            }

            var zbiory = DynamicPropertyHelper.GetCollection(cecha, "ZbioryAsortymentu");

            var dto = new ProductAttributeDto
            {
                Id = DynamicPropertyHelper.GetId(cecha),
                Name = DynamicPropertyHelper.GetString(cecha, "Nazwa"),
                IsActive = true,
                ProductCount = zbiory.Count()
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
            dynamic sfera = _sferaService.GetSfera();
            var kontrahenciManager = sfera.Kontrahenci();
            var allKontrahenci = ((IEnumerable<dynamic>)kontrahenciManager.Dane.Wszystkie()).ToList();

            // Collect all groups from contractor relationships
            var grupyDict = new Dictionary<int, dynamic>();
            foreach (var kontrahent in allKontrahenci)
            {
                var grupy = DynamicPropertyHelper.GetCollection(kontrahent, "GrupyKontrahenta");
                foreach (var grupa in grupy)
                {
                    var id = DynamicPropertyHelper.GetId(grupa);
                    if (id != 0 && !grupyDict.ContainsKey(id))
                    {
                        grupyDict[id] = grupa;
                    }
                }
            }

            var dtos = new List<ContractorGroupDto>();
            foreach (var g in grupyDict.Values)
            {
                dtos.Add(new ContractorGroupDto
                {
                    Id = DynamicPropertyHelper.GetId(g),
                    Symbol = DynamicPropertyHelper.GetString(g, "Symbol"),
                    Name = DynamicPropertyHelper.GetString(g, "Nazwa"),
                    IsActive = DynamicPropertyHelper.GetNullableBool(g, "Aktywna") ?? true
                });
            }
            dtos = dtos.OrderBy(g => g.Symbol).ToList();

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
            dynamic sfera = _sferaService.GetSfera();
            var kontrahenciManager = sfera.Kontrahenci();
            var allKontrahenci = ((IEnumerable<dynamic>)kontrahenciManager.Dane.Wszystkie()).ToList();

            // Filter contractors in the specified group
            var kontrahenci = new List<dynamic>();
            foreach (var k in allKontrahenci)
            {
                var grupy = DynamicPropertyHelper.GetCollection(k, "GrupyKontrahenta").ToList();
                bool inGroup = false;
                foreach (var g in grupy)
                {
                    if (DynamicPropertyHelper.GetId(g) == groupId)
                    {
                        inGroup = true;
                        break;
                    }
                }
                if (inGroup)
                {
                    kontrahenci.Add(k);
                }
            }

            var totalCount = kontrahenci.Count;
            var pagedKontrahenci = kontrahenci
                .OrderBy(k => DynamicPropertyHelper.GetString(k, "NazwaSkrocona"))
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<CustomerDto>();
            foreach (var k in pagedKontrahenci)
            {
                items.Add(new CustomerDto
                {
                    Id = DynamicPropertyHelper.GetId(k),
                    Symbol = DynamicPropertyHelper.GetString(k, "Symbol"),
                    ShortName = DynamicPropertyHelper.GetString(k, "NazwaSkrocona"),
                    FullName = DynamicPropertyHelper.GetString(k, "NazwaPelna"),
                    TaxId = DynamicPropertyHelper.GetString(k, "NIP"),
                    IsActive = DynamicPropertyHelper.GetBool(k, "Aktywny")
                });
            }

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

    private static DiscountDto MapDiscount(dynamic r, bool includeDetails)
    {
        var waluta = DynamicPropertyHelper.GetProperty(r, "Waluta");
        var procentRabatu = DynamicPropertyHelper.GetNullableDecimal(r, "ProcentRabatu");
        var kwotaRabatu = DynamicPropertyHelper.GetNullableDecimal(r, "KwotaRabatu");

        var dto = new DiscountDto
        {
            Id = DynamicPropertyHelper.GetId(r),
            Symbol = DynamicPropertyHelper.GetString(r, "Symbol"),
            Name = DynamicPropertyHelper.GetString(r, "Nazwa"),
            Description = DynamicPropertyHelper.GetString(r, "Opis"),
            PercentValue = procentRabatu,
            AmountValue = kwotaRabatu,
            CurrencySymbol = waluta != null ? DynamicPropertyHelper.GetString(waluta, "Symbol") : null,
            ValidFrom = DynamicPropertyHelper.GetDateTime(r, "OdDaty"),
            ValidTo = DynamicPropertyHelper.GetDateTime(r, "DoDaty"),
            IsActive = DynamicPropertyHelper.GetBool(r, "Aktywny"),
            Priority = DynamicPropertyHelper.GetNullableInt(r, "Priorytet")
        };

        // Determine discount type
        if (procentRabatu.HasValue && procentRabatu > 0)
        {
            dto.Type = DiscountType.Percentage;
        }
        else if (kwotaRabatu.HasValue && kwotaRabatu > 0)
        {
            dto.Type = DiscountType.Amount;
        }

        if (includeDetails)
        {
            var podmioty = DynamicPropertyHelper.GetCollection(r, "Podmioty").ToList();
            if (podmioty.Count > 0)
            {
                dto.Subjects = new List<DiscountSubjectDto>();
                foreach (var p in podmioty)
                {
                    dto.Subjects.Add(new DiscountSubjectDto
                    {
                        Id = DynamicPropertyHelper.GetId(p),
                        SubjectType = "Contractor",
                        SubjectId = DynamicPropertyHelper.GetId(p),
                        SubjectName = DynamicPropertyHelper.GetString(p, "NazwaSkrocona")
                    });
                }
            }
        }

        return dto;
    }

    #endregion
}
