using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.EgzekutorMagazynowy;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Numeracja;
using Microsoft.AspNetCore.Mvc.Testing;
using NexoSferaApi.Helpers;
using Xunit;

namespace NexoSferaApi.Tests;

/// <summary>Stock on a day through the typed IMagazynier overloads.</summary>
public sealed class StockAtDateTests
{
    [Theory]
    [InlineData("2024-06-11", true)]
    [InlineData(" 2024-06-11 ", true)]
    [InlineData("2024-6-11", false)]
    [InlineData("11.06.2024", false)]
    [InlineData("2024-06-11T00:00:00", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ADayIsYearMonthDayOnly(string? text, bool valid)
    {
        Assert.Equal(valid, StockAtDate.TryParseDay(text, out var day));
        if (valid)
        {
            Assert.Equal(new DateTime(2024, 6, 11), day);
        }
    }

    [Fact]
    public void TheEndOfADayIsItsLastSecond()
    {
        Assert.Equal(new DateTime(2024, 6, 11, 23, 59, 59), StockAtDate.EndOfDay(new DateTime(2024, 6, 11, 8, 30, 0)));
    }

    [Fact]
    public void FreeAndAvailableCallTheAsortymentOverloadsWithTheDate()
    {
        var magazynier = DispatchProxy.Create<IMagazynier, MagazynierProxy>();
        var calls = ((MagazynierProxy)(object)magazynier).Calls;
        var day = new DateTime(2024, 6, 11);

        Assert.Equal(7m, StockAtDate.Free(magazynier, day, new Asortyment(), null));
        Assert.Equal(9m, StockAtDate.Available(magazynier, StockAtDate.EndOfDay(day), new Asortyment(), new Magazyn()));

        Assert.Equal(new[]
        {
            "IloscWolna(Nullable`1, Asortyment, Magazyn) 2024-06-11 00:00:00",
            "IloscDostepna(Nullable`1, Asortyment, Magazyn) 2024-06-11 23:59:59",
        }, calls);
    }

    public class MagazynierProxy : DispatchProxy
    {
        public List<string> Calls { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var parameters = string.Join(", ", targetMethod!.GetParameters().Select(p => p.ParameterType.Name));
            Calls.Add($"{targetMethod.Name}({parameters}) {(DateTime?)args![0]:yyyy-MM-dd HH:mm:ss}");
            return targetMethod.Name == "IloscWolna" ? 7m : 9m;
        }
    }
}

/// <summary>The next number of a type on a day: predicted, never reserved or saved.</summary>
public sealed class NextNumberPreviewTests
{
    [Theory]
    [InlineData("FS", "FS")]
    [InlineData(" kfs ", "KFS")]
    [InlineData("zw", "ZW")]
    [InlineData("PW", "PW")]
    public void KnownTypesAreTakenInUpperCase(string type, string expected)
    {
        Assert.Null(NextNumberPreview.CheckType(type, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("WZ")]
    [InlineData("ZPA")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherTypesAreRefusedByName(string? type)
    {
        Assert.Contains("type must be one of FS, PA, KFS, ZW, RW, PW", NextNumberPreview.CheckType(type, out _));
    }

    [Fact]
    public void ACorrectionsNumberIsSaidToBeAnApproximation()
    {
        Assert.True(NextNumberPreview.IsCorrection("KFS"));
        Assert.True(NextNumberPreview.IsCorrection("ZW"));
        Assert.False(NextNumberPreview.IsCorrection("FS"));
        Assert.Equal("przybliżenie: korekta bez dokumentu korygowanego, seria z daty i magazynu MG",
            NextNumberPreview.BasisFor("KFS", "MG"));
        Assert.StartsWith("przybliżenie:", NextNumberPreview.BasisFor("ZW", "MG"));
        Assert.Equal("nowy dokument z konfiguracji domyślnej", NextNumberPreview.BasisFor("FS", "MG"));
    }

    [Fact]
    public void ThePredictionComesFromTheNumberingSourceOnTheDatedEntity()
    {
        var (document, state) = Document(new PodgladNumeru("FS 2024/06/", "24", '\0', string.Empty, string.Empty));
        var magazyn = new Magazyn();

        var outcome = NextNumberPreview.Predict(document, "FS", new DateTime(2024, 6, 11), magazyn, "nowy dokument");

        Assert.Null(outcome.Error);
        Assert.Equal(("FS 2024/06/24", 24), (outcome.FullNumber, outcome.Sequence));
        Assert.Same(state.Data, state.PredictedFor);
        Assert.Equal(new DateTime(2024, 6, 11), state.Data.DataWydaniaWystawienia);
        Assert.Equal(new DateTime(2024, 6, 11), state.Data.DataWprowadzenia);
        Assert.Same(magazyn, state.Data.Magazyn);
    }

    [Fact]
    public void NothingIsReservedOrSaved()
    {
        var (document, state) = Document(new PodgladNumeru("FS 2024/06/", "24", '\0', string.Empty, string.Empty));

        NextNumberPreview.Predict(document, "FS", new DateTime(2024, 6, 11), null, "nowy dokument");

        var allowed = new[] { "get_Dane", "get_ZrodloNumerowRezerwujace", "PodajPrzewidywanyNumer" };
        Assert.Empty(state.Calls.Except(allowed));
    }

    [Fact]
    public void APredictionWithoutANumberOrOfAnotherPeriodIsAnError()
    {
        var (auto, _) = Document(new PodgladNumeru("FS 2024/06/", "Auto", '\0', string.Empty, string.Empty));
        var (july, _) = Document(new PodgladNumeru("FS 2024/07/", "1", '\0', string.Empty, string.Empty));

        Assert.Contains("predicted no number", NextNumberPreview.Predict(auto, "FS", new DateTime(2024, 6, 11), null, "b").Error);
        Assert.Contains("belongs to 2024-07", NextNumberPreview.Predict(july, "FS", new DateTime(2024, 6, 11), null, "b").Error);
    }

    [Fact]
    public void ACorrectionGetsTheWarehouseAskedForButNotTheEntryDate()
    {
        // No original is loaded, so the warehouse comes from the request; the typed correction path sets only the
        // issue date, and so does the preview.
        var (document, state) = Document(new PodgladNumeru("KFS 2024/02/", "2", '\0', string.Empty, string.Empty));
        var entry = state.Data.DataWprowadzenia;
        var magazyn = new Magazyn();

        var outcome = NextNumberPreview.Predict(document, "KFS", new DateTime(2024, 2, 29), magazyn, "przybliżenie");

        Assert.Null(outcome.Error);
        Assert.Equal(2, outcome.Sequence);
        Assert.Equal(new DateTime(2024, 2, 29), state.Data.DataWydaniaWystawienia);
        Assert.Equal(entry, state.Data.DataWprowadzenia);
        Assert.Same(magazyn, state.Data.Magazyn);
    }

    [Fact]
    public void TheSourceNeverReservesOrSaves()
    {
        // Read-only by construction: the preview code calls none of the writing members (comments aside).
        foreach (var file in new[] { "Helpers/NextNumberPreview.cs", "Controllers/DocumentNumbersController.cs" })
        {
            var code = string.Join("\n", File.ReadAllLines(Path.Combine(SourceRoot(), file))
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            foreach (var forbidden in new[] { "Zarezerwuj", "Zatwierdz", "Zapisz", "ZwolnijNumer", "SaveChanges",
                         "originalDocumentId", "DokumentySprzedazy().Dane" })
            {
                Assert.False(code.Contains(forbidden, StringComparison.Ordinal), $"{file} mentions {forbidden}");
            }

            // A correction is never built from a real document: only the creators without an argument.
            Assert.DoesNotMatch(@"Utworz(KorekteFakturySprzedazy|ZwrotDoParagonu)\s*\(\s*[^)\s]", code);
        }
    }

    private static (IDokumentSprzedazy Document, DocumentState State) Document(PodgladNumeru prediction)
    {
        var state = new DocumentState(prediction);
        var document = DispatchProxy.Create<IDokumentSprzedazy, DocumentProxy>();
        ((DocumentProxy)(object)document).State = state;
        return (document, state);
    }

    private static string SourceRoot()
    {
        var configured = Environment.GetEnvironmentVariable("NEXO_SOURCE_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "NexoSferaApi.csproj")))
            {
                return Path.Combine(current.FullName, "src");
            }
        }

        throw new DirectoryNotFoundException("Could not find the NexoSferaApi source directory.");
    }

    public sealed class DocumentState(PodgladNumeru prediction)
    {
        public PodgladNumeru Prediction { get; } = prediction;
        public DokumentDS Data { get; } = new();
        public List<string> Calls { get; } = new();
        public object? PredictedFor { get; set; }
    }

    public class DocumentProxy : DispatchProxy
    {
        internal DocumentState? State { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var state = State!;
            state.Calls.Add(targetMethod!.Name);
            return targetMethod.Name switch
            {
                "get_Dane" => state.Data,
                "get_ZrodloNumerowRezerwujace" => Source(state),
                _ => throw new NotSupportedException($"The preview must not call {targetMethod.Name}"),
            };
        }

        private static IZrodloNumerow Source(DocumentState state)
        {
            var source = DispatchProxy.Create<IZrodloNumerow, SourceProxy>();
            ((SourceProxy)(object)source).State = state;
            return source;
        }
    }

    public class SourceProxy : DispatchProxy
    {
        internal DocumentState? State { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var state = State!;
            state.Calls.Add(targetMethod!.Name);
            if (targetMethod.Name != "PodajPrzewidywanyNumer")
            {
                throw new NotSupportedException($"The preview must not call {targetMethod.Name}");
            }

            state.PredictedFor = args![0];
            return state.Prediction;
        }
    }
}

/// <summary>The read-only endpoints refuse bad input before the SDK is touched.</summary>
public sealed class StockAtDateAndNextNumberContractTests(NexoApiFactory factory) : IClassFixture<NexoApiFactory>
{
    private readonly HttpClient _client = CreateAuthenticatedClient(factory);

    [Theory]
    [InlineData("/api/inventory/stock-at-date?date=2024-06-11", "symbol is required")]
    [InlineData("/api/inventory/stock-at-date?symbol=SYM&date=2024-6-11", "date is required as yyyy-MM-dd")]
    [InlineData("/api/inventory/free-quantity?productId=1&date=11.06.2024", "date must be yyyy-MM-dd")]
    [InlineData("/api/documents/next-number?type=WZ&date=2024-06-11", "type must be one of")]
    [InlineData("/api/documents/next-number?type=KFS", "date is required as yyyy-MM-dd")]
    public async Task BadInputIsRefusedWithItsReason(string url, string reason)
    {
        var response = await _client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(reason, body);
    }

    private static HttpClient CreateAuthenticatedClient(NexoApiFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NexoApiFactory.ApiKey);
        return client;
    }
}
