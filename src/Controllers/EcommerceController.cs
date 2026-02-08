using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// E-commerce integrations and shipping management
/// </summary>
[ApiController]
[Route("api/ecommerce")]
[Authorize]
[Tags("E-commerce")]
public class EcommerceController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<EcommerceController> _logger;

    public EcommerceController(ISferaService sferaService, ILogger<EcommerceController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Integration Accounts

    /// <summary>
    /// Get e-commerce integration accounts
    /// </summary>
    [HttpGet("integrations")]
    [ProducesResponseType(typeof(ApiResponse<List<IntegrationAccountDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<IntegrationAccountDto>>> GetIntegrationAccounts()
    {
        try
        {
            var manager = _sferaService.GetManager("KontaIntegracji");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<List<IntegrationAccountDto>>.Error("Failed to get KontaIntegracji manager"));
            }

            var allAccounts = DynamicPropertyHelper.SafeGetAll((object)manager);
            var items = new List<IntegrationAccountDto>();

            foreach (var acc in allAccounts)
            {
                items.Add(new IntegrationAccountDto
                {
                    Id = DynamicPropertyHelper.GetId(acc),
                    Name = DynamicPropertyHelper.GetString(acc, "Nazwa") ?? "",
                    Platform = DynamicPropertyHelper.GetString(acc, "Platforma") ?? "",
                    IsActive = DynamicPropertyHelper.GetBool(acc, "Aktywny"),
                    LastSyncDate = DynamicPropertyHelper.GetDateTime(acc, "DataOstatniejSynchronizacji"),
                    Description = DynamicPropertyHelper.GetString(acc, "Opis")
                });
            }

            return Ok(ApiResponse<List<IntegrationAccountDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting integration accounts");
            return StatusCode(500, ApiResponse<List<IntegrationAccountDto>>.Error("Error retrieving integration accounts", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get integration account by ID
    /// </summary>
    [HttpGet("integrations/{id}")]
    [ProducesResponseType(typeof(ApiResponse<IntegrationAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IntegrationAccountDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<IntegrationAccountDto>> GetIntegrationAccount(int id)
    {
        try
        {
            var manager = _sferaService.GetManager("KontaIntegracji");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<IntegrationAccountDto>.Error("Failed to get KontaIntegracji manager"));
            }

            var allAccounts = DynamicPropertyHelper.SafeGetAll((object)manager);
            var account = allAccounts.FirstOrDefault(a => DynamicPropertyHelper.GetId(a) == id);

            if (account == null)
            {
                return NotFound(ApiResponse<IntegrationAccountDto>.Error($"Integration account with ID {id} not found"));
            }

            var dto = new IntegrationAccountDto
            {
                Id = DynamicPropertyHelper.GetId(account),
                Name = DynamicPropertyHelper.GetString(account, "Nazwa") ?? "",
                Platform = DynamicPropertyHelper.GetString(account, "Platforma") ?? "",
                IsActive = DynamicPropertyHelper.GetBool(account, "Aktywny"),
                LastSyncDate = DynamicPropertyHelper.GetDateTime(account, "DataOstatniejSynchronizacji"),
                Description = DynamicPropertyHelper.GetString(account, "Opis")
            };

            return Ok(ApiResponse<IntegrationAccountDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting integration account {Id}", id);
            return StatusCode(500, ApiResponse<IntegrationAccountDto>.Error("Error retrieving integration account", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Online Offers

    /// <summary>
    /// Get online/internet offers
    /// </summary>
    [HttpGet("offers")]
    [ProducesResponseType(typeof(PagedResponse<OnlineOfferDto>), StatusCodes.Status200OK)]
    public ActionResult<PagedResponse<OnlineOfferDto>> GetOnlineOffers(
        [FromQuery] string? status,
        [FromQuery] int? integrationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var manager = _sferaService.GetManager("OfertyInternetowe");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get OfertyInternetowe manager"));
            }

            var allOffers = DynamicPropertyHelper.SafeGetAll((object)manager);

            // Filter by status
            if (!string.IsNullOrEmpty(status))
            {
                allOffers = allOffers.Where(o =>
                {
                    var offerStatus = DynamicPropertyHelper.GetString(o, "Status");
                    return offerStatus != null && offerStatus.Equals(status, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            // Filter by integration
            if (integrationId.HasValue)
            {
                allOffers = allOffers.Where(o =>
                {
                    var konto = DynamicPropertyHelper.GetProperty(o, "KontoIntegracji");
                    return konto != null && DynamicPropertyHelper.GetId(konto) == integrationId.Value;
                }).ToList();
            }

            var totalCount = allOffers.Count;
            var pagedOffers = allOffers
                .OrderByDescending(o => DynamicPropertyHelper.GetDateTime(o, "DataUtworzenia") ?? DateTime.MinValue)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<OnlineOfferDto>();
            foreach (var o in pagedOffers)
            {
                var asortyment = DynamicPropertyHelper.GetProperty(o, "Asortyment");

                items.Add(new OnlineOfferDto
                {
                    Id = DynamicPropertyHelper.GetId(o),
                    ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0,
                    ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                    ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
                    Title = DynamicPropertyHelper.GetString(o, "Tytul") ?? "",
                    Price = DynamicPropertyHelper.GetDecimal(o, "Cena"),
                    Quantity = DynamicPropertyHelper.GetDecimal(o, "Ilosc"),
                    Status = DynamicPropertyHelper.GetString(o, "Status") ?? "Unknown",
                    ExternalId = DynamicPropertyHelper.GetString(o, "IdentyfikatorZewnetrzny"),
                    CreatedDate = DynamicPropertyHelper.GetDateTime(o, "DataUtworzenia"),
                    LastModifiedDate = DynamicPropertyHelper.GetDateTime(o, "DataModyfikacji")
                });
            }

            return Ok(new PagedResponse<OnlineOfferDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting online offers");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving online offers", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get online offer by ID
    /// </summary>
    [HttpGet("offers/{id}")]
    [ProducesResponseType(typeof(ApiResponse<OnlineOfferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnlineOfferDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<OnlineOfferDto>> GetOnlineOffer(int id)
    {
        try
        {
            var manager = _sferaService.GetManager("OfertyInternetowe");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<OnlineOfferDto>.Error("Failed to get OfertyInternetowe manager"));
            }

            var allOffers = DynamicPropertyHelper.SafeGetAll((object)manager);
            var offer = allOffers.FirstOrDefault(o => DynamicPropertyHelper.GetId(o) == id);

            if (offer == null)
            {
                return NotFound(ApiResponse<OnlineOfferDto>.Error($"Online offer with ID {id} not found"));
            }

            var asortyment = DynamicPropertyHelper.GetProperty(offer, "Asortyment");

            var dto = new OnlineOfferDto
            {
                Id = DynamicPropertyHelper.GetId(offer),
                ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0,
                ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
                Title = DynamicPropertyHelper.GetString(offer, "Tytul") ?? "",
                Description = DynamicPropertyHelper.GetString(offer, "Opis"),
                Price = DynamicPropertyHelper.GetDecimal(offer, "Cena"),
                Quantity = DynamicPropertyHelper.GetDecimal(offer, "Ilosc"),
                Status = DynamicPropertyHelper.GetString(offer, "Status") ?? "Unknown",
                ExternalId = DynamicPropertyHelper.GetString(offer, "IdentyfikatorZewnetrzny"),
                ExternalUrl = DynamicPropertyHelper.GetString(offer, "LinkZewnetrzny"),
                CreatedDate = DynamicPropertyHelper.GetDateTime(offer, "DataUtworzenia"),
                LastModifiedDate = DynamicPropertyHelper.GetDateTime(offer, "DataModyfikacji")
            };

            return Ok(ApiResponse<OnlineOfferDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting online offer {Id}", id);
            return StatusCode(500, ApiResponse<OnlineOfferDto>.Error("Error retrieving online offer", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Offer Groups

    /// <summary>
    /// Get offer groups (categories for e-commerce)
    /// </summary>
    [HttpGet("offer-groups")]
    [ProducesResponseType(typeof(ApiResponse<List<OfferGroupDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<OfferGroupDto>>> GetOfferGroups()
    {
        try
        {
            var manager = _sferaService.GetManager("GrupyOfert");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<List<OfferGroupDto>>.Error("Failed to get GrupyOfert manager"));
            }

            var allGroups = DynamicPropertyHelper.SafeGetAll((object)manager);
            var items = new List<OfferGroupDto>();

            foreach (var g in allGroups)
            {
                var parent = DynamicPropertyHelper.GetProperty(g, "Rodzic");

                items.Add(new OfferGroupDto
                {
                    Id = DynamicPropertyHelper.GetId(g),
                    Name = DynamicPropertyHelper.GetString(g, "Nazwa") ?? "",
                    ParentId = parent != null ? DynamicPropertyHelper.GetId(parent) : null,
                    ExternalId = DynamicPropertyHelper.GetString(g, "IdentyfikatorZewnetrzny"),
                    IsActive = DynamicPropertyHelper.GetBool(g, "Aktywny")
                });
            }

            return Ok(ApiResponse<List<OfferGroupDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting offer groups");
            return StatusCode(500, ApiResponse<List<OfferGroupDto>>.Error("Error retrieving offer groups", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Shipping

    /// <summary>
    /// Get shipping lists
    /// </summary>
    [HttpGet("shipping-lists")]
    [ProducesResponseType(typeof(PagedResponse<ShippingListDto>), StatusCodes.Status200OK)]
    public ActionResult<PagedResponse<ShippingListDto>> GetShippingLists(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var manager = _sferaService.GetManager("ListyWysylkowe");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ListyWysylkowe manager"));
            }

            var allLists = DynamicPropertyHelper.SafeGetAll((object)manager);

            // Filter by date range
            if (dateFrom.HasValue)
            {
                allLists = allLists.Where(l =>
                {
                    var date = DynamicPropertyHelper.GetDateTime(l, "DataUtworzenia");
                    return date.HasValue && date.Value >= dateFrom.Value;
                }).ToList();
            }

            if (dateTo.HasValue)
            {
                allLists = allLists.Where(l =>
                {
                    var date = DynamicPropertyHelper.GetDateTime(l, "DataUtworzenia");
                    return date.HasValue && date.Value <= dateTo.Value;
                }).ToList();
            }

            // Filter by status
            if (!string.IsNullOrEmpty(status))
            {
                allLists = allLists.Where(l =>
                {
                    var listStatus = DynamicPropertyHelper.GetString(l, "Status");
                    return listStatus != null && listStatus.Equals(status, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            var totalCount = allLists.Count;
            var pagedLists = allLists
                .OrderByDescending(l => DynamicPropertyHelper.GetDateTime(l, "DataUtworzenia") ?? DateTime.MinValue)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<ShippingListDto>();
            foreach (var l in pagedLists)
            {
                items.Add(new ShippingListDto
                {
                    Id = DynamicPropertyHelper.GetId(l),
                    Number = DynamicPropertyHelper.GetString(l, "Numer") ?? "",
                    CreatedDate = DynamicPropertyHelper.GetDateTime(l, "DataUtworzenia"),
                    Status = DynamicPropertyHelper.GetString(l, "Status") ?? "Unknown",
                    CarrierName = DynamicPropertyHelper.GetString(l, "NazwaPrzewoznika"),
                    PackageCount = DynamicPropertyHelper.GetNullableInt(l, "LiczbaPaczek") ?? 0
                });
            }

            return Ok(new PagedResponse<ShippingListDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shipping lists");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving shipping lists", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get shipping packages
    /// </summary>
    [HttpGet("packages")]
    [ProducesResponseType(typeof(PagedResponse<ShippingPackageDto>), StatusCodes.Status200OK)]
    public ActionResult<PagedResponse<ShippingPackageDto>> GetShippingPackages(
        [FromQuery] int? shippingListId,
        [FromQuery] string? trackingNumber,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var manager = _sferaService.GetManager("PaczkiWysylkowe");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get PaczkiWysylkowe manager"));
            }

            var allPackages = DynamicPropertyHelper.SafeGetAll((object)manager);

            // Filter by shipping list
            if (shippingListId.HasValue)
            {
                allPackages = allPackages.Where(p =>
                {
                    var lista = DynamicPropertyHelper.GetProperty(p, "ListaWysylkowa");
                    return lista != null && DynamicPropertyHelper.GetId(lista) == shippingListId.Value;
                }).ToList();
            }

            // Filter by tracking number
            if (!string.IsNullOrEmpty(trackingNumber))
            {
                allPackages = allPackages.Where(p =>
                {
                    var tracking = DynamicPropertyHelper.GetString(p, "NumerSledzenia");
                    return tracking != null && tracking.Contains(trackingNumber, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            var totalCount = allPackages.Count;
            var pagedPackages = allPackages
                .OrderByDescending(p => DynamicPropertyHelper.GetDateTime(p, "DataUtworzenia") ?? DateTime.MinValue)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<ShippingPackageDto>();
            foreach (var p in pagedPackages)
            {
                items.Add(new ShippingPackageDto
                {
                    Id = DynamicPropertyHelper.GetId(p),
                    TrackingNumber = DynamicPropertyHelper.GetString(p, "NumerSledzenia") ?? "",
                    Status = DynamicPropertyHelper.GetString(p, "Status") ?? "Unknown",
                    Weight = DynamicPropertyHelper.GetDecimal(p, "Waga"),
                    Width = DynamicPropertyHelper.GetDecimal(p, "Szerokosc"),
                    Height = DynamicPropertyHelper.GetDecimal(p, "Wysokosc"),
                    Depth = DynamicPropertyHelper.GetDecimal(p, "Glebokosc"),
                    CarrierName = DynamicPropertyHelper.GetString(p, "NazwaPrzewoznika"),
                    CreatedDate = DynamicPropertyHelper.GetDateTime(p, "DataUtworzenia"),
                    ShippedDate = DynamicPropertyHelper.GetDateTime(p, "DataWyslania"),
                    DeliveredDate = DynamicPropertyHelper.GetDateTime(p, "DataDostarczenia")
                });
            }

            return Ok(new PagedResponse<ShippingPackageDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shipping packages");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving shipping packages", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get shipping package by ID
    /// </summary>
    [HttpGet("packages/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ShippingPackageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ShippingPackageDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<ShippingPackageDto>> GetShippingPackage(int id)
    {
        try
        {
            var manager = _sferaService.GetManager("PaczkiWysylkowe");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<ShippingPackageDto>.Error("Failed to get PaczkiWysylkowe manager"));
            }

            var allPackages = DynamicPropertyHelper.SafeGetAll((object)manager);
            var package = allPackages.FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == id);

            if (package == null)
            {
                return NotFound(ApiResponse<ShippingPackageDto>.Error($"Shipping package with ID {id} not found"));
            }

            var dto = new ShippingPackageDto
            {
                Id = DynamicPropertyHelper.GetId(package),
                TrackingNumber = DynamicPropertyHelper.GetString(package, "NumerSledzenia") ?? "",
                Status = DynamicPropertyHelper.GetString(package, "Status") ?? "Unknown",
                Weight = DynamicPropertyHelper.GetDecimal(package, "Waga"),
                Width = DynamicPropertyHelper.GetDecimal(package, "Szerokosc"),
                Height = DynamicPropertyHelper.GetDecimal(package, "Wysokosc"),
                Depth = DynamicPropertyHelper.GetDecimal(package, "Glebokosc"),
                CarrierName = DynamicPropertyHelper.GetString(package, "NazwaPrzewoznika"),
                RecipientName = DynamicPropertyHelper.GetString(package, "NazwaOdbiorcy"),
                RecipientAddress = DynamicPropertyHelper.GetString(package, "AdresOdbiorcy"),
                CreatedDate = DynamicPropertyHelper.GetDateTime(package, "DataUtworzenia"),
                ShippedDate = DynamicPropertyHelper.GetDateTime(package, "DataWyslania"),
                DeliveredDate = DynamicPropertyHelper.GetDateTime(package, "DataDostarczenia")
            };

            return Ok(ApiResponse<ShippingPackageDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shipping package {Id}", id);
            return StatusCode(500, ApiResponse<ShippingPackageDto>.Error("Error retrieving shipping package", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get package by tracking number
    /// </summary>
    [HttpGet("packages/track/{trackingNumber}")]
    [ProducesResponseType(typeof(ApiResponse<ShippingPackageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ShippingPackageDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<ShippingPackageDto>> GetPackageByTracking(string trackingNumber)
    {
        try
        {
            var manager = _sferaService.GetManager("PaczkiWysylkowe");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<ShippingPackageDto>.Error("Failed to get PaczkiWysylkowe manager"));
            }

            var allPackages = DynamicPropertyHelper.SafeGetAll((object)manager);
            var package = allPackages.FirstOrDefault(p =>
            {
                var tracking = DynamicPropertyHelper.GetString(p, "NumerSledzenia");
                return tracking != null && tracking.Equals(trackingNumber, StringComparison.OrdinalIgnoreCase);
            });

            if (package == null)
            {
                return NotFound(ApiResponse<ShippingPackageDto>.Error($"Package with tracking number {trackingNumber} not found"));
            }

            var dto = new ShippingPackageDto
            {
                Id = DynamicPropertyHelper.GetId(package),
                TrackingNumber = DynamicPropertyHelper.GetString(package, "NumerSledzenia") ?? "",
                Status = DynamicPropertyHelper.GetString(package, "Status") ?? "Unknown",
                Weight = DynamicPropertyHelper.GetDecimal(package, "Waga"),
                Width = DynamicPropertyHelper.GetDecimal(package, "Szerokosc"),
                Height = DynamicPropertyHelper.GetDecimal(package, "Wysokosc"),
                Depth = DynamicPropertyHelper.GetDecimal(package, "Glebokosc"),
                CarrierName = DynamicPropertyHelper.GetString(package, "NazwaPrzewoznika"),
                CreatedDate = DynamicPropertyHelper.GetDateTime(package, "DataUtworzenia"),
                ShippedDate = DynamicPropertyHelper.GetDateTime(package, "DataWyslania"),
                DeliveredDate = DynamicPropertyHelper.GetDateTime(package, "DataDostarczenia")
            };

            return Ok(ApiResponse<ShippingPackageDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting package by tracking {TrackingNumber}", trackingNumber);
            return StatusCode(500, ApiResponse<ShippingPackageDto>.Error("Error retrieving package", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Package Dimensions

    /// <summary>
    /// Get predefined package dimension templates
    /// </summary>
    [HttpGet("package-dimensions")]
    [ProducesResponseType(typeof(ApiResponse<List<PackageDimensionDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<PackageDimensionDto>>> GetPackageDimensions()
    {
        try
        {
            var manager = _sferaService.GetManager("GabarytyPaczki");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<List<PackageDimensionDto>>.Error("Failed to get GabarytyPaczki manager"));
            }

            var allDimensions = DynamicPropertyHelper.SafeGetAll((object)manager);
            var items = new List<PackageDimensionDto>();

            foreach (var d in allDimensions)
            {
                items.Add(new PackageDimensionDto
                {
                    Id = DynamicPropertyHelper.GetId(d),
                    Name = DynamicPropertyHelper.GetString(d, "Nazwa") ?? "",
                    Width = DynamicPropertyHelper.GetDecimal(d, "Szerokosc"),
                    Height = DynamicPropertyHelper.GetDecimal(d, "Wysokosc"),
                    Depth = DynamicPropertyHelper.GetDecimal(d, "Glebokosc"),
                    MaxWeight = DynamicPropertyHelper.GetDecimal(d, "MaksymalnaWaga"),
                    IsDefault = DynamicPropertyHelper.GetBool(d, "Domyslny")
                });
            }

            return Ok(ApiResponse<List<PackageDimensionDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting package dimensions");
            return StatusCode(500, ApiResponse<List<PackageDimensionDto>>.Error("Error retrieving package dimensions", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Available Platforms (Static)

    /// <summary>
    /// Get supported e-commerce platforms
    /// </summary>
    [HttpGet("platforms")]
    [ProducesResponseType(typeof(ApiResponse<List<EcommercePlatformDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<EcommercePlatformDto>>> GetSupportedPlatforms()
    {
        var platforms = new List<EcommercePlatformDto>
        {
            new() { Code = "ALLEGRO", Name = "Allegro", Country = "PL", Type = "Marketplace" },
            new() { Code = "AMAZON", Name = "Amazon", Country = "EU", Type = "Marketplace" },
            new() { Code = "EBAY", Name = "eBay", Country = "EU", Type = "Marketplace" },
            new() { Code = "WOOCOMMERCE", Name = "WooCommerce", Country = "Global", Type = "E-commerce Platform" },
            new() { Code = "PRESTASHOP", Name = "PrestaShop", Country = "Global", Type = "E-commerce Platform" },
            new() { Code = "SHOPIFY", Name = "Shopify", Country = "Global", Type = "E-commerce Platform" },
            new() { Code = "MAGENTO", Name = "Magento", Country = "Global", Type = "E-commerce Platform" },
            new() { Code = "SHOPER", Name = "Shoper", Country = "PL", Type = "E-commerce Platform" },
            new() { Code = "IDOSELL", Name = "IdoSell", Country = "PL", Type = "E-commerce Platform" },
            new() { Code = "BASELINKER", Name = "BaseLinker", Country = "PL", Type = "Integration Hub" }
        };

        return Ok(ApiResponse<List<EcommercePlatformDto>>.Ok(platforms));
    }

    #endregion
}

#region DTOs

/// <summary>
/// Integration account DTO
/// </summary>
public class IntegrationAccountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Online offer DTO
/// </summary>
public class OnlineOfferDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string? ExternalUrl { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

/// <summary>
/// Offer group DTO
/// </summary>
public class OfferGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public string? ExternalId { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Shipping list DTO
/// </summary>
public class ShippingListDto
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CarrierName { get; set; }
    public int PackageCount { get; set; }
}

/// <summary>
/// Shipping package DTO
/// </summary>
public class ShippingPackageDto
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal Depth { get; set; }
    public string? CarrierName { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientAddress { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
}

/// <summary>
/// Package dimension template DTO
/// </summary>
public class PackageDimensionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal Depth { get; set; }
    public decimal MaxWeight { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// E-commerce platform DTO
/// </summary>
public class EcommercePlatformDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

#endregion
