using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NexoSferaApi.Filters;
using NexoSferaApi.Helpers;
using NexoSferaApi.Models.Dto;
using Xunit;

namespace NexoSferaApi.Tests;

/// <summary>
/// A form stated outright (<c>invoiceForm</c>) is applied exactly or refused, never ignored: a historical invoice
/// imported as "Papierowa" must not land in Subiekt's KSeF send queue.
/// </summary>
public sealed class InvoiceFormTests
{
    /// <summary>The SDK calls of <c>InvoiceForm.Apply</c>, as plain state.</summary>
    private sealed class FakeDocument
    {
        public bool CanTakeForm { get; init; } = true;
        public byte? Stored { get; set; } = 1; // KSEF: what a buyer's default may have set
        public byte? Keeps { get; init; } // when set, Nexo keeps this whatever is written
        public int Writes { get; private set; }

        public string? Apply(string requested) => InvoiceForm.Apply(requested, () => CanTakeForm, () => Stored, value =>
        {
            Writes++;
            Stored = Keeps ?? value;
        });
    }

    [Theory]
    [InlineData("Papierowa", (byte)0)]
    [InlineData("papierowa", (byte)0)]
    [InlineData(" KSEF ", (byte)1)]
    [InlineData("ksef", (byte)1)]
    public void AppliesTheRequestedForm(string requested, byte stored)
    {
        var document = new FakeDocument();

        Assert.Null(document.Apply(requested));
        Assert.Equal(stored, document.Stored);
    }

    [Theory]
    [InlineData("KSEFOffline")]
    [InlineData("KSEFAwaryjna")]
    [InlineData("paper")]
    [InlineData("")]
    [InlineData("0")]
    public void RefusesAFormItDoesNotApplyNamingTheField(string requested)
    {
        var document = new FakeDocument();

        var error = document.Apply(requested);

        Assert.NotNull(error);
        Assert.StartsWith("invoiceForm: ", error);
        Assert.Equal(0, document.Writes);
        Assert.Equal(error, InvoiceForm.CheckRequest(requested));
    }

    [Fact]
    public void AnAbsentFormIsNoRequest()
    {
        Assert.Null(InvoiceForm.CheckRequest(null));
    }

    [Fact]
    public void RefusesAnInvoiceThatCannotTakeAForm()
    {
        var document = new FakeDocument { CanTakeForm = false };

        Assert.Equal("invoiceForm: this invoice cannot take a form (OkreslaFormeFaktury is false)", document.Apply("Papierowa"));
        Assert.Equal(0, document.Writes);
        Assert.Equal((byte)1, document.Stored);
    }

    [Fact]
    public void RefusesWhenNexoKeepsAnotherForm()
    {
        var document = new FakeDocument { Keeps = 1 };

        Assert.Equal("invoiceForm: Nexo kept KSEF", document.Apply("Papierowa"));
    }

    [Fact]
    public void RefusesWhenNexoKeepsNoForm()
    {
        var document = new FakeDocument { Keeps = null, Stored = null };
        var cleared = new FakeDocument { Stored = 1 };

        Assert.Null(document.Apply("KSEF"));
        Assert.Equal("invoiceForm: Nexo kept no form",
            InvoiceForm.Apply("Papierowa", () => true, () => null, _ => cleared.Stored = null));
    }

    [Theory]
    [InlineData((byte)0, "Papierowa")]
    [InlineData((byte)1, "KSEF")]
    [InlineData((byte)2, "KSEFOffline")]
    [InlineData((byte)3, "KSEFAwaryjna")]
    [InlineData((byte)9, "9")]
    [InlineData(null, null)]
    public void NamesTheStoredForm(byte? stored, string? name)
    {
        Assert.Equal(name, InvoiceForm.Name(stored));
    }

    [Fact]
    public void ReadsNothingFromWhatIsNotADocument()
    {
        Assert.Equal((null, null), InvoiceForm.Read(null));
        Assert.Equal((null, null), InvoiceForm.Read("FS 2024/06/24"));

        var dto = new DocumentDto();
        InvoiceForm.Fill(dto, null);
        Assert.Null(dto.InvoiceForm);
        Assert.Null(dto.KsefStatus);
    }

    [Theory]
    [InlineData("{\"invoiceForm\":\"Papierowa\"}", true)]
    [InlineData("{\"type\":1,\"InvoiceForm\":null}", true)]
    [InlineData("{\"items\":[{\"invoiceForm\":\"KSEF\"}]}", false)]
    [InlineData("{\"defaultInvoiceForm\":\"KSEF\"}", false)]
    [InlineData("{\"type\":1}", false)]
    [InlineData(null, false)]
    public void FindsATopLevelInvoiceFormOnly(string? body, bool expected)
    {
        Assert.Equal(expected, DocumentNumberFieldFilter.HasTopLevelField(body, InvoiceForm.Field));
    }
}

/// <summary>HTTP contract of <c>invoiceForm</c>: advertised, refused where it is not applied. No request reaches the SDK.</summary>
public sealed class InvoiceFormContractTests(NexoApiFactory factory) : IClassFixture<NexoApiFactory>
{
    private const string ItemsJson = "\"items\":[{\"productSymbol\":\"TEST\",\"quantity\":1}]";

    private readonly HttpClient _client = CreateAuthenticatedClient(factory);

    [Fact]
    public async Task CapabilitiesListTheEndpointThatAppliesAForm()
    {
        var response = await _client.GetAsync("/api/documents/capabilities");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.GetProperty("data");
        Assert.Equal(new[] { "sales-invoice" }, data.GetProperty("invoiceForm").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(new[] { "sales-invoice", "receipt" },
            data.GetProperty("explicitDocumentNumber").EnumerateArray().Select(e => e.GetString()));
    }

    [Theory]
    [InlineData("/api/documents/receipt")]
    [InlineData("/api/documents/customer-order")]
    [InlineData("/api/documents/vat-margin-invoice")]
    public async Task EndpointsThatCannotApplyAFormRefuseIt(string path)
    {
        var response = await PostAsync(path, "{\"type\":0,\"invoiceForm\":\"Papierowa\",\"issueDate\":\"2024-06-11\"," + ItemsJson + "}");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invoiceForm is supported only by POST /api/documents/sales-invoice", json);
    }

    [Fact]
    public async Task AnUnknownFormIsRefusedBeforeTheSdk()
    {
        var response = await PostAsync("/api/documents/sales-invoice",
            "{\"type\":1,\"invoiceForm\":\"KSEFOffline\",\"issueDate\":\"2024-06-11\"," + ItemsJson + "}");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invoiceForm: \\u0022KSEFOffline\\u0022 is not a form this bridge applies", json);
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
