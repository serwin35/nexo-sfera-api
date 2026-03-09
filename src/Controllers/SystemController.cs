using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// System information endpoints (Company info, Operator info, Context)
/// </summary>
[ApiController]
[Route("api/system")]
[Authorize]
[Tags("System")]
public class SystemController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<SystemController> _logger;

    public SystemController(ISferaService sferaService, ILogger<SystemController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Company Info

    /// <summary>
    /// Get company information
    /// </summary>
    [HttpGet("company")]
    [ProducesResponseType(typeof(ApiResponse<CompanyInfoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CompanyInfoDto>>> GetCompanyInfo()
    {
        try
        {
            var dto = await _sferaService.ExecuteWithLockAsync(() =>
            {
                dynamic sfera = _sferaService.GetSfera();

                // Try to get company info from Sfera
                dynamic? firma = null;
                try
                {
                    firma = sfera.PodajObiektTypu<dynamic>();
                }
                catch { }

                // Fallback: try to get from Kontekst
                dynamic? kontekst = null;
                try
                {
                    var kontekstProp = sfera.GetType().GetProperty("Kontekst");
                    if (kontekstProp != null)
                    {
                        kontekst = kontekstProp.GetValue(sfera);
                    }
                }
                catch { }

                // Try to get from InformacjeOFirmie
                dynamic? infoOFirmie = null;
                try
                {
                    var infoProp = sfera.GetType().GetProperty("InformacjeOFirmie");
                    if (infoProp != null)
                    {
                        infoOFirmie = infoProp.GetValue(sfera);
                    }
                }
                catch { }

                var result = new CompanyInfoDto();

                if (infoOFirmie != null)
                {
                    result.Name = DynamicPropertyHelper.GetString(infoOFirmie, "Nazwa");
                    result.ShortName = DynamicPropertyHelper.GetString(infoOFirmie, "NazwaSkrocona");
                    result.TaxId = DynamicPropertyHelper.GetString(infoOFirmie, "NIP");
                    result.Regon = DynamicPropertyHelper.GetString(infoOFirmie, "Regon");
                    result.Street = DynamicPropertyHelper.GetString(infoOFirmie, "Ulica");
                    result.City = DynamicPropertyHelper.GetString(infoOFirmie, "Miejscowosc");
                    result.PostalCode = DynamicPropertyHelper.GetString(infoOFirmie, "KodPocztowy");
                    result.Country = DynamicPropertyHelper.GetString(infoOFirmie, "Kraj");
                    result.Phone = DynamicPropertyHelper.GetString(infoOFirmie, "Telefon");
                    result.Email = DynamicPropertyHelper.GetString(infoOFirmie, "Email");
                    result.Website = DynamicPropertyHelper.GetString(infoOFirmie, "WWW");
                    result.BankAccount = DynamicPropertyHelper.GetString(infoOFirmie, "NumerRachunkuBankowego");
                    result.BankName = DynamicPropertyHelper.GetString(infoOFirmie, "NazwaBanku");
                }
                else if (kontekst != null)
                {
                    // Try getting from context
                    var firmaProp = DynamicPropertyHelper.GetProperty(kontekst, "Firma");
                    if (firmaProp != null)
                    {
                        result.Name = DynamicPropertyHelper.GetString(firmaProp, "Nazwa");
                        result.ShortName = DynamicPropertyHelper.GetString(firmaProp, "NazwaSkrocona");
                        result.TaxId = DynamicPropertyHelper.GetString(firmaProp, "NIP");
                    }
                }

                // If still empty, try to get from database name or connection info
                if (string.IsNullOrEmpty(result.Name))
                {
                    try
                    {
                        var nazwaFirmyProp = sfera.GetType().GetProperty("NazwaFirmy");
                        if (nazwaFirmyProp != null)
                        {
                            result.Name = nazwaFirmyProp.GetValue(sfera)?.ToString();
                        }
                    }
                    catch { }
                }

                return result;
            });

            return Ok(ApiResponse<CompanyInfoDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company info");
            return StatusCode(500, ApiResponse<CompanyInfoDto>.Error("Error retrieving company info", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Operator Info

    /// <summary>
    /// Get current operator information
    /// </summary>
    [HttpGet("operator")]
    [ProducesResponseType(typeof(ApiResponse<OperatorInfoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<OperatorInfoDto>>> GetOperatorInfo()
    {
        try
        {
            var dto = await _sferaService.ExecuteWithLockAsync(() =>
            {
                dynamic sfera = _sferaService.GetSfera();

                var result = new OperatorInfoDto();

                // Try to get from Operator property
                try
                {
                    var operatorProp = sfera.GetType().GetProperty("Operator");
                    if (operatorProp != null)
                    {
                        dynamic? oper = operatorProp.GetValue(sfera);
                        if (oper != null)
                        {
                            result.Id = DynamicPropertyHelper.GetId(oper);
                            result.Login = DynamicPropertyHelper.GetString(oper, "Login");
                            result.Name = DynamicPropertyHelper.GetString(oper, "Nazwa");
                            result.Email = DynamicPropertyHelper.GetString(oper, "Email");
                            result.IsActive = DynamicPropertyHelper.GetBool(oper, "Aktywny");
                            result.IsAdmin = DynamicPropertyHelper.GetBool(oper, "Administrator");
                        }
                    }
                }
                catch { }

                // Try alternative via ZalogowanyOperator
                if (string.IsNullOrEmpty(result.Login))
                {
                    try
                    {
                        var zalogowanyProp = sfera.GetType().GetProperty("ZalogowanyOperator");
                        if (zalogowanyProp != null)
                        {
                            dynamic? zalogowany = zalogowanyProp.GetValue(sfera);
                            if (zalogowany != null)
                            {
                                result.Id = DynamicPropertyHelper.GetId(zalogowany);
                                result.Login = DynamicPropertyHelper.GetString(zalogowany, "Login");
                                result.Name = DynamicPropertyHelper.GetString(zalogowany, "Nazwa");
                                result.Email = DynamicPropertyHelper.GetString(zalogowany, "Email");
                            }
                        }
                    }
                    catch { }
                }

                return result;
            });

            return Ok(ApiResponse<OperatorInfoDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting operator info");
            return StatusCode(500, ApiResponse<OperatorInfoDto>.Error("Error retrieving operator info", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Work Context

    /// <summary>
    /// Get current work context (database, session info)
    /// </summary>
    [HttpGet("context")]
    [ProducesResponseType(typeof(ApiResponse<WorkContextDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WorkContextDto>>> GetWorkContext()
    {
        try
        {
            var dto = await _sferaService.ExecuteWithLockAsync(() =>
            {
                dynamic sfera = _sferaService.GetSfera();

                var result = new WorkContextDto
                {
                    IsConnected = _sferaService.IsConnected,
                    SessionStartTime = DateTime.Now // Approximate
                };

                // Try to get database info
                try
                {
                    var danePol = sfera.GetType().GetProperty("DanePolaczenia");
                    if (danePol != null)
                    {
                        dynamic? dane = danePol.GetValue(sfera);
                        if (dane != null)
                        {
                            result.ServerName = DynamicPropertyHelper.GetString(dane, "Serwer");
                            result.DatabaseName = DynamicPropertyHelper.GetString(dane, "Baza");
                        }
                    }
                }
                catch { }

                // Try to get product info
                try
                {
                    var produktProp = sfera.GetType().GetProperty("Produkt");
                    if (produktProp != null)
                    {
                        var produkt = produktProp.GetValue(sfera);
                        result.ProductName = produkt?.ToString();
                    }
                }
                catch { }

                // Try to get version
                try
                {
                    var verProp = sfera.GetType().GetProperty("Wersja");
                    if (verProp != null)
                    {
                        var ver = verProp.GetValue(sfera);
                        result.Version = ver?.ToString();
                    }
                }
                catch { }

                return result;
            });

            return Ok(ApiResponse<WorkContextDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work context");
            return StatusCode(500, ApiResponse<WorkContextDto>.Error("Error retrieving work context", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Available Managers

    /// <summary>
    /// Get list of available managers/services
    /// </summary>
    [HttpGet("managers")]
    [ProducesResponseType(typeof(ApiResponse<List<ManagerInfoDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ManagerInfoDto>>>> GetAvailableManagers()
    {
        var managers = new List<ManagerInfoDto>
        {
            // Products
            new() { Name = "Asortymenty", Description = "Products/Items management", Category = "Products" },
            new() { Name = "SzablonyAsortymentu", Description = "Product templates", Category = "Products" },
            new() { Name = "GrupyAsortymentu", Description = "Product groups/categories", Category = "Products" },
            new() { Name = "CechyAsortymentu", Description = "Product attributes", Category = "Products" },

            // Customers
            new() { Name = "Podmioty", Description = "Customers/Contractors", Category = "Customers" },
            new() { Name = "GrupyKontrahentow", Description = "Customer groups", Category = "Customers" },

            // Documents
            new() { Name = "Dokumenty", Description = "All documents", Category = "Documents" },
            new() { Name = "DokumentySprzedazy", Description = "Sales documents", Category = "Documents" },
            new() { Name = "DokumentyZakupu", Description = "Purchase documents", Category = "Documents" },
            new() { Name = "KorektyDokumentowSprzedazy", Description = "Sales corrections", Category = "Documents" },
            new() { Name = "KorektyDokumentowZakupu", Description = "Purchase corrections", Category = "Documents" },
            new() { Name = "DokumentyElektroniczne", Description = "Electronic documents", Category = "Documents" },

            // Orders
            new() { Name = "ZamowieniaOdKlientow", Description = "Customer orders", Category = "Orders" },
            new() { Name = "ZamowieniaDoDostawcow", Description = "Supplier orders", Category = "Orders" },
            new() { Name = "Oferty", Description = "Offers/Quotes", Category = "Orders" },

            // Warehouse
            new() { Name = "Magazyny", Description = "Warehouses", Category = "Warehouse" },
            new() { Name = "WydaniaZewnetrzne", Description = "External issues (WZ)", Category = "Warehouse" },
            new() { Name = "PrzyjeciaZewnetrzne", Description = "External receipts (PZ)", Category = "Warehouse" },
            new() { Name = "WydaniaMiedzymagazynowe", Description = "Inter-warehouse transfers (MM)", Category = "Warehouse" },
            new() { Name = "RozchodyWewnetrzne", Description = "Internal issues (RW)", Category = "Warehouse" },

            // Dictionaries
            new() { Name = "StawkiVat", Description = "VAT rates", Category = "Dictionaries" },
            new() { Name = "JednostkiMiar", Description = "Units of measure", Category = "Dictionaries" },
            new() { Name = "Waluty", Description = "Currencies", Category = "Dictionaries" },
            new() { Name = "KursyWalut", Description = "Exchange rates", Category = "Dictionaries" },
            new() { Name = "FormyPlatnosci", Description = "Payment methods", Category = "Dictionaries" },

            // Pricing
            new() { Name = "Cenniki", Description = "Price lists", Category = "Pricing" },
            new() { Name = "PoziomyCen", Description = "Price levels", Category = "Pricing" },
            new() { Name = "Rabaty", Description = "Discounts", Category = "Pricing" },

            // Finance
            new() { Name = "OperacjeKasowe", Description = "Cash operations", Category = "Finance" },
            new() { Name = "OperacjeBankowe", Description = "Bank operations", Category = "Finance" },
            new() { Name = "Rozrachunki", Description = "Receivables/Payables", Category = "Finance" },
            new() { Name = "StanowiskaKasowe", Description = "Cash registers", Category = "Finance" },
            new() { Name = "RachunkiBankowe", Description = "Bank accounts", Category = "Finance" },
            new() { Name = "RaportyKasowe", Description = "Cash reports", Category = "Finance" },
            new() { Name = "WyciagiBankowe", Description = "Bank statements", Category = "Finance" },
            new() { Name = "RodzajeOperacjiKasowych", Description = "Cash operation types", Category = "Finance" },
            new() { Name = "RodzajeOperacjiBankowych", Description = "Bank operation types", Category = "Finance" }
        };

        // Test availability
        await _sferaService.ExecuteWithLockAsync(() =>
        {
            foreach (var manager in managers)
            {
                try
                {
                    var result = _sferaService.GetManager(manager.Name);
                    manager.IsAvailable = result != null;
                }
                catch
                {
                    manager.IsAvailable = false;
                }
            }
            return true;
        });

        return Ok(ApiResponse<List<ManagerInfoDto>>.Ok(managers));
    }

    #endregion
}

/// <summary>
/// Company information DTO
/// </summary>
public class CompanyInfoDto
{
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? TaxId { get; set; }
    public string? Regon { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }
}

/// <summary>
/// Operator information DTO
/// </summary>
public class OperatorInfoDto
{
    public int Id { get; set; }
    public string? Login { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
}

/// <summary>
/// Work context DTO
/// </summary>
public class WorkContextDto
{
    public bool IsConnected { get; set; }
    public string? ServerName { get; set; }
    public string? DatabaseName { get; set; }
    public string? ProductName { get; set; }
    public string? Version { get; set; }
    public DateTime SessionStartTime { get; set; }
}

/// <summary>
/// Manager info DTO
/// </summary>
public class ManagerInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsAvailable { get; set; }
}
