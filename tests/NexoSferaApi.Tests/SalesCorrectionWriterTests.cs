using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using NexoSferaApi.Helpers;
using NexoSferaApi.Models.Requests;
using Xunit;

namespace NexoSferaApi.Tests;

/// <summary>
/// A correction of an existing FS or PA corrects named positions of that document, all or nothing; a field the bridge
/// cannot apply is refused with its name.
/// </summary>
public sealed class SalesCorrectionWriterTests
{
    private static readonly SalesCorrectionWriter.OriginalLine Boots = new(74166, 1m, "RBPUDBS01375_43");
    private static readonly SalesCorrectionWriter.OriginalLine Patches = new(74167, 4m, "DMSUDKS00023_01GL");

    private static CreateCorrectionRequest Request(params CreateCorrectionItemRequest[] items) => new()
    {
        OriginalDocumentId = 1001,
        IssueDate = new DateTime(2024, 10, 16),
        Items = items.ToList(),
    };

    private static CreateCorrectionItemRequest Line(int? position, decimal change, string? symbol = null) => new()
    {
        OriginalPositionId = position,
        QuantityCorrection = change,
        ProductSymbol = symbol,
    };

    [Fact]
    public void AFullReturnTakesThePositionToZero()
    {
        var (plan, errors) = SalesCorrectionWriter.Plan(new[] { Boots, Patches }, new[] { Line(74166, -1m, "RBPUDBS01375_43") });

        Assert.Empty(errors);
        Assert.Equal(new[] { new SalesCorrectionWriter.PlannedLine(74166, 0m) }, plan);
    }

    [Fact]
    public void APartialReturnKeepsTheRest()
    {
        var (plan, errors) = SalesCorrectionWriter.Plan(new[] { Boots, Patches }, new[] { Line(74167, -3m) });

        Assert.Empty(errors);
        Assert.Equal(1m, Assert.Single(plan).QuantityAfter);
    }

    [Fact]
    public void ALineMustNameAPositionOfTheCorrectedDocument()
    {
        var (plan, errors) = SalesCorrectionWriter.Plan(new[] { Boots }, new[] { Line(99999, -1m) });

        Assert.Empty(plan);
        Assert.Contains("99999 is not a position of the corrected document", Assert.Single(errors));
    }

    [Fact]
    public void AStatedProductMustBeTheProductOfThePosition()
    {
        var (_, errors) = SalesCorrectionWriter.Plan(new[] { Boots }, new[] { Line(74166, -1m, "RBPUDBS01375_44") });

        Assert.Contains("differs from position 74166 (RBPUDBS01375_43)", Assert.Single(errors));
    }

    [Fact]
    public void AQuantityCannotGoBelowZero()
    {
        var (_, errors) = SalesCorrectionWriter.Plan(new[] { Boots }, new[] { Line(74166, -2m) });

        Assert.Contains("goes below zero", Assert.Single(errors));
    }

    [Fact]
    public void TheRequestNeedsLinesThatNamePositionsOnce()
    {
        Assert.Contains("at least one corrected line", Assert.Single(SalesCorrectionWriter.CheckRequest(Request())));

        var errors = SalesCorrectionWriter.CheckRequest(Request(Line(null, -1m), Line(74166, -1m), Line(74166, -1m), Line(74167, 0m)));

        Assert.Contains(errors, e => e.Contains("items[0].originalPositionId is required"));
        Assert.Contains(errors, e => e.Contains("items[2].originalPositionId 74166 is corrected twice"));
        Assert.Contains(errors, e => e.Contains("items[3].quantityCorrection is 0"));
    }

    [Fact]
    public void FieldsThisPathCannotApplyAreRefusedByName()
    {
        var request = Request(new CreateCorrectionItemRequest
        {
            OriginalPositionId = 74166, QuantityCorrection = -1m, PriceNetCorrection = 10m, Unit = "kpl", ItemCorrectionReason = "zwrot",
        });
        request.CorrectionReason = "Zwrot towaru";

        var errors = SalesCorrectionWriter.CheckRequest(request);

        Assert.Contains(errors, e => e.StartsWith("correctionReason"));
        Assert.Contains(errors, e => e.StartsWith("items[0].priceNetCorrection"));
        Assert.Contains(errors, e => e.StartsWith("items[0].unit"));
        Assert.Contains(errors, e => e.StartsWith("items[0].itemCorrectionReason"));
    }

    [Fact]
    public void AValidRequestPassesTheChecks()
    {
        Assert.Empty(SalesCorrectionWriter.CheckRequest(Request(Line(74166, -1m), Line(74167, -4m))));
    }

    [Fact]
    public void ARequestWithoutAReasonPassesModelValidation()
    {
        // Model validation runs before the action: a required reason refused every correction of an existing document,
        // since that path refuses a non-empty reason (found on the deployed bridge, 2026-11-19).
        var request = Request(Line(74166, -1m));
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            request, new System.ComponentModel.DataAnnotations.ValidationContext(request), results, validateAllProperties: true);

        Assert.True(valid, string.Join("; ", results.Select(r => r.ErrorMessage)));
        Assert.Empty(SalesCorrectionWriter.CheckRequest(request));
    }

    [Theory]
    [InlineData(typeof(CreateCorrectionRequest))]
    [InlineData(typeof(CreateCorrectionItemRequest))]
    public void EveryOptionalReferencePropertyIsNullable(Type model)
    {
        // MVC treats a non-nullable reference property as [Required] (Nullable is enabled), which DataAnnotations'
        // Validator does not: `string CorrectionReason = string.Empty` would pass the test above and still refuse every
        // typed correction with "The CorrectionReason field is required" (found on the deployed bridge, 2026-11-19).
        var context = new NullabilityInfoContext();
        var notNullable = model.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !p.PropertyType.IsValueType && p.Name != nameof(CreateCorrectionRequest.Items))
            .Where(p => context.Create(p) is var info &&
                        (info.ReadState != NullabilityState.Nullable || info.WriteState != NullabilityState.Nullable))
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(notNullable);
    }

    [Fact]
    public void FieldsTheCorrectedDocumentSetsAreRefusedByName()
    {
        var request = Request(Line(74166, -1m));
        request.OriginalDocumentNumber = "PA 2024/06/10";
        request.CustomerId = 7;
        request.CustomerNIP = "9512473321";
        request.WarehouseSymbol = "MG";
        request.ReserveNumber = true;

        var errors = SalesCorrectionWriter.CheckRequest(request);

        foreach (var field in new[] { "originalDocumentNumber", "customerId", "customerNIP", "warehouseSymbol", "reserveNumber" })
        {
            Assert.Contains(errors, e => e.StartsWith(field + ":"));
        }

        Assert.Equal(5, errors.Count);
    }

    [Fact]
    public void AStatedProductIdMustBeTheProductOfThePosition()
    {
        var boots = Boots with { ProductId = 501 };
        var line = new CreateCorrectionItemRequest { OriginalPositionId = 74166, QuantityCorrection = -1m, ProductId = 502 };

        var (plan, errors) = SalesCorrectionWriter.Plan(new[] { boots }, new[] { line });

        Assert.Empty(plan);
        Assert.Contains("items[0].productId 502 differs from position 74166 (product 501)", Assert.Single(errors));

        line.ProductId = 501;
        Assert.Empty(SalesCorrectionWriter.Plan(new[] { boots }, new[] { line }).Errors);
    }

    [Fact]
    public void AStatedProductIdCannotMatchAPositionWithoutAProduct()
    {
        var line = new CreateCorrectionItemRequest { OriginalPositionId = 74166, QuantityCorrection = -1m, ProductId = 501 };

        var (_, errors) = SalesCorrectionWriter.Plan(new[] { Boots }, new[] { line });

        Assert.Contains("(no product)", Assert.Single(errors));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("Zwrot towaru", false)]
    public void TheOldPathStillNeedsAReason(string? reason, bool refused)
    {
        var request = new CreateCorrectionRequest { CorrectionReason = reason, Items = { Line(null, -1m, "SYM") } };

        Assert.Equal(refused ? SalesCorrectionWriter.ReasonRequired : null, SalesCorrectionWriter.MissingReason(request));
    }

    [Theory]
    [InlineData(SalesCorrectionWriter.Kind.InvoiceCorrection, "FS")]
    [InlineData(SalesCorrectionWriter.Kind.ReceiptReturn, "PA")]
    public void EachKindCorrectsItsOwnDocumentType(SalesCorrectionWriter.Kind kind, string symbol)
    {
        Assert.Equal(symbol, SalesCorrectionWriter.CorrectedSymbol(kind));
    }
}

/// <summary>The request checks of a correction of an existing document run before the SDK is touched.</summary>
public sealed class SalesCorrectionContractTests(NexoApiFactory factory) : IClassFixture<NexoApiFactory>
{
    private readonly HttpClient _client = CreateAuthenticatedClient(factory);

    [Theory]
    [InlineData("/api/documents/sales-invoice-correction")]
    [InlineData("/api/documents/receipt-return")]
    public async Task ALineWithoutAPositionIsRefusedWithItsName(string path)
    {
        var response = await _client.PostAsync(path, Json(
            "{\"originalDocumentId\":1001,\"issueDate\":\"2024-10-16\",\"items\":[{\"productSymbol\":\"SYM\",\"quantityCorrection\":-1}]}"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("items[0].originalPositionId is required", body);
    }

    [Theory]
    [InlineData("/api/documents/sales-invoice-correction")]
    [InlineData("/api/documents/receipt-return")]
    [InlineData("/api/documents/purchase-invoice-correction")]
    public async Task WithoutAnOriginalDocumentTheReasonIsStillRequired(string path)
    {
        // The contract before #67: the old dynamic path refuses a correction without a reason before Nexo sees it.
        var response = await _client.PostAsync(path, Json(
            "{\"correctionReason\":\"  \",\"items\":[{\"productSymbol\":\"SYM\",\"quantityCorrection\":-1}]}"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(SalesCorrectionWriter.ReasonRequired, body);
    }

    [Theory]
    [InlineData("/api/documents/sales-invoice-correction")]
    [InlineData("/api/documents/receipt-return")]
    public async Task APriceCorrectionIsRefusedNotIgnored(string path)
    {
        var response = await _client.PostAsync(path, Json(
            "{\"originalDocumentId\":1001,\"items\":[{\"originalPositionId\":7,\"quantityCorrection\":-1,\"priceNetCorrection\":5}]}"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("priceNetCorrection", body);
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static HttpClient CreateAuthenticatedClient(NexoApiFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NexoApiFactory.ApiKey);
        return client;
    }
}
