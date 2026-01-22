using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Moria.Asortymenty;

namespace NexoSferaApi.Controllers;

[ApiController]
[Route("api/warehouse-documents")]
public class WarehouseDocumentsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<WarehouseDocumentsController> _logger;

    public WarehouseDocumentsController(ISferaService sferaService, ILogger<WarehouseDocumentsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get warehouse documents with filtering
    /// </summary>
    [HttpGet]
    public ActionResult<PagedResponse<WarehouseDocumentDto>> GetWarehouseDocuments([FromQuery] WarehouseDocumentQueryRequest query)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var documents = new List<WarehouseDocumentDto>();
            var totalCount = 0;

            // Query WZ documents
            if (!query.Type.HasValue || query.Type == WarehouseDocumentType.WZ)
            {
                var wz = sfera.WydaniaZewnetrzne();
                var wzQuery = wz.Dane.Wszystkie();

                if (!string.IsNullOrEmpty(query.WarehouseSymbol))
                    wzQuery = wzQuery.Where(d => d.Magazyn != null && d.Magazyn.Symbol == query.WarehouseSymbol);

                if (query.DateFrom.HasValue)
                    wzQuery = wzQuery.Where(d => d.DataWystawienia >= query.DateFrom.Value);

                if (query.DateTo.HasValue)
                    wzQuery = wzQuery.Where(d => d.DataWystawienia <= query.DateTo.Value);

                totalCount += wzQuery.Count();
                var wzDocs = wzQuery.OrderByDescending(d => d.DataWystawienia).Take(query.PageSize).ToList();
                documents.AddRange(wzDocs.Select(d => MapWZToDto(d)));
            }

            // Query PZ documents
            if (!query.Type.HasValue || query.Type == WarehouseDocumentType.PZ)
            {
                var pz = sfera.PrzyjeciaZewnetrzne();
                var pzQuery = pz.Dane.Wszystkie();

                if (!string.IsNullOrEmpty(query.WarehouseSymbol))
                    pzQuery = pzQuery.Where(d => d.Magazyn != null && d.Magazyn.Symbol == query.WarehouseSymbol);

                if (query.DateFrom.HasValue)
                    pzQuery = pzQuery.Where(d => d.DataWystawienia >= query.DateFrom.Value);

                if (query.DateTo.HasValue)
                    pzQuery = pzQuery.Where(d => d.DataWystawienia <= query.DateTo.Value);

                totalCount += pzQuery.Count();
                var pzDocs = pzQuery.OrderByDescending(d => d.DataWystawienia).Take(query.PageSize).ToList();
                documents.AddRange(pzDocs.Select(d => MapPZToDto(d)));
            }

            var response = new PagedResponse<WarehouseDocumentDto>
            {
                Data = documents.OrderByDescending(d => d.IssueDate).Take(query.PageSize).ToList(),
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting warehouse documents");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving warehouse documents", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create external release (WZ - Wydanie zewnętrzne)
    /// </summary>
    [HttpPost("wz")]
    public ActionResult<ApiResponse<WarehouseDocumentDto>> CreateWZ([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var wydania = sfera.WydaniaZewnetrzne();
            var konfiguracja = sfera.Konfiguracje().DaneDomyslne.WydanieZewnetrzne;

            using (var wz = wydania.Utworz(konfiguracja))
            {
                // Set contractor
                SetContractor(sfera, wz.Dane, request.ContractorId, request.ContractorNIP);

                // Set warehouse
                var magazyn = sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == request.WarehouseSymbol);
                if (magazyn != null)
                {
                    wz.Dane.Magazyn = magazyn;
                }

                if (request.IssueDate.HasValue)
                {
                    wz.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    wz.Dane.Uwagi = request.Notes;
                }

                // Add items
                AddWarehouseDocumentItems(sfera, wz, request.Items);

                if (wz.Zapisz())
                {
                    _logger.LogInformation("Created WZ {Number}", wz.Dane.NumerWewnetrzny?.PelnaSygnatura);

                    return CreatedAtAction(
                        nameof(GetWarehouseDocuments),
                        ApiResponse<WarehouseDocumentDto>.Ok(MapWZToDto(wz.Dane), "WZ created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(wz);
                    return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Failed to create WZ", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating WZ");
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error creating WZ", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create external receipt (PZ - Przyjęcie zewnętrzne)
    /// </summary>
    [HttpPost("pz")]
    public ActionResult<ApiResponse<WarehouseDocumentDto>> CreatePZ([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var przyjecia = sfera.PrzyjeciaZewnetrzne();
            var konfiguracja = sfera.Konfiguracje().DaneDomyslne.PrzyjecieZewnetrzne;

            using (var pz = przyjecia.Utworz(konfiguracja))
            {
                // Set contractor (supplier)
                SetContractor(sfera, pz.Dane, request.ContractorId, request.ContractorNIP);

                // Set warehouse
                var magazyn = sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == request.WarehouseSymbol);
                if (magazyn != null)
                {
                    pz.Dane.Magazyn = magazyn;
                }

                if (request.IssueDate.HasValue)
                {
                    pz.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (!string.IsNullOrEmpty(request.RelatedDocumentNumber))
                {
                    pz.Dane.NumerObcy = request.RelatedDocumentNumber;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    pz.Dane.Uwagi = request.Notes;
                }

                // Add items
                AddWarehouseDocumentItems(sfera, pz, request.Items);

                if (pz.Zapisz())
                {
                    _logger.LogInformation("Created PZ {Number}", pz.Dane.NumerWewnetrzny?.PelnaSygnatura);

                    return CreatedAtAction(
                        nameof(GetWarehouseDocuments),
                        ApiResponse<WarehouseDocumentDto>.Ok(MapPZToDto(pz.Dane), "PZ created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(pz);
                    return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Failed to create PZ", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PZ");
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error creating PZ", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create internal consumption (RW - Rozchód wewnętrzny)
    /// </summary>
    [HttpPost("rw")]
    public ActionResult<ApiResponse<WarehouseDocumentDto>> CreateRW([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var wydania = sfera.WydaniaZewnetrzne();
            var konfiguracja = sfera.Konfiguracje().DaneDomyslne.RozchodWewnetrzny;

            using (var rw = wydania.Utworz(konfiguracja))
            {
                // Set warehouse
                var magazyn = sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == request.WarehouseSymbol);
                if (magazyn != null)
                {
                    rw.Dane.Magazyn = magazyn;
                }

                if (request.IssueDate.HasValue)
                {
                    rw.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    rw.Dane.Uwagi = request.Notes;
                }

                // Add items
                AddWarehouseDocumentItems(sfera, rw, request.Items);

                if (rw.Zapisz())
                {
                    _logger.LogInformation("Created RW {Number}", rw.Dane.NumerWewnetrzny?.PelnaSygnatura);

                    return CreatedAtAction(
                        nameof(GetWarehouseDocuments),
                        ApiResponse<WarehouseDocumentDto>.Ok(MapWZToDto(rw.Dane), "RW created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(rw);
                    return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Failed to create RW", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating RW");
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error creating RW", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create internal receipt (PW - Przychód wewnętrzny)
    /// </summary>
    [HttpPost("pw")]
    public ActionResult<ApiResponse<WarehouseDocumentDto>> CreatePW([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var przyjecia = sfera.PrzyjeciaZewnetrzne();
            var konfiguracja = sfera.Konfiguracje().DaneDomyslne.PrzychodWewnetrzny;

            using (var pw = przyjecia.Utworz(konfiguracja))
            {
                // Set warehouse
                var magazyn = sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == request.WarehouseSymbol);
                if (magazyn != null)
                {
                    pw.Dane.Magazyn = magazyn;
                }

                if (request.IssueDate.HasValue)
                {
                    pw.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    pw.Dane.Uwagi = request.Notes;
                }

                // Add items
                AddWarehouseDocumentItems(sfera, pw, request.Items);

                if (pw.Zapisz())
                {
                    _logger.LogInformation("Created PW {Number}", pw.Dane.NumerWewnetrzny?.PelnaSygnatura);

                    return CreatedAtAction(
                        nameof(GetWarehouseDocuments),
                        ApiResponse<WarehouseDocumentDto>.Ok(MapPZToDto(pw.Dane), "PW created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(pw);
                    return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Failed to create PW", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PW");
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error creating PW", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create inter-warehouse transfer (MM - Przesunięcie międzymagazynowe)
    /// </summary>
    [HttpPost("mm")]
    public ActionResult<ApiResponse<WarehouseDocumentDto>> CreateMM([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.TargetWarehouseSymbol))
            {
                return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Target warehouse symbol is required for MM"));
            }

            var sfera = _sferaService.GetSfera();
            var wydania = sfera.WydaniaMiedzymagazynowe();
            var konfiguracja = sfera.Konfiguracje().DaneDomyslne.PrzesuniecieMiedzymagazynowe;

            using (var mm = wydania.Utworz(konfiguracja))
            {
                // Set source warehouse
                var magazynZrodlowy = sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == request.WarehouseSymbol);
                if (magazynZrodlowy != null)
                {
                    mm.Dane.Magazyn = magazynZrodlowy;
                }

                // Set target warehouse
                var magazynDocelowy = sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == request.TargetWarehouseSymbol);
                if (magazynDocelowy != null)
                {
                    mm.Dane.MagazynDocelowy = magazynDocelowy;
                }

                if (request.IssueDate.HasValue)
                {
                    mm.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    mm.Dane.Uwagi = request.Notes;
                }

                // Add items
                var asortymenty = sfera.Asortymenty();
                foreach (var item in request.Items)
                {
                    Asortyment? asortyment = GetAsortyment(asortymenty, item.ProductId, item.ProductSymbol, item.ProductEan);

                    if (asortyment != null)
                    {
                        mm.Pozycje.Dodaj(asortyment, item.Quantity, asortyment.JednostkaSprzedazy);
                    }
                }

                if (mm.Zapisz())
                {
                    _logger.LogInformation("Created MM {Number}", mm.Dane.NumerWewnetrzny?.PelnaSygnatura);

                    return CreatedAtAction(
                        nameof(GetWarehouseDocuments),
                        ApiResponse<WarehouseDocumentDto>.Ok(MapMMToDto(mm.Dane), "MM created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(mm);
                    return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Failed to create MM", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating MM");
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error creating MM", new List<string> { ex.Message }));
        }
    }

    private void SetContractor(Uchwyt sfera, dynamic dokumentDane, int? contractorId, string? contractorNIP)
    {
        if (contractorId.HasValue)
        {
            var podmiot = sfera.Podmioty().Dane.Wszystkie()
                .FirstOrDefault(p => p.Id == contractorId.Value);
            if (podmiot != null)
            {
                dokumentDane.Podmiot = podmiot;
            }
        }
        else if (!string.IsNullOrEmpty(contractorNIP))
        {
            var podmiot = sfera.Podmioty().Dane.Pierwszy(p => p.NIP == contractorNIP);
            if (podmiot != null)
            {
                dokumentDane.Podmiot = podmiot;
            }
        }
    }

    private void AddWarehouseDocumentItems(Uchwyt sfera, dynamic dokument, List<CreateWarehouseDocumentItemRequest> items)
    {
        var asortymenty = sfera.Asortymenty();
        foreach (var item in items)
        {
            Asortyment? asortyment = GetAsortyment(asortymenty, item.ProductId, item.ProductSymbol, item.ProductEan);

            if (asortyment != null)
            {
                var pozycja = dokument.Pozycje.Dodaj(asortyment, item.Quantity, asortyment.JednostkaSprzedazy);

                if (item.PriceNet.HasValue && pozycja != null)
                {
                    pozycja.Dane.CenaNetto = item.PriceNet.Value;
                }
            }
        }
    }

    private static Asortyment? GetAsortyment(IAsortymenty asortymenty, int? productId, string? productSymbol, string? productEan)
    {
        if (productId.HasValue)
        {
            return asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.Id == productId.Value);
        }
        else if (!string.IsNullOrEmpty(productSymbol))
        {
            return asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.Symbol == productSymbol);
        }
        else if (!string.IsNullOrEmpty(productEan))
        {
            return asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.KodKreskowy == productEan);
        }
        return null;
    }

    private static WarehouseDocumentDto MapWZToDto(DokumentWZ dokument)
    {
        var dto = new WarehouseDocumentDto
        {
            Id = dokument.Id,
            Number = dokument.NumerWewnetrzny?.Numer.ToString() ?? "",
            FullNumber = dokument.NumerWewnetrzny?.PelnaSygnatura,
            Type = WarehouseDocumentType.WZ,
            IssueDate = dokument.DataWystawienia,
            ContractorName = dokument.Podmiot?.NazwaSkrocona,
            ContractorNIP = dokument.Podmiot?.NIP,
            WarehouseSymbol = dokument.Magazyn?.Symbol,
            WarehouseName = dokument.Magazyn?.Nazwa,
            TotalNet = dokument.WartoscNetto,
            TotalGross = dokument.WartoscBrutto,
            Notes = dokument.Uwagi,
            CreatedAt = dokument.DataUtworzenia,
            Items = new List<WarehouseDocumentItemDto>()
        };

        if (dokument.Pozycje != null)
        {
            int lineNum = 1;
            foreach (var poz in dokument.Pozycje)
            {
                dto.TotalQuantity += poz.Ilosc;
                dto.Items.Add(new WarehouseDocumentItemDto
                {
                    Id = poz.Id,
                    LineNumber = lineNum++,
                    ProductId = poz.Asortyment?.Id,
                    ProductSymbol = poz.Asortyment?.Symbol,
                    Name = poz.Nazwa,
                    Quantity = poz.Ilosc,
                    Unit = poz.Jednostka?.Symbol ?? "szt.",
                    PriceNet = poz.CenaNetto,
                    ValueNet = poz.WartoscNetto,
                    ValueGross = poz.WartoscBrutto
                });
            }
        }

        return dto;
    }

    private static WarehouseDocumentDto MapPZToDto(DokumentPZ dokument)
    {
        var dto = new WarehouseDocumentDto
        {
            Id = dokument.Id,
            Number = dokument.NumerWewnetrzny?.Numer.ToString() ?? "",
            FullNumber = dokument.NumerWewnetrzny?.PelnaSygnatura,
            Type = WarehouseDocumentType.PZ,
            IssueDate = dokument.DataWystawienia,
            ContractorName = dokument.Podmiot?.NazwaSkrocona,
            ContractorNIP = dokument.Podmiot?.NIP,
            WarehouseSymbol = dokument.Magazyn?.Symbol,
            WarehouseName = dokument.Magazyn?.Nazwa,
            RelatedDocumentNumber = dokument.NumerObcy,
            TotalNet = dokument.WartoscNetto,
            TotalGross = dokument.WartoscBrutto,
            Notes = dokument.Uwagi,
            CreatedAt = dokument.DataUtworzenia,
            Items = new List<WarehouseDocumentItemDto>()
        };

        if (dokument.Pozycje != null)
        {
            int lineNum = 1;
            foreach (var poz in dokument.Pozycje)
            {
                dto.TotalQuantity += poz.Ilosc;
                dto.Items.Add(new WarehouseDocumentItemDto
                {
                    Id = poz.Id,
                    LineNumber = lineNum++,
                    ProductId = poz.Asortyment?.Id,
                    ProductSymbol = poz.Asortyment?.Symbol,
                    Name = poz.Nazwa,
                    Quantity = poz.Ilosc,
                    Unit = poz.Jednostka?.Symbol ?? "szt.",
                    PriceNet = poz.CenaNetto,
                    ValueNet = poz.WartoscNetto,
                    ValueGross = poz.WartoscBrutto
                });
            }
        }

        return dto;
    }

    private static WarehouseDocumentDto MapMMToDto(DokumentMM dokument)
    {
        var dto = new WarehouseDocumentDto
        {
            Id = dokument.Id,
            Number = dokument.NumerWewnetrzny?.Numer.ToString() ?? "",
            FullNumber = dokument.NumerWewnetrzny?.PelnaSygnatura,
            Type = WarehouseDocumentType.MM,
            IssueDate = dokument.DataWystawienia,
            WarehouseSymbol = dokument.Magazyn?.Symbol,
            WarehouseName = dokument.Magazyn?.Nazwa,
            TargetWarehouseSymbol = dokument.MagazynDocelowy?.Symbol,
            TargetWarehouseName = dokument.MagazynDocelowy?.Nazwa,
            Notes = dokument.Uwagi,
            CreatedAt = dokument.DataUtworzenia,
            Items = new List<WarehouseDocumentItemDto>()
        };

        if (dokument.Pozycje != null)
        {
            int lineNum = 1;
            foreach (var poz in dokument.Pozycje)
            {
                dto.TotalQuantity += poz.Ilosc;
                dto.Items.Add(new WarehouseDocumentItemDto
                {
                    Id = poz.Id,
                    LineNumber = lineNum++,
                    ProductId = poz.Asortyment?.Id,
                    ProductSymbol = poz.Asortyment?.Symbol,
                    Name = poz.Nazwa,
                    Quantity = poz.Ilosc,
                    Unit = poz.Jednostka?.Symbol ?? "szt."
                });
            }
        }

        return dto;
    }

    private static List<string> GetBusinessObjectErrors(InsERT.Mox.ObiektyBiznesowe.IObiektBiznesowy obiekt)
    {
        var errors = new List<string>();
        foreach (var encjaZBledami in obiekt.InvalidData)
        {
            foreach (var blad in encjaZBledami.Errors)
            {
                errors.Add(blad.ToString());
            }
            foreach (var bladNaPolach in encjaZBledami.MemberErrors)
            {
                errors.Add($"{bladNaPolach.Key}: {string.Join(", ", bladNaPolach)}");
            }
        }
        return errors;
    }
}
