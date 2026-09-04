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
    public async Task<ActionResult<ApiResponse<List<IntegrationAccountDto>>>> GetIntegrationAccounts()
    {
        try
        {
            var items = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("KontaIntegracji");
                if (manager == null)
                    return null;

                var allAccounts = DynamicPropertyHelper.SafeGetAll((object)manager);
                var result = new List<IntegrationAccountDto>();

                foreach (var acc in allAccounts)
                {
                    result.Add(new IntegrationAccountDto
                    {
                        Id = DynamicPropertyHelper.GetId(acc),
                        Name = DynamicPropertyHelper.GetString(acc, "Nazwa") ?? "",
                        Platform = DynamicPropertyHelper.GetString(acc, "Platforma") ?? "",
                        IsActive = DynamicPropertyHelper.GetBool(acc, "Aktywny"),
                        LastSyncDate = DynamicPropertyHelper.GetDateTime(acc, "DataOstatniejSynchronizacji"),
                        Description = DynamicPropertyHelper.GetString(acc, "Opis")
                    });
                }

                return result;
            });

            if (items == null)
                return StatusCode(500, ApiResponse<List<IntegrationAccountDto>>.Error("Failed to get KontaIntegracji manager"));

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
    public async Task<ActionResult<ApiResponse<IntegrationAccountDto>>> GetIntegrationAccount(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("KontaIntegracji");
                if (manager == null)
                    return (true, (IntegrationAccountDto?)null);

                var allAccounts = DynamicPropertyHelper.SafeGetAll((object)manager);
                var account = allAccounts.FirstOrDefault(a => DynamicPropertyHelper.GetId(a) == id);

                if (account == null)
                    return (false, (IntegrationAccountDto?)null);

                return (false, new IntegrationAccountDto
                {
                    Id = DynamicPropertyHelper.GetId(account),
                    Name = DynamicPropertyHelper.GetString(account, "Nazwa") ?? "",
                    Platform = DynamicPropertyHelper.GetString(account, "Platforma") ?? "",
                    IsActive = DynamicPropertyHelper.GetBool(account, "Aktywny"),
                    LastSyncDate = DynamicPropertyHelper.GetDateTime(account, "DataOstatniejSynchronizacji"),
                    Description = DynamicPropertyHelper.GetString(account, "Opis")
                });
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<IntegrationAccountDto>.Error("Failed to get KontaIntegracji manager"));

            if (dto == null)
                return NotFound(ApiResponse<IntegrationAccountDto>.Error($"Integration account with ID {id} not found"));

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
    public async Task<ActionResult<PagedResponse<OnlineOfferDto>>> GetOnlineOffers(
        [FromQuery] string? status,
        [FromQuery] int? integrationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("OfertyInternetowe");
                if (manager == null)
                    return (null, 0);

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

                return (items, totalCount);
            });

            if (result.Item1 == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get OfertyInternetowe manager"));

            return Ok(new PagedResponse<OnlineOfferDto>
            {
                Data = result.Item1,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.Item2
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
    public async Task<ActionResult<ApiResponse<OnlineOfferDto>>> GetOnlineOffer(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("OfertyInternetowe");
                if (manager == null)
                    return (true, (OnlineOfferDto?)null);

                var allOffers = DynamicPropertyHelper.SafeGetAll((object)manager);
                var offer = allOffers.FirstOrDefault(o => DynamicPropertyHelper.GetId(o) == id);

                if (offer == null)
                    return (false, (OnlineOfferDto?)null);

                var asortyment = DynamicPropertyHelper.GetProperty(offer, "Asortyment");

                return (false, new OnlineOfferDto
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
                });
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<OnlineOfferDto>.Error("Failed to get OfertyInternetowe manager"));

            if (dto == null)
                return NotFound(ApiResponse<OnlineOfferDto>.Error($"Online offer with ID {id} not found"));

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
    public async Task<ActionResult<ApiResponse<List<OfferGroupDto>>>> GetOfferGroups()
    {
        try
        {
            var items = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("GrupyOfert");
                if (manager == null)
                    return null;

                var allGroups = DynamicPropertyHelper.SafeGetAll((object)manager);
                var result = new List<OfferGroupDto>();

                foreach (var g in allGroups)
                {
                    var parent = DynamicPropertyHelper.GetProperty(g, "Rodzic");

                    result.Add(new OfferGroupDto
                    {
                        Id = DynamicPropertyHelper.GetId(g),
                        Name = DynamicPropertyHelper.GetString(g, "Nazwa") ?? "",
                        ParentId = parent != null ? DynamicPropertyHelper.GetId(parent) : null,
                        ExternalId = DynamicPropertyHelper.GetString(g, "IdentyfikatorZewnetrzny"),
                        IsActive = DynamicPropertyHelper.GetBool(g, "Aktywny")
                    });
                }

                return result;
            });

            if (items == null)
                return StatusCode(500, ApiResponse<List<OfferGroupDto>>.Error("Failed to get GrupyOfert manager"));

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
    public async Task<ActionResult<PagedResponse<ShippingListDto>>> GetShippingLists(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("ListyWysylkowe");
                if (manager == null)
                    return (null, 0);

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
                        PackageCount = DynamicPropertyHelper.GetNullableInt(l, "LiczbaPaczek") ?? 0,
                        // SDK 60.0.0: ListaWysylkowa.AdresPodjazduKuriera
                        CourierPickupAddress = DynamicPropertyHelper.GetString(l, "AdresPodjazduKuriera")
                    });
                }

                return (items, totalCount);
            });

            if (result.Item1 == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ListyWysylkowe manager"));

            return Ok(new PagedResponse<ShippingListDto>
            {
                Data = result.Item1,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.Item2
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
    public async Task<ActionResult<PagedResponse<ShippingPackageDto>>> GetShippingPackages(
        [FromQuery] int? shippingListId,
        [FromQuery] string? trackingNumber,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("PaczkiWysylkowe");
                if (manager == null)
                    return (null, 0);

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

                return (items, totalCount);
            });

            if (result.Item1 == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get PaczkiWysylkowe manager"));

            return Ok(new PagedResponse<ShippingPackageDto>
            {
                Data = result.Item1,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.Item2
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
    public async Task<ActionResult<ApiResponse<ShippingPackageDto>>> GetShippingPackage(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("PaczkiWysylkowe");
                if (manager == null)
                    return (true, (ShippingPackageDto?)null);

                var allPackages = DynamicPropertyHelper.SafeGetAll((object)manager);
                var package = allPackages.FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == id);

                if (package == null)
                    return (false, (ShippingPackageDto?)null);

                return (false, new ShippingPackageDto
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
                });
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<ShippingPackageDto>.Error("Failed to get PaczkiWysylkowe manager"));

            if (dto == null)
                return NotFound(ApiResponse<ShippingPackageDto>.Error($"Shipping package with ID {id} not found"));

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
    public async Task<ActionResult<ApiResponse<ShippingPackageDto>>> GetPackageByTracking(string trackingNumber)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("PaczkiWysylkowe");
                if (manager == null)
                    return (true, (ShippingPackageDto?)null);

                var allPackages = DynamicPropertyHelper.SafeGetAll((object)manager);
                var package = allPackages.FirstOrDefault(p =>
                {
                    var tracking = DynamicPropertyHelper.GetString(p, "NumerSledzenia");
                    return tracking != null && tracking.Equals(trackingNumber, StringComparison.OrdinalIgnoreCase);
                });

                if (package == null)
                    return (false, (ShippingPackageDto?)null);

                return (false, new ShippingPackageDto
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
                });
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<ShippingPackageDto>.Error("Failed to get PaczkiWysylkowe manager"));

            if (dto == null)
                return NotFound(ApiResponse<ShippingPackageDto>.Error($"Package with tracking number {trackingNumber} not found"));

            return Ok(ApiResponse<ShippingPackageDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting package by tracking {TrackingNumber}", trackingNumber);
            return StatusCode(500, ApiResponse<ShippingPackageDto>.Error("Error retrieving package", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Shipping Orders

    /// <summary>
    /// Get e-commerce shipping orders (zamówienia wysyłkowe) - paged, with optional filters
    /// </summary>
    [HttpGet("shipping-orders")]
    [ProducesResponseType(typeof(PagedResponse<ShippingOrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ShippingOrderDto>>> GetShippingOrders(
        [FromQuery] int? integrationId,
        [FromQuery] string? status,
        [FromQuery] string? externalId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = GetShippingOrdersManager();
                if (manager == null)
                    return (null, 0);

                var allOrders = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by integration account
                if (integrationId.HasValue)
                {
                    allOrders = allOrders.Where(o =>
                    {
                        var konto = DynamicPropertyHelper.GetProperty(o, "KontoIntegracji");
                        return konto != null && DynamicPropertyHelper.GetId(konto) == integrationId.Value;
                    }).ToList();
                }

                // Filter by status name (StatusZamowieniaWysylkowego.Nazwa)
                if (!string.IsNullOrEmpty(status))
                {
                    allOrders = allOrders.Where(o =>
                    {
                        var statusName = DynamicPropertyHelper.GetString(o, "Status", "Nazwa");
                        return statusName != null && statusName.Equals(status, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                // Filter by external (marketplace) order id
                if (!string.IsNullOrEmpty(externalId))
                {
                    allOrders = allOrders.Where(o =>
                    {
                        var ext = DynamicPropertyHelper.GetString(o, "IdZewnetrzny");
                        return ext != null && ext.Equals(externalId, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                var totalCount = allOrders.Count;
                var pagedOrders = allOrders
                    .OrderByDescending(o => DynamicPropertyHelper.GetDateTime(o, "DataZakupu") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<ShippingOrderDto>();
                foreach (var o in pagedOrders)
                {
                    items.Add(MapShippingOrder(o, includeItems: false));
                }

                return (items, totalCount);
            });

            if (result.Item1 == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ZamowieniaWysylkowe manager"));

            return Ok(new PagedResponse<ShippingOrderDto>
            {
                Data = result.Item1,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.Item2
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shipping orders");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving shipping orders", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get shipping order by ID (with line items)
    /// </summary>
    [HttpGet("shipping-orders/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ShippingOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ShippingOrderDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ShippingOrderDto>>> GetShippingOrder(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = GetShippingOrdersManager();
                if (manager == null)
                    return (true, (ShippingOrderDto?)null);

                var order = DynamicPropertyHelper.FindById((object)manager, id);
                if (order == null)
                    return (false, (ShippingOrderDto?)null);

                return (false, MapShippingOrder((object)order, includeItems: true));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<ShippingOrderDto>.Error("Failed to get ZamowieniaWysylkowe manager"));

            if (dto == null)
                return NotFound(ApiResponse<ShippingOrderDto>.Error($"Shipping order with ID {id} not found"));

            return Ok(ApiResponse<ShippingOrderDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shipping order {Id}", id);
            return StatusCode(500, ApiResponse<ShippingOrderDto>.Error("Error retrieving shipping order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Resolves InsERT.Moria.HandelElektroniczny.IZamowieniaWysylkowe via PodajObiektTypu
    /// (SferaService.GetManager has no alias for it yet). Returns null when unavailable.
    /// </summary>
    private dynamic? GetShippingOrdersManager()
    {
        try
        {
            return _sferaService.GetManagerByType("InsERT.Moria.API", "InsERT.Moria.HandelElektroniczny.IZamowieniaWysylkowe");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ZamowieniaWysylkowe manager not available");
            return null;
        }
    }

    private static ShippingOrderDto MapShippingOrder(object o, bool includeItems)
    {
        object? konto = DynamicPropertyHelper.GetProperty(o, "KontoIntegracji");
        object? nabywca = DynamicPropertyHelper.GetProperty(o, "Nabywca");
        object? statusObj = DynamicPropertyHelper.GetProperty(o, "Status");

        string? firstName = DynamicPropertyHelper.GetString(o, "ImieOdbiorcy");
        string? lastName = DynamicPropertyHelper.GetString(o, "NazwiskoOdbiorcy");
        var recipientName = string.Join(" ", new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));

        string? houseNumber = DynamicPropertyHelper.GetString(o, "NumerDomuOdbiorcy");
        string? flatNumber = DynamicPropertyHelper.GetString(o, "NumerLokaluOdbiorcy");
        var buildingNumber = string.IsNullOrWhiteSpace(flatNumber) ? houseNumber : $"{houseNumber}/{flatNumber}";

        var dto = new ShippingOrderDto
        {
            Id = DynamicPropertyHelper.GetId(o),
            ExternalId = DynamicPropertyHelper.GetString(o, "IdZewnetrzny"),
            Number = DynamicPropertyHelper.GetString(o, "Numeracja", "NumerPelny")
                  ?? DynamicPropertyHelper.GetString(o, "Numeracja", "Numer"),
            Signature = DynamicPropertyHelper.GetString(o, "Sygnatura"),
            Title = DynamicPropertyHelper.GetString(o, "Tytul"),
            Kind = DynamicPropertyHelper.GetEnumString(o, "Rodzaj"),
            PurchaseDate = DynamicPropertyHelper.GetDateTime(o, "DataZakupu"),
            StatusId = statusObj != null ? DynamicPropertyHelper.GetId(statusObj) : null,
            Status = statusObj != null ? DynamicPropertyHelper.GetString(statusObj, "Nazwa") : null,
            ServiceStatus = DynamicPropertyHelper.GetString(o, "StatusZamowieniaWSerwisie"),
            ServicePaymentStatus = DynamicPropertyHelper.GetEnumString(o, "StatusPlatnosciZSerwisu"),
            IntegrationAccountId = konto != null ? DynamicPropertyHelper.GetId(konto) : null,
            IntegrationAccountName = konto != null ? DynamicPropertyHelper.GetString(konto, "Nazwa") : null,
            BuyerId = nabywca != null ? DynamicPropertyHelper.GetId(nabywca) : null,
            BuyerName = nabywca != null
                ? (DynamicPropertyHelper.GetString(nabywca, "NazwaSkrocona") ?? DynamicPropertyHelper.GetString(nabywca, "Nazwa"))
                : null,
            BuyerLogin = DynamicPropertyHelper.GetString(o, "NazwaUzytkownikaWSerwisie"),
            Email = DynamicPropertyHelper.GetString(o, "Email"),
            Phone = DynamicPropertyHelper.GetString(o, "Telefon"),
            Currency = DynamicPropertyHelper.GetString(o, "Waluta", "Symbol"),
            Value = DynamicPropertyHelper.GetDecimal(o, "Wartosc"),
            ShippingValue = DynamicPropertyHelper.GetDecimal(o, "WartoscTransportu"),
            RemainingToPay = DynamicPropertyHelper.GetNullableDecimal(o, "PozostaloDoZaplaty"),
            IsCashOnDelivery = DynamicPropertyHelper.GetBool(o, "Pobranie"),
            BuyerWantsInvoice = DynamicPropertyHelper.GetBool(o, "KupujacyChceFakture"),
            BuyerRemarks = DynamicPropertyHelper.GetString(o, "UwagiKupujacego"),
            Weight = DynamicPropertyHelper.GetNullableDecimal(o, "Masa"),
            DeliveryMethodName = DynamicPropertyHelper.GetString(o, "NazwaSposobuDostawyZSerwisu"),
            RecipientName = string.IsNullOrWhiteSpace(recipientName) ? null : recipientName,
            RecipientCompany = DynamicPropertyHelper.GetString(o, "FirmaOdbiorcy"),
            RecipientStreet = DynamicPropertyHelper.GetString(o, "UlicaOdbiorcy"),
            RecipientBuildingNumber = string.IsNullOrWhiteSpace(buildingNumber) ? null : buildingNumber,
            RecipientPostalCode = DynamicPropertyHelper.GetString(o, "KodPocztowyOdbiorcy"),
            RecipientCity = DynamicPropertyHelper.GetString(o, "MiejscowoscOdbiorcy"),
            RecipientCountry = DynamicPropertyHelper.GetString(o, "PanstwoOdbiorcy"),
            PackageCount = DynamicPropertyHelper.GetCount(o, "Paczki"),
            LineCount = DynamicPropertyHelper.GetCount(o, "Pozycje"),
            Items = new List<ShippingOrderItemDto>()
        };

        if (!includeItems)
            return dto;

        var lineNumber = 1;
        foreach (var poz in DynamicPropertyHelper.GetCollection(o, "Pozycje"))
        {
            dto.Items.Add(MapShippingOrderItem(poz, lineNumber++));
        }

        return dto;
    }

    private static ShippingOrderItemDto MapShippingOrderItem(object poz, int fallbackLineNumber)
    {
        object? asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
        object? jednostka = DynamicPropertyHelper.GetProperty(poz, "JednostkaMiary");
        object? stawkaVat = DynamicPropertyHelper.GetProperty(poz, "StawkaVat");

        return new ShippingOrderItemDto
        {
            Id = DynamicPropertyHelper.GetId(poz),
            LineNumber = DynamicPropertyHelper.GetNullableInt(poz, "Lp") ?? fallbackLineNumber,
            ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : null,
            ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
            ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
            Name = DynamicPropertyHelper.GetString(poz, "Nazwa") ?? "",
            ExternalId = DynamicPropertyHelper.GetString(poz, "IdZewnetrzny"),
            ExternalOfferId = DynamicPropertyHelper.GetString(poz, "IdZewnetrznyOferty"),
            Sku = DynamicPropertyHelper.GetString(poz, "SKU"),
            Gtin = DynamicPropertyHelper.GetString(poz, "GTIN"),
            Quantity = DynamicPropertyHelper.GetDecimal(poz, "Ilosc"),
            Unit = jednostka != null ? DynamicPropertyHelper.GetString(jednostka, "Symbol") : null,
            Price = DynamicPropertyHelper.GetDecimal(poz, "Cena"),
            PriceNet = DynamicPropertyHelper.GetNullableDecimal(poz, "CenaNetto"),
            Value = DynamicPropertyHelper.GetDecimal(poz, "Wartosc"),
            ValueNet = DynamicPropertyHelper.GetNullableDecimal(poz, "WartoscNetto"),
            VatRate = stawkaVat != null ? DynamicPropertyHelper.GetString(stawkaVat, "Symbol") : null,
            VatRateId = DynamicPropertyHelper.GetNullableInt(poz, "StawkaVatId"),
            UnitWeight = DynamicPropertyHelper.GetNullableDecimal(poz, "MasaJednostkowa"),
            Weight = DynamicPropertyHelper.GetNullableDecimal(poz, "Masa"),
            // SDK 60.0.0: PozycjaZamowieniaWysylkowego.WartoscKaucji/WalutaKaucji
            DepositValue = DynamicPropertyHelper.GetNullableDecimal(poz, "WartoscKaucji"),
            DepositCurrency = DynamicPropertyHelper.GetString(poz, "WalutaKaucji", "Symbol"),
            // SDK 61.1.0: PozycjaZamowieniaWysylkowego.KodTaryfyCelnej (null on older SDKs)
            CustomsTariffCode = DynamicPropertyHelper.GetString(poz, "KodTaryfyCelnej")
        };
    }

    #endregion

    #region Package Dimensions

    /// <summary>
    /// Get predefined package dimension templates
    /// </summary>
    [HttpGet("package-dimensions")]
    [ProducesResponseType(typeof(ApiResponse<List<PackageDimensionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<PackageDimensionDto>>>> GetPackageDimensions()
    {
        try
        {
            var items = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("GabarytyPaczki");
                if (manager == null)
                    return null;

                var allDimensions = DynamicPropertyHelper.SafeGetAll((object)manager);
                var result = new List<PackageDimensionDto>();

                foreach (var d in allDimensions)
                {
                    result.Add(new PackageDimensionDto
                    {
                        Id = DynamicPropertyHelper.GetId(d),
                        Name = DynamicPropertyHelper.GetString(d, "Nazwa") ?? "",
                        Width = DynamicPropertyHelper.GetDecimal(d, "Szerokosc"),
                        Height = DynamicPropertyHelper.GetDecimal(d, "Wysokosc"),
                        Depth = DynamicPropertyHelper.GetDecimal(d, "Glebokosc"),
                        MaxWeight = DynamicPropertyHelper.GetDecimal(d, "MaksymalnaWaga"),
                        IsDefault = DynamicPropertyHelper.GetBool(d, "Domyslny"),
                        // SDK 60.0.0: GabarytPaczki.Kolejnosc
                        SortOrder = DynamicPropertyHelper.GetInt(d, "Kolejnosc")
                    });
                }

                return result;
            });

            if (items == null)
                return StatusCode(500, ApiResponse<List<PackageDimensionDto>>.Error("Failed to get GabarytyPaczki manager"));

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

    /// <summary>
    /// Courier pickup address (SDK 60.0.0: ListaWysylkowa.AdresPodjazduKuriera)
    /// </summary>
    public string? CourierPickupAddress { get; set; }
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
/// E-commerce shipping order (ZamowienieWysylkowe) DTO
/// </summary>
public class ShippingOrderDto
{
    public int Id { get; set; }

    /// <summary>External (marketplace/shop) order identifier</summary>
    public string? ExternalId { get; set; }
    public string? Number { get; set; }
    public string? Signature { get; set; }
    public string? Title { get; set; }

    /// <summary>RodzajZamowieniaWysylkowego: Normalne / TylkoWysylki / TylkoDokumenty</summary>
    public string? Kind { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public int? StatusId { get; set; }
    public string? Status { get; set; }
    public string? ServiceStatus { get; set; }
    public string? ServicePaymentStatus { get; set; }
    public int? IntegrationAccountId { get; set; }
    public string? IntegrationAccountName { get; set; }
    public int? BuyerId { get; set; }
    public string? BuyerName { get; set; }
    public string? BuyerLogin { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Currency { get; set; }
    public decimal Value { get; set; }
    public decimal ShippingValue { get; set; }
    public decimal? RemainingToPay { get; set; }
    public bool IsCashOnDelivery { get; set; }
    public bool BuyerWantsInvoice { get; set; }
    public string? BuyerRemarks { get; set; }
    public decimal? Weight { get; set; }
    public string? DeliveryMethodName { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientCompany { get; set; }
    public string? RecipientStreet { get; set; }
    public string? RecipientBuildingNumber { get; set; }
    public string? RecipientPostalCode { get; set; }
    public string? RecipientCity { get; set; }
    public string? RecipientCountry { get; set; }
    public int PackageCount { get; set; }
    public int LineCount { get; set; }

    /// <summary>Line items (populated only by GET shipping-orders/{id})</summary>
    public List<ShippingOrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// E-commerce shipping order line (PozycjaZamowieniaWysylkowego) DTO
/// </summary>
public class ShippingOrderItemDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public int? ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string? ExternalOfferId { get; set; }
    public string? Sku { get; set; }
    public string? Gtin { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }

    /// <summary>Unit price as sent by the service (PozycjaZamowieniaWysylkowego.Cena)</summary>
    public decimal Price { get; set; }
    public decimal? PriceNet { get; set; }

    /// <summary>Line value as sent by the service (PozycjaZamowieniaWysylkowego.Wartosc)</summary>
    public decimal Value { get; set; }
    public decimal? ValueNet { get; set; }
    public string? VatRate { get; set; }
    public int? VatRateId { get; set; }
    public decimal? UnitWeight { get; set; }
    public decimal? Weight { get; set; }

    /// <summary>Deposit (kaucja) value (SDK 60.0.0: PozycjaZamowieniaWysylkowego.WartoscKaucji)</summary>
    public decimal? DepositValue { get; set; }

    /// <summary>Deposit currency symbol (SDK 60.0.0: PozycjaZamowieniaWysylkowego.WalutaKaucji)</summary>
    public string? DepositCurrency { get; set; }

    /// <summary>Customs tariff (CN/HS) code (SDK 61.1.0: PozycjaZamowieniaWysylkowego.KodTaryfyCelnej); null on older SDKs</summary>
    public string? CustomsTariffCode { get; set; }
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

    /// <summary>
    /// Sort order (SDK 60.0.0: GabarytPaczki.Kolejnosc)
    /// </summary>
    public int SortOrder { get; set; }
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
