using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed write path of <c>POST /api/customers</c>. Creation follows the SDK sample
/// <c>OperacjeSferyczne/ImportEksportPracownikow</c>: <c>IPodmioty.UtworzFirme()</c> / <c>UtworzOsobe()</c>, the card
/// fields, the primary address (<c>IPodmiot.DodajAdres(TypyAdresu.DaneDomyslne.Glowny)</c> → <c>Szczegoly</c>), contacts
/// (<c>Podmiot.Kontakty.Add</c> with the default contact kinds), groups (<c>Podmiot.Grupy</c>), one <c>Zapisz()</c>.
/// </summary>
/// <remarks>
/// <para>
/// All-or-nothing: every problem is collected before the save, and a business object that is not saved is disposed, which
/// discards it.
/// </para>
/// <para>
/// A symbol or NIP that another contractor already has is refused with that contractor's id
/// (<see cref="CreateOutcome.ExistingCustomerId"/>): a retried request never creates a second card. Must run on the SDK
/// thread.
/// </para>
/// </remarks>
public static class CustomerCreator
{
    public const int MaxShortNameLength = 128;
    public const int MaxCompanyNameLength = 512;
    public const int MaxFirstNameLength = 32;
    public const int MaxLastNameLength = 64;
    public const int MaxNotesLength = 256;
    public const int MaxContactLength = 256;
    public const int MaxGroups = 20;

    /// <summary>Result of <see cref="Create"/>.</summary>
    public sealed class CreateOutcome
    {
        /// <summary>Id of the saved contractor.</summary>
        public int? CustomerId { get; set; }

        /// <summary>Id of the contractor that already has the requested symbol or NIP (nothing was saved).</summary>
        public int? ExistingCustomerId { get; set; }

        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// Validation that needs no SDK access, one message per rejected field. Fields the endpoint does not write are refused
    /// when they carry a non-default value, instead of being dropped silently.
    /// </summary>
    public static List<string> Validate(CreateCustomerRequest r)
    {
        var errors = new List<string>();
        var isPerson = r.Type == CustomerType.Person;

        if (string.IsNullOrWhiteSpace(r.ShortName)) errors.Add("shortName: required.");
        else if (r.ShortName.Trim().Length > MaxShortNameLength) errors.Add($"shortName: at most {MaxShortNameLength} characters.");

        if (r.Symbol != null && string.IsNullOrWhiteSpace(r.Symbol)) errors.Add("symbol: must not be blank; omit it for an automatic symbol.");

        if (isPerson)
        {
            if (string.IsNullOrWhiteSpace(r.FirstName)) errors.Add("firstName: required for a person.");
            else if (r.FirstName.Trim().Length > MaxFirstNameLength) errors.Add($"firstName: at most {MaxFirstNameLength} characters.");
            if (string.IsNullOrWhiteSpace(r.LastName)) errors.Add("lastName: required for a person.");
            else if (r.LastName.Trim().Length > MaxLastNameLength) errors.Add($"lastName: at most {MaxLastNameLength} characters.");
            if (r.FullName != null) errors.Add("fullName: only companies have a full name in Nexo; send firstName and lastName.");
            if (r.REGON != null) errors.Add("regon: only companies have a REGON in Nexo.");
        }
        else
        {
            if (r.FirstName != null) errors.Add("firstName: only for type Person.");
            if (r.LastName != null) errors.Add("lastName: only for type Person.");
            if (r.FullName != null && r.FullName.Trim().Length > MaxCompanyNameLength)
                errors.Add($"fullName: at most {MaxCompanyNameLength} characters.");
        }

        var nip = NormalizeNip(r.NIP);
        if (r.NIP != null && nip == null) errors.Add("nip: must not be blank.");
        if (nip != null && nip.Length == 10 && nip.All(char.IsDigit) && !IsValidPolishNip(nip))
            errors.Add($"nip: {nip} is not a valid NIP (check digit).");
        if (r.EuTaxId != null && string.IsNullOrWhiteSpace(r.EuTaxId)) errors.Add("euTaxId: must not be blank.");

        CheckText(errors, "email", r.Email, MaxContactLength);
        CheckText(errors, "phone", r.Phone, MaxContactLength);
        CheckText(errors, "website", r.Website, MaxContactLength);
        CheckText(errors, "notes", r.Notes, MaxNotesLength);

        if (r.Address != null)
        {
            var a = r.Address;
            if (string.IsNullOrWhiteSpace(a.City)) errors.Add("address.city: required.");
            if (string.IsNullOrWhiteSpace(a.PostalCode)) errors.Add("address.postalCode: required.");
            CheckText(errors, "address.street", a.Street, 64);
            CheckText(errors, "address.buildingNumber", a.BuildingNumber, 10);
            CheckText(errors, "address.apartmentNumber", a.ApartmentNumber, 10);
            CheckText(errors, "address.city", a.City, 64);
            CheckText(errors, "address.postalCode", a.PostalCode, 10);
            if (string.IsNullOrWhiteSpace(a.Country)) errors.Add("address.country: must not be blank (default Polska).");
        }

        var groupIds = r.GroupIds ?? new List<int>();
        if (groupIds.Count > MaxGroups) errors.Add($"groupIds: at most {MaxGroups} groups.");
        if (groupIds.Any(id => id <= 0)) errors.Add("groupIds: every id must be a positive contractor group id.");
        if (groupIds.Distinct().Count() != groupIds.Count) errors.Add("groupIds: every group must be listed once.");

        foreach (var field in UnsupportedFields(r))
        {
            errors.Add($"{field}: not supported by POST /api/customers; set it in Subiekt after creating the contractor.");
        }

        return errors;
    }

    /// <summary>Creates and saves the contractor card. Call <see cref="Validate"/> first.</summary>
    public static CreateOutcome Create(Uchwyt sfera, CreateCustomerRequest request)
    {
        var outcome = new CreateOutcome();
        var podmioty = sfera.Podmioty();
        var symbol = string.IsNullOrWhiteSpace(request.Symbol) ? null : request.Symbol.Trim();
        var nip = NormalizeNip(request.NIP);

        if (symbol != null)
        {
            var owner = podmioty.Dane.Wszystkie()
                .Where(p => p.Sygnatura.PelnaSygnatura == symbol)
                .Select(p => (int?)p.Id)
                .FirstOrDefault();
            if (owner.HasValue)
            {
                outcome.ExistingCustomerId = owner;
                outcome.Errors.Add($"symbol: {symbol} already belongs to contractor {owner}.");
            }
        }

        if (nip != null)
        {
            var original = request.NIP!.Trim();
            var owner = podmioty.Dane.Wszystkie()
                .Where(p => p.NIP == nip || p.NIP == original)
                .Select(p => (int?)p.Id)
                .FirstOrDefault();
            if (owner.HasValue)
            {
                outcome.ExistingCustomerId ??= owner;
                outcome.Errors.Add($"nip: {nip} already belongs to contractor {owner}.");
            }
        }

        if (outcome.ExistingCustomerId.HasValue) return outcome;

        var groups = ResolveGroups(sfera, request.GroupIds, outcome.Errors);
        Panstwo? country = null;
        TypAdresu? mainAddressType = null;
        if (request.Address != null)
        {
            var countryName = request.Address.Country.Trim();
            country = sfera.Panstwa().Dane.Wszystkie().FirstOrDefault(c => c.Nazwa == countryName);
            if (country == null) outcome.Errors.Add($"address.country: country '{countryName}' not found in Nexo.");

            mainAddressType = SdkMember.Read(() => sfera.TypyAdresu().DaneDomyslne.Glowny, null);
            if (mainAddressType == null) outcome.Errors.Add("address: the default main address type is not available in Nexo.");
        }

        var contactKinds = SdkMember.Read(() => sfera.RodzajeKontaktu().DaneDomyslne, null);
        var emailKind = request.Email == null ? null : RequireKind(contactKinds?.Email, "email", outcome.Errors);
        var phoneKind = request.Phone == null ? null : RequireKind(contactKinds?.Telefon, "phone", outcome.Errors);
        var websiteKind = request.Website == null ? null : RequireKind(contactKinds?.StronaInternetowa, "website", outcome.Errors);

        if (outcome.Errors.Count > 0) return outcome;

        using IPodmiot podmiot = request.Type == CustomerType.Person ? podmioty.UtworzOsobe() : podmioty.UtworzFirme();
        var dane = podmiot.Dane;
        var errors = outcome.Errors;

        Set(errors, "shortName", () => dane.NazwaSkrocona = request.ShortName.Trim());
        if (request.Type == CustomerType.Person)
        {
            var osoba = dane.Osoba;
            if (osoba == null) errors.Add("type: Nexo created the person card without its person data.");
            else
            {
                Set(errors, "firstName", () => osoba.Imie = request.FirstName!.Trim());
                Set(errors, "lastName", () => osoba.Nazwisko = request.LastName!.Trim());
            }
        }
        else
        {
            var firma = dane.Firma;
            if (firma == null) errors.Add("type: Nexo created the company card without its company data.");
            else
            {
                Set(errors, "fullName", () => firma.Nazwa = (request.FullName ?? request.ShortName).Trim());
                if (request.REGON != null) Set(errors, "regon", () => firma.REGON = request.REGON.Trim());
            }
        }

        if (symbol != null)
        {
            Set(errors, "symbol", () =>
            {
                dane.Sygnatura ??= new Sygnatura();
                dane.Sygnatura.PelnaSygnatura = symbol;
            });
        }
        else
        {
            podmiot.AutoSymbol();
        }

        if (nip != null) Set(errors, "nip", () => dane.NIP = nip);
        if (request.EuTaxId != null)
        {
            // Nexo accepts a VATIN only on an EU taxpayer (Podmiot.NIPUE requires PodatnikUE).
            Set(errors, "euTaxId", () =>
            {
                dane.PodatnikUE = true;
                dane.NIPUE = request.EuTaxId.Trim();
            });
        }
        else if (request.IsEuTaxpayer)
        {
            Set(errors, "isEuTaxpayer", () => dane.PodatnikUE = true);
        }

        Set(errors, "isActive", () => dane.Aktywny = request.IsActive);
        Set(errors, "isOneTime", () => dane.Jednorazowy = request.IsOneTime);
        Set(errors, "isContractor", () => dane.Kontrahent = request.IsContractor);
        if (request.DocumentBlock) Set(errors, "documentBlock", () => dane.BlokadaWystawianiaDokumentow = true);
        if (request.Notes != null) Set(errors, "notes", () => dane.Uwagi = request.Notes.Trim());

        if (request.Address != null) ApplyAddress(podmiot, request.Address, mainAddressType!, country!, errors);

        if (emailKind != null) AddContact(errors, "email", dane, emailKind, request.Email!.Trim());
        if (phoneKind != null) AddContact(errors, "phone", dane, phoneKind, request.Phone!.Trim());
        if (websiteKind != null) AddContact(errors, "website", dane, websiteKind, request.Website!.Trim());

        foreach (var group in groups)
        {
            Set(errors, "groupIds", () => dane.Grupy.Add(group));
        }

        if (errors.Count > 0) return outcome;

        if (!podmiot.Zapisz())
        {
            errors.AddRange(SdkErrors.Of(podmiot));
            if (errors.Count == 0) errors.Add("Nexo refused to save the contractor.");
            return outcome;
        }

        outcome.CustomerId = dane.Id;
        return outcome;
    }

    /// <summary>NIP without spaces and dashes, or null when none was sent.</summary>
    public static string? NormalizeNip(string? nip)
    {
        if (nip == null) return null;
        var normalized = new string(nip.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
        return normalized.Length == 0 ? null : normalized;
    }

    /// <summary>Check digit of a 10-digit Polish NIP (weights 6,5,7,2,3,4,5,6,7, modulo 11).</summary>
    public static bool IsValidPolishNip(string nip)
    {
        int[] weights = { 6, 5, 7, 2, 3, 4, 5, 6, 7 };
        var sum = 0;
        for (var i = 0; i < weights.Length; i++) sum += (nip[i] - '0') * weights[i];
        var check = sum % 11;
        return check != 10 && check == nip[9] - '0';
    }

    /// <summary>JSON names of the request fields this endpoint does not write and that carry a non-default value.</summary>
    private static IEnumerable<string> UnsupportedFields(CreateCustomerRequest r)
    {
        if (r.SUN != null) yield return "sun";
        // Podmiot.RodzajKontrahenta is marked "pole nieużywane" in the SDK.
        if (r.ContractorType != ContractorType.Customer) yield return "contractorType";
        if (r.BankAccount != null) yield return "bankAccount";
        if (r.BankName != null) yield return "bankName";
        if (r.PaymentTermSales.HasValue) yield return "paymentTermSales";
        if (r.PaymentTermPurchase.HasValue) yield return "paymentTermPurchase";
        if (r.TradeCreditLimit.HasValue) yield return "tradeCreditLimit";
        if (r.AllowTradeCredit) yield return "allowTradeCredit";
        if (r.AlwaysUseEuVat) yield return "alwaysUseEuVat";
        if (!r.VatDeductible) yield return "vatDeductible";
        if (r.AgriculturalProducer) yield return "agriculturalProducer";
        if (r.DisplayMessage) yield return "displayMessage";
        if (r.MessageText != null) yield return "messageText";
        if (r.LoyaltyProgramParticipant) yield return "loyaltyProgramParticipant";
    }

    private static void CheckText(List<string> errors, string field, string? value, int maxLength)
    {
        if (value == null) return;
        var trimmed = value.Trim();
        if (trimmed.Length == 0) errors.Add($"{field}: must not be blank; omit it instead.");
        else if (trimmed.Length > maxLength) errors.Add($"{field}: at most {maxLength} characters.");
    }

    private static List<Grupa> ResolveGroups(Uchwyt sfera, List<int>? groupIds, List<string> errors)
    {
        if (groupIds == null || groupIds.Count == 0) return new List<Grupa>();

        var ids = groupIds.Distinct().ToList();
        var groups = sfera.Grupy().Dane.Wszystkie().Where(g => ids.Contains(g.Id)).ToList();
        foreach (var missing in ids.Where(id => groups.All(g => g.Id != id)))
        {
            errors.Add($"groupIds: contractor group {missing} not found.");
        }

        return groups;
    }

    private static RodzajKontaktu? RequireKind(RodzajKontaktu? kind, string field, List<string> errors)
    {
        if (kind == null) errors.Add($"{field}: the default contact kind is not available in Nexo.");
        return kind;
    }

    /// <summary>Main address: the card's primary address when Nexo created one, otherwise a new one of the main type.</summary>
    private static void ApplyAddress(IPodmiot podmiot, CreateAddressRequest address, TypAdresu mainType, Panstwo country, List<string> errors)
    {
        AdresPodmiotu? adres = null;
        Set(errors, "address", () => adres = podmiot.Dane.AdresPodstawowy ?? podmiot.DodajAdres(mainType));
        if (adres == null)
        {
            if (errors.Count == 0) errors.Add("address: Nexo did not add the main address.");
            return;
        }

        Set(errors, "address", () =>
        {
            adres.Szczegoly ??= new AdresSzczegoly();
            var szczegoly = adres.Szczegoly;
            if (!string.IsNullOrWhiteSpace(address.Street)) szczegoly.Ulica = address.Street.Trim();
            if (!string.IsNullOrWhiteSpace(address.BuildingNumber)) szczegoly.NrDomu = address.BuildingNumber.Trim();
            if (!string.IsNullOrWhiteSpace(address.ApartmentNumber)) szczegoly.NrLokalu = address.ApartmentNumber.Trim();
            szczegoly.Miejscowosc = address.City.Trim();
            szczegoly.KodPocztowy = address.PostalCode.Trim();
            adres.Panstwo = country;
        });
    }

    /// <summary>Adds a primary contact the way the SDK sample does: add the entity first, then set kind and value.</summary>
    private static void AddContact(List<string> errors, string field, Podmiot dane, RodzajKontaktu kind, string value)
    {
        Set(errors, field, () =>
        {
            var kontakt = new Kontakt();
            dane.Kontakty.Add(kontakt);
            kontakt.Rodzaj = kind;
            kontakt.Wartosc = value;
            kontakt.Podstawowy = true;
        });
    }

    private static void Set(List<string> errors, string field, Action set)
    {
        try
        {
            set();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            errors.Add($"{field}: Nexo rejected the value ({ex.Message}).");
        }
    }
}
