using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using InsERT.Moria.Numeracja;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using NexoSferaApi.Filters;
using NexoSferaApi.Helpers;
using Xunit;

namespace NexoSferaApi.Tests;

/// <summary>
/// A number stated outright (<c>documentNumber</c>) must be applied exactly or refused, never ignored or replaced by the
/// next free number: historical imports fill numbering gaps with the number the customer got.
/// </summary>
public sealed class ExplicitDocumentNumberTests
{
    [Fact]
    public void ReservesTheStatedNumberAndGivesItBackWhenTheDocumentIsNotSaved()
    {
        var numbering = new FakeNumbering { PreviewNumber = "24" };

        using (var reservation = new ExplicitDocumentNumber(NullLogger.Instance, "test"))
        {
            Assert.Null(reservation.Reserve(numbering.Document, 24));
            Assert.True(reservation.IsReserved);
            Assert.Equal(24, reservation.Number);
            Assert.Equal(new[] { 24 }, numbering.Reserved);
            Assert.Equal(0, numbering.Released);
        }

        Assert.Equal(1, numbering.Released);
    }

    [Fact]
    public void KeepsTheNumberOfASavedDocument()
    {
        var numbering = new FakeNumbering { PreviewNumber = "00024" };

        using (var reservation = new ExplicitDocumentNumber(NullLogger.Instance, "test"))
        {
            Assert.Null(reservation.Reserve(numbering.Document, 24));
            reservation.MarkSaved();
        }

        Assert.Equal(0, numbering.Released);
    }

    [Fact]
    public void RefusesASeriesWithoutManualNumbersBeforeReserving()
    {
        var numbering = new FakeNumbering { ManualAllowed = false };
        using var reservation = new ExplicitDocumentNumber(NullLogger.Instance, "test");

        var error = reservation.Reserve(numbering.Document, 24);

        Assert.NotNull(error);
        Assert.Contains("documentNumber 24", error);
        Assert.Contains("does not allow manual numbers", error);
        Assert.Empty(numbering.Reserved);
        Assert.False(reservation.IsReserved);
    }

    [Fact]
    public void RefusesADocumentWithoutANumberingSource()
    {
        var numbering = new FakeNumbering { HasSource = false };
        using var reservation = new ExplicitDocumentNumber(NullLogger.Instance, "test");

        var error = reservation.Reserve(numbering.Document, 24);

        Assert.NotNull(error);
        Assert.Contains("no numbering source", error);
        Assert.Empty(numbering.Reserved);
    }

    [Fact]
    public void RefusesADocumentThatCannotTakeANumber()
    {
        using var reservation = new ExplicitDocumentNumber(NullLogger.Instance, "test");

        var error = reservation.Reserve(new object(), 24);

        Assert.NotNull(error);
        Assert.Contains("cannot take a stated number", error);
        Assert.False(reservation.IsReserved);
    }

    [Fact]
    public void ReportsTheNumberingRefusalOfNexo()
    {
        var numbering = new FakeNumbering { ReserveFailure = new NumeracjaException("Numer 24 jest już zajęty.") };
        using var reservation = new ExplicitDocumentNumber(NullLogger.Instance, "test");

        var error = reservation.Reserve(numbering.Document, 24);

        Assert.NotNull(error);
        Assert.Contains("documentNumber 24", error);
        Assert.Contains("Numer 24 jest już zajęty.", error);
        Assert.False(reservation.IsReserved);
        Assert.Equal(1, numbering.Released);
    }

    [Fact]
    public void HidesTheDetailsOfAnUnexpectedFailure()
    {
        var numbering = new FakeNumbering { ReserveFailure = new InvalidOperationException("internal SDK detail") };
        using var reservation = new ExplicitDocumentNumber(NullLogger.Instance, "test");

        var error = reservation.Reserve(numbering.Document, 24);

        Assert.NotNull(error);
        Assert.Contains("could not be reserved", error);
        Assert.DoesNotContain("internal SDK detail", error);
        Assert.False(reservation.IsReserved);
        Assert.Equal(1, numbering.Released);
    }

    [Fact]
    public void KeepsANumberInThePeriodOfTheIssueDate()
    {
        var numbering = new FakeNumbering { PreviewNumber = "24", PreviewSuffix = "/06/2024" };
        using var reservation = new ExplicitDocumentNumber(NullLogger.Instance, "test");

        Assert.Null(reservation.Reserve(numbering.Document, 24, new DateTime(2024, 6, 11)));
        Assert.True(reservation.IsReserved);
    }

    [Fact]
    public void RefusesAndReleasesANumberOutsideThePeriodOfTheIssueDate()
    {
        var numbering = new FakeNumbering { PreviewNumber = "24", PreviewSuffix = "/06/2024" };
        using var reservation = new ExplicitDocumentNumber(NullLogger.Instance, "test");

        var error = reservation.Reserve(numbering.Document, 24, new DateTime(2024, 7, 1));

        Assert.NotNull(error);
        Assert.Contains("2024-06", error);
        Assert.Contains("2024-07", error);
        Assert.False(reservation.IsReserved);
        Assert.Equal(1, numbering.Released);
    }

    [Theory]
    [InlineData("FS 2024/06/24", null)]
    [InlineData("FS 24/06/2024", null)]
    [InlineData("FS 24/06/7", null)]
    [InlineData("FS 2024/07/24", "2024-07")]
    [InlineData("FS 2025/06/24", "2025-06")]
    [InlineData("PA 24/05/000031", "2024-05")]
    [InlineData("FS 24", null)]
    [InlineData("FS 1234/06/2024", null)]
    [InlineData("FS 1234/07/2024", "2024-07")]
    [InlineData("FS 2024/06/2024", null)]
    [InlineData("FS 24/2025", "2025")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ComparesThePeriodOfTheNumberWithTheIssueDate(string? preview, string? reportedPeriod)
    {
        var error = ExplicitDocumentNumber.PeriodMismatch(preview, new DateTime(2024, 6, 11));

        if (reportedPeriod == null)
        {
            Assert.Null(error);
        }
        else
        {
            Assert.NotNull(error);
            Assert.Contains($"belongs to {reportedPeriod}", error);
        }
    }

    [Theory]
    [InlineData("{\"documentNumber\":24}", true)]
    [InlineData("{\"type\":1,\"DocumentNumber\":24}", true)]
    [InlineData("{\"relatedDocumentNumber\":\"FZ 1/2024\"}", false)]
    [InlineData("{\"items\":[{\"documentNumber\":1}]}", false)]
    [InlineData("[{\"documentNumber\":1}]", false)]
    [InlineData("{\"documentNumber\": 24", false)]
    [InlineData("{\"type\":1}", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void FindsATopLevelDocumentNumberOnly(string? body, bool expected)
    {
        Assert.Equal(expected, DocumentNumberFieldFilter.HasDocumentNumberField(body));
    }

    [Fact]
    public void RefusesAndReleasesWhenNexoWarnsAboutTheReservation()
    {
        var numbering = new FakeNumbering { PreviewNumber = "24", Warnings = "Numer zaburza chronologię." };
        using var reservation = new ExplicitDocumentNumber(NullLogger.Instance, "test");

        var error = reservation.Reserve(numbering.Document, 24);

        Assert.NotNull(error);
        Assert.Contains("Numer zaburza chronologię.", error);
        Assert.False(reservation.IsReserved);
        Assert.Equal(1, numbering.Released);
    }

    [Fact]
    public void RefusesAndReleasesWhenNexoReservedAnotherNumber()
    {
        var numbering = new FakeNumbering { PreviewNumber = "29" };
        using var reservation = new ExplicitDocumentNumber(NullLogger.Instance, "test");

        var error = reservation.Reserve(numbering.Document, 24);

        Assert.NotNull(error);
        Assert.Contains("instead", error);
        Assert.Contains("29", error);
        Assert.False(reservation.IsReserved);
        Assert.Equal(1, numbering.Released);
    }

    [Theory]
    [InlineData("24", 24)]
    [InlineData("00024", 24)]
    [InlineData(" 7 ", 7)]
    [InlineData("24a", null)]
    [InlineData("-24", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ReadsTheDigitsOfThePreviewNumber(string? text, int? expected)
    {
        Assert.Equal(expected, ExplicitDocumentNumber.ParseNumber(text));
    }

    /// <summary>State behind fake <see cref="IObiektNumerowany"/> and <see cref="IZrodloNumerow"/> proxies.</summary>
    private sealed class FakeNumbering
    {
        public bool HasSource { get; init; } = true;
        public bool ManualAllowed { get; init; } = true;
        public Exception? ReserveFailure { get; init; }
        public string? Warnings { get; init; }
        public string PreviewNumber { get; init; } = "24";
        public string PreviewSuffix { get; init; } = "/06/2024";
        public List<int> Reserved { get; } = new();
        public int Released { get; set; }

        public IObiektNumerowany Document
        {
            get
            {
                var document = DispatchProxy.Create<IObiektNumerowany, NumberedDocumentProxy>();
                ((NumberedDocumentProxy)(object)document).State = this;
                return document;
            }
        }

        public IZrodloNumerow Source
        {
            get
            {
                var source = DispatchProxy.Create<IZrodloNumerow, NumberSourceProxy>();
                ((NumberSourceProxy)(object)source).State = this;
                return source;
            }
        }
    }

    public class NumberedDocumentProxy : DispatchProxy
    {
        internal object? State { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var state = (FakeNumbering)State!;
            switch (targetMethod?.Name)
            {
                case "get_ZrodloNumerowRezerwujace":
                    return state.HasSource ? state.Source : null;
                case "ZarezerwujNumer" when args is { Length: 1 }:
                    if (state.ReserveFailure != null) throw state.ReserveFailure;
                    state.Reserved.Add((int)args[0]!);
                    return null;
                case "SprawdzOstrzezeniaRezerwacji":
                    return state.Warnings;
                case "PodajPodgladNumeru":
                    return new PodgladNumeru("FS ", state.PreviewNumber, '\0', string.Empty, state.PreviewSuffix);
                case "ZwolnijNumer":
                    state.Released++;
                    return null;
                default:
                    throw new NotSupportedException($"Unexpected call {targetMethod?.Name}");
            }
        }
    }

    public class NumberSourceProxy : DispatchProxy
    {
        internal object? State { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var state = (FakeNumbering)State!;
            return targetMethod?.Name switch
            {
                "get_MoznaNadacNumerRecznie" => state.ManualAllowed,
                _ => throw new NotSupportedException($"Unexpected call {targetMethod?.Name}")
            };
        }
    }
}

/// <summary>
/// HTTP contract of <c>documentNumber</c>: endpoints that cannot apply it refuse it, and clients can ask which apply it.
/// None of these requests reaches the SDK.
/// </summary>
public sealed class ExplicitDocumentNumberContractTests(NexoApiFactory factory) : IClassFixture<NexoApiFactory>
{
    private const string ItemsJson = "\"items\":[{\"productSymbol\":\"TEST\",\"quantity\":1}]";

    private readonly HttpClient _client = CreateAuthenticatedClient(factory);

    [Fact]
    public async Task CapabilitiesListTheEndpointsThatApplyAStatedNumber()
    {
        var response = await _client.GetAsync("/api/documents/capabilities");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(json);
        var endpoints = document.RootElement.GetProperty("data").GetProperty("explicitDocumentNumber")
            .EnumerateArray().Select(element => element.GetString()).ToArray();
        Assert.Equal(new[] { "sales-invoice", "receipt" }, endpoints);
    }

    [Theory]
    [InlineData("/api/documents/customer-order")]
    [InlineData("/api/documents/purchase-invoice")]
    [InlineData("/api/documents/vat-margin-invoice")]
    [InlineData("/api/documents/sales-invoice-correction")]
    [InlineData("/api/warehouse-documents/pz")]
    public async Task EndpointsThatCannotApplyAStatedNumberRefuseIt(string path)
    {
        var response = await PostAsync(path, "{\"type\":1,\"documentNumber\":24,\"issueDate\":\"2024-06-11\"," + ItemsJson + "}");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("documentNumber is supported only by", json);
    }

    [Theory]
    [InlineData("/api/documents/sales-invoice", "{\"type\":1,\"documentNumber\":24," + ItemsJson + "}")]
    [InlineData("/api/documents/receipt", "{\"type\":0,\"documentNumber\":24," + ItemsJson + "}")]
    public async Task AStatedNumberNeedsTheIssueDate(string path, string body)
    {
        var response = await PostAsync(path, body);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("documentNumber needs issueDate", json);
    }

    [Theory]
    [InlineData("/api/documents/sales-invoice", "{\"type\":1,\"documentNumber\":0,\"issueDate\":\"2024-06-11\"," + ItemsJson + "}")]
    [InlineData("/api/documents/receipt", "{\"type\":0,\"documentNumber\":-3,\"issueDate\":\"2024-06-11\"," + ItemsJson + "}")]
    public async Task AStatedNumberMustBePositive(string path, string body)
    {
        var response = await PostAsync(path, body);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("DocumentNumber", json, StringComparison.OrdinalIgnoreCase);
    }

    private Task<HttpResponseMessage> PostAsync(string path, string json)
    {
        return _client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    private static HttpClient CreateAuthenticatedClient(NexoApiFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NexoApiFactory.ApiKey);
        return client;
    }
}
