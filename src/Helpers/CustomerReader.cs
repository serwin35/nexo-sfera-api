using System.Runtime.CompilerServices;
using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed, read-only access to the contractor card (<c>Podmiot</c>) for the members the dynamic mapper read under names
/// that do not exist in SDK 61.1: <c>Symbol</c>, <c>NazwaPelna</c>, <c>REGON</c> (on <c>Firma</c>), contacts by
/// <c>Kontakt.Typ</c> (the kind is <c>Kontakt.Rodzaj</c>), <c>Rachunki[].NumerRachunku/NazwaBanku/Glowny</c>.
/// </summary>
/// <remarks>Must run on the SDK thread. Nothing here modifies SDK objects.</remarks>
public static class CustomerReader
{
    /// <summary>Largest page of <c>GET /api/customers?full=true</c> (each card costs several lazy loads on the SDK thread).</summary>
    public const int MaxFullPageSize = 200;

    private static readonly ConditionalWeakTable<Uchwyt, ContactKinds> ContactKindsCache = new();

    /// <summary>Ids of the default contact kinds (RodzajeKontaktu.DaneDomyslne) of the connected database.</summary>
    public sealed class ContactKinds
    {
        public int? EmailId { get; init; }
        public int? PhoneId { get; init; }
        public int? WebsiteId { get; init; }
    }

    public static ContactKinds ContactKindsOf(Uchwyt sfera)
    {
        return ContactKindsCache.GetValue(sfera, s =>
        {
            var defaults = SdkMember.Read(() => s.RodzajeKontaktu().DaneDomyslne, null);
            if (defaults == null) return new ContactKinds();

            return new ContactKinds
            {
                EmailId = SdkMember.Read<int?>(() => defaults.Email?.Id, null),
                PhoneId = SdkMember.Read<int?>(() => defaults.Telefon?.Id, null),
                WebsiteId = SdkMember.Read<int?>(() => defaults.StronaInternetowa?.Id, null),
            };
        });
    }

    /// <summary>
    /// The contractor list filters of <see cref="CustomerQueryRequest"/> as one SQL query (same filters as the legacy
    /// in-memory list; the search also matches the contractor symbol, which the legacy list read from a missing member).
    /// </summary>
    public static IQueryable<Podmiot> Query(Uchwyt sfera, CustomerQueryRequest query)
    {
        IQueryable<Podmiot> podmioty = sfera.Podmioty().Dane.Wszystkie();

        if (query.ActiveOnly) podmioty = podmioty.Where(p => p.Aktywny);
        if (query.ContractorsOnly) podmioty = podmioty.Where(p => p.Kontrahent);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            podmioty = podmioty.Where(p =>
                p.NazwaSkrocona.Contains(search) ||
                p.NIP.Contains(search) ||
                p.Sygnatura.PelnaSygnatura.Contains(search));
        }

        if (query.Type.HasValue)
        {
            // Podmiot.Typ holds TypObiektu: Firma = 2, Osoba = 1.
            short typ = query.Type.Value == CustomerType.Company ? (short)TypObiektu.Firma : (short)TypObiektu.Osoba;
            podmioty = podmioty.Where(p => p.Typ == typ);
        }

        if (query.ContractorType.HasValue)
        {
            var rodzaj = (byte)query.ContractorType.Value;
            podmioty = podmioty.Where(p => p.RodzajKontrahenta == rodzaj);
        }

        if (query.HasDocumentBlock.HasValue)
        {
            var block = query.HasDocumentBlock.Value;
            podmioty = podmioty.Where(p => p.BlokadaWystawianiaDokumentow == block);
        }

        if (query.IsEuTaxpayer.HasValue)
        {
            var eu = query.IsEuTaxpayer.Value;
            podmioty = podmioty.Where(p => p.PodatnikUE == eu);
        }

        return podmioty;
    }

    /// <summary>Overwrites the card fields the dynamic mapper cannot read and adds the delivery address and credit limits.</summary>
    public static void Enrich(CustomerDto dto, object entity, ContactKinds kinds)
    {
        if (entity is not Podmiot podmiot) return;

        var firma = SdkMember.Read(() => podmiot.Firma, null);
        var osoba = SdkMember.Read(() => podmiot.Osoba, null);

        // Podmiot.Typ is TypObiektu (Firma = 2, Osoba = 1); the dynamic mapper compared it with 0 and reported every
        // company as a person.
        dto.Type = SdkMember.Read(() => podmiot.Typ, (short)0) == (short)TypObiektu.Firma ? CustomerType.Company : CustomerType.Person;
        dto.Symbol = SdkMember.Text(() => podmiot.Sygnatura == null ? null : podmiot.Sygnatura.PelnaSygnatura) ?? string.Empty;
        dto.FullName = firma != null
            ? SdkMember.Text(() => firma.Nazwa)
            : osoba != null ? PersonName(osoba) : null;
        dto.REGON = firma == null ? null : SdkMember.Text(() => firma.REGON);
        dto.KRS = firma == null ? null : SdkMember.Text(() => firma.KRS);
        dto.FirstName = osoba == null ? null : SdkMember.Text(() => osoba.Imie);
        dto.LastName = osoba == null ? null : SdkMember.Text(() => osoba.Nazwisko);

        var contacts = SdkMember.Read(() => podmiot.Kontakty?.Where(k => k != null).ToList(), null) ?? new List<Kontakt>();
        dto.Email = Contact(contacts, kinds.EmailId);
        dto.Emails = Contacts(contacts, kinds.EmailId);
        dto.Phone = SdkMember.Text(() => podmiot.Telefon) ?? Contact(contacts, kinds.PhoneId);
        dto.EmailDomain = SdkMember.Text(() => podmiot.Domena);
        // Legacy value was Domena (the e-mail domain used to bind incoming mail); kept as the fallback.
        dto.Website = Contact(contacts, kinds.WebsiteId) ?? dto.EmailDomain;

        var account = SdkMember.Read(() => podmiot.RachunekPodstawowy, null)
                      ?? SdkMember.Read(() => podmiot.Rachunki?.Where(r => r != null && r.Aktywny).OrderBy(r => r.Id).FirstOrDefault(), null);
        dto.BankAccount = account == null ? null : SdkMember.Text(() => account.Numer);
        dto.BankName = account == null
            ? null
            : SdkMember.Text(() => account.PodmiotBankowy == null ? null : account.PodmiotBankowy.NazwaSkrocona);

        dto.CreditLimitCurrency = SdkMember.Text(() => podmiot.WalutaLimitow == null ? null : podmiot.WalutaLimitow.Symbol);
        dto.SalesCreditLimit = SdkMember.Read<decimal?>(() => podmiot.LimitKredytuNaSprzedazy == null ? null : podmiot.LimitKredytuNaSprzedazy.Wartosc, null);
        dto.DeliveryCreditLimit = SdkMember.Read<decimal?>(() => podmiot.LimitKredytuNaWydaniu == null ? null : podmiot.LimitKredytuNaWydaniu.Wartosc, null);
        dto.OrderCreditLimit = SdkMember.Read<decimal?>(() => podmiot.LimitKredytuNaZamowieniu == null ? null : podmiot.LimitKredytuNaZamowieniu.Wartosc, null);

        var mainAddress = SdkMember.Read(() => podmiot.AdresPodstawowy, null)
                          ?? SdkMember.Read(() => podmiot.Adresy?.Where(a => a != null).OrderBy(a => a.Id).FirstOrDefault(), null);
        dto.Address = MapAddress(mainAddress);
        dto.DeliveryAddress = MapAddress(SdkMember.Read(() => podmiot.DomyslnyAdresDostaw, null));
    }

    /// <summary>
    /// Address in the legacy shape: street/number/city/postcode from <c>Szczegoly</c>, or <c>Linia1</c>/<c>Linia2</c>
    /// as street/city when the details carry no street (kept as before so WolfFire parses addresses the same way).
    /// </summary>
    public static AddressDto? MapAddress(Adres? adres)
    {
        if (adres == null) return null;

        var szczegoly = SdkMember.Read(() => adres.Szczegoly, null);
        var panstwo = SdkMember.Read(() => adres.Panstwo, null);
        var country = panstwo == null ? null : SdkMember.Read(() => panstwo.Nazwa, null);
        var countryEuCode = panstwo == null ? null : SdkMember.Text(() => panstwo.KodPanstwaUE);

        AddressDto? dto = null;
        if (szczegoly != null)
        {
            dto = new AddressDto
            {
                Street = SdkMember.Read(() => szczegoly.Ulica, null),
                BuildingNumber = SdkMember.Read(() => szczegoly.NrDomu, null),
                ApartmentNumber = SdkMember.Read(() => szczegoly.NrLokalu, null),
                City = SdkMember.Read(() => szczegoly.Miejscowosc, null),
                PostalCode = SdkMember.Read(() => szczegoly.KodPocztowy, null),
                Country = country,
            };
        }

        if (dto == null || string.IsNullOrEmpty(dto.Street))
        {
            dto = new AddressDto
            {
                Street = SdkMember.Read(() => adres.Linia1, null),
                City = SdkMember.Read(() => adres.Linia2, null),
                Country = country,
            };
        }

        dto.CountryEuCode = countryEuCode;
        dto.GLN = SdkMember.Read(() => adres.GLN, null);
        return dto;
    }

    private static string? PersonName(Osoba osoba)
    {
        var name = string.Join(" ", new[] { SdkMember.Text(() => osoba.Imie), SdkMember.Text(() => osoba.Nazwisko) }
            .Where(part => part != null));
        return name.Length == 0 ? null : name;
    }

    private static IEnumerable<Kontakt> OfKind(List<Kontakt> contacts, int? kindId) =>
        kindId == null
            ? Enumerable.Empty<Kontakt>()
            : contacts
                .Where(k => SdkMember.Read<int?>(() => k.Rodzaj == null ? null : k.Rodzaj.Id, null) == kindId)
                .OrderByDescending(k => SdkMember.Read(() => k.Podstawowy, false))
                .ThenBy(k => SdkMember.Read(() => k.Id, 0));

    private static string? Contact(List<Kontakt> contacts, int? kindId) =>
        OfKind(contacts, kindId).Select(k => SdkMember.Text(() => k.Wartosc)).FirstOrDefault(v => v != null);

    private static List<string> Contacts(List<Kontakt> contacts, int? kindId) =>
        OfKind(contacts, kindId).Select(k => SdkMember.Text(() => k.Wartosc)).Where(v => v != null).Select(v => v!).Distinct().ToList();
}
