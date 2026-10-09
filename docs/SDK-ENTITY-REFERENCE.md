# Nexo SDK entity reference (verified against SDK 61.1.0.9431)

Property names below were extracted from the `.NET` metadata of `InsERT.Moria.ModelDanych.dll` /
`InsERT.Moria.API.dll` (SDK 61.1.0.9431) and the shipped CHM documentation - they are **not** guesses.
Use them when mapping `dynamic` SDK objects in controllers. Names that do **not** exist are listed
explicitly because the codebase used them for a long time (and silently returned `0`/`null`).

How to re-verify (macOS, no dotnet needed): `python3 -m venv .venv && .venv/bin/pip install dnfile`,
then dump `TypeDef -> Property` from the DLL (see `scripts/` history / session notes), or extract the
CHM with `7zz x InsERT.nexo.Sfera.chm` and grep the `<title>` index. With the .NET SDK, a 30-line console app on
`System.Reflection.MetadataLoadContext` over `docs/nexoSDK_*/Bin` lists members with their types. Best of all, read new
fields through a typed helper (`Helpers/*Reader.cs`): a wrong member name is then a build error.

## Dokument (InsERT.Moria.ModelDanych.Dokument) - all commercial/warehouse documents

| Meaning | Property | Notes |
|---|---|---|
| Id / number | `Id`, `NumerWewnetrzny.PelnaSygnatura`, `NumerWewnetrzny.Numer`, `NumerZewnetrzny`, `NumerReferencyjny` | |
| Symbol / type | `Symbol`, `SymbolRzeczywisty`, `Konfiguracja` (`KonfiguracjaRzeczywista`) | |
| Issue date | `DataWydaniaWystawienia` | "Data wystawienia / wydania" |
| Entry date | `DataWprowadzenia` | |
| Sale date | `DataSprzedazy` | **only** on `DokumentHandlowy` (FS/FZ/PA...) ; `DataMagazynowa` also there |
| Totals | `Wartosc.NettoPoRabacie`, `Wartosc.BruttoPoRabacie`, `Wartosc.VatPoRabacie` | `Wartosc` is a sub-object |
| Goods / services | `WartoscTowarowNetto/Brutto`, `WartoscUslugNetto/Brutto` | |
| Amount due at issue | `KwotaDoZaplaty` (`PomniejszonaKwotaDoZaplaty` on DokumentHandlowy) | NOT the live paid state |
| Payments | `PlatnosciDokumentow[]` -> `PlatnoscDokumentu` | see below |
| Settlement | `Rozrachunek` -> `Rozrachunek` | **paid / unpaid source of truth** |
| Payment form | `FormaPlatnosci` (`.Nazwa`, `.Id`, `.TerminPlatnosci` days) | |
| Sums by form | `SumaPlatnosciGotowkowych`, `SumaPlatnosciKarta`, `SumaPlatnosciOdroczonych`, `SumaPlatnosciKredytowych`, `SumaZaplaconoPrzelewem`, `SumaSzybkichPlatnosci` | |
| Party | `Podmiot` (`.Id`, `.NazwaSkrocona`, `.NIP`), `PodmiotId`, `Platnik`, `Odbiorca`? (check per type) | |
| Warehouse | `Magazyn` (`.Id/.Symbol/.Nazwa`), `MagazynId` | |
| Status | `StatusDokumentu` (`.Nazwa`, `.Symbol`, `.Mnemonik`, `.Zaakceptowany`, `.Zamkniety`, `.Uniewazniony`), `StatusDokumentuId` | |
| Currency | `Waluta.Symbol`, `KursWalutyDokumentu` (`.Kurs`, `.DataKursu`) | |
| Lines | `Pozycje[]` -> `PozycjaDokumentu` | |
| Relations | `DokumentyRealizowane`, `DokumentyRealizujace`, `DokumentyPowiazane`, `DokumentPowiazany` | |
| Notes | `Uwagi`, `Tytul`, `Podtytul` | |
| Split payment | `WymagaPodzielonejPlatnosci`, `NettoPodlegajacePodzielonejPlatnosci` | |
| KSeF | `NumerKSeFDokumentu`, `DataWystawieniaNadanaPrzezKSEF`; DokumentHandlowy: `RodzajFakturyKsef`, `TerminPrzeslaniaDoKsef`, `AwariaKSeF` | |

| Header / audit | `Naglowek` (`NaglowekEncji`): `Utworzono`, `Zmieniono`, `Usunieto`, `Wydrukowano` (`DateTimeOffset`), `*OperatorNazwa` | the only creation / modification time; `Rozrachunek.Naglowek.Zmieniono` moves on payments |
| Cancelled | `StatusDokumentu.Uniewazniony` (bool) | also `Zamkniety`/`Zaakceptowany` (`bool?`), `Mnemonik`, `Nazwa` |
| Order realization (ZK) | `StanRealizacjiZamowienia` (header and line), `ZezwalajNaNiepelnaRealizacje`, `BlokujRealizacje` | see PozycjaDokumentu |

**Do not exist on Dokument:** `WartoscNetto`, `WartoscBrutto`, `WartoscVat`, `TerminPlatnosci`, `DataWystawienia`,
`OdroczonaPlatnoscDni`, `Potwierdzony`, `DataUtworzenia`, `DataModyfikacji`, `Anulowany`, `Zamkniety`, `Status`,
`Kurs`, `DataKursu`. Read in `Helpers/DocumentReader.cs` / `OrderReader.cs`.

### PlatnoscDokumentu (Dokument.PlatnosciDokumentow)
`Id`, `RodzajPlatnosci` (1 Przedplata, 2 Natychmiastowa, 3 Odroczona), `RodzajZaplaty` (0 Gotowka, 1 Przelew),
`FormaPlatnosci`, `KwotaDokumentu`, `KwotaPlatnosci`, `Procent`, `Termin` (date), `TerminDni`, `Data`, `Czas`,
`PozycjaHarmonogramuRozrachunku`, `Rozrachunek` (cesja), `NadplataDokumentu`.

### Rozrachunek (settlements; manager `Rozrachunki`)
`Id`, `Typ` (1 Naleznosc, 2 Zobowiazanie), `Podtyp.Nazwa`, `Kwota`, `KwotaPozostala` (remaining), `KwotaNierozliczona`,
`KwotaVAT`, `TerminPlatnosci`, `DataPowstania`, `DataDokumentuZrodlowego`, `DataOstatniegoRozliczenia`, `DokumentZrodlowy`
(string number), `Dokument` (entity), `Podmiot`, `Podmiot_Id`, `Waluta.Symbol`, `Tytul`, `Sciagalny`, `PodzielonaPlatnosc`,
`Pozycje[]` (raty -> `PozycjaHarmonogramuRozrachunku`: `Kwota`, `KwotaPozostala`, `TerminPlatnosci`, `Rozliczenia`).
`StatusRozrachunku` enum: 1 Rozliczony, 2 RozliczonyCzesciowo, 3 Nierozliczony, 4/5 wstępnie, 6 NiePodlegaRozliczeniu.
**Do not exist:** `KwotaDoRozliczenia`, `DataPlatnosci`, `DataWystawienia`, `NumerDokumentuZrodlowego`.

## PozycjaDokumentu (document line)

| Meaning | Property |
|---|---|
| Product | `AsortymentAktualny` (Asortyment: `Id/Symbol/Nazwa`) - **use this id**; `AsortymentWybrany` is the historical snapshot (different Id!) |
| Quantity | `Ilosc` (in the line unit), `IloscWJednostceBazowej` (stock unit), `IloscDoRealizacji` |
| Unit | `JednostkaMiaryAs` (JednostkaMiaryAsortymentu) -> `.JednostkaMiary.Symbol/.Nazwa`; `JednostkaMiaryAsId` |
| Price | `Cena.NettoPrzedRabatem/NettoPoRabacie/BruttoPrzedRabatem/BruttoPoRabacie`, `Cena.RabatProcent`, `Cena.RabatWartosc` |
| Value | `Wartosc.NettoPoRabacie/BruttoPoRabacie/VatPoRabacie` |
| VAT | `StawkaVat` (`.Symbol`, `.Wartosc`), `StawkaVatId` |
| Cost | `KosztMagazynowy`, `KosztEwidencyjny`, `JednostkowyKosztMagazynowy`, `KosztDlaMarzy` |
| Warehouse | `Magazyn`, `MagazynId` |
| Order state | `StanRealizacjiZamowienia` (ZK lines): `ZrealizowanaIlosc` (**base unit**; SDK sample: open = `IloscWJednostceBazowej - ZrealizowanaIlosc`), `ZrealizowanaIloscZeSkutkiem`, `ProcentowyStanRealizacji` (**fraction**, samples test `< 1m`), `DataOstatniejRealizacji`, `NumeryDokumentowRealizujacych` (text), `SkompletowanaIlosc`, `StanGotowosci`; `IloscDoRealizacji` (entity): `PozostalaIlosc` (left to realize), `BlokujRealizacje`, `TypDokumentuRealizowanego`; `PozycjeRealizowane`, `PozycjeRealizujace` |
| Reservation | `Rezerwacja` (entity): `Ilosc` and `IloscZrealizowana` (both in the **stock unit**; open = difference), `Ilosciowa` (true = stock, false = deliveries), `Termin` (expiry), `Asortyment`; `RezerwacjaIlosciowa` (bool), `RezerwacjaDopelniajaca` |
| Misc | `LP`, `Opis`, `Termin`, `Przyjecie`, `Wydanie`, `CenaRecznieEdytowana`, `RabatRecznieEdytowany`, `Dokument` (owner) |

**Do not exist:** `Jednostka`, `JednostkaMiary`, `Asortyment`, `RabatProcent`, `RabatKwota`, `CenaNetto`, `CenaJednostkowa`, `Marza`,
`Nazwa` (name = `AsortymentWybrany.Nazwa`), `IloscZrealizowana`, `Zarezerwowana`, `IloscZarezerwowana`.

## Asortyment (product) - units & kits
`JednostkiMiar[]` (JednostkaMiaryAsortymentu), `PodstawowaJednostkaMiaryAsortymentu`, `JednostkaSprzedazy`, `JednostkaZakupu`,
`JednostkaMagazynowa`, `JednostkaPorownawcza`, `SkladnikiKompletu[]` (SkladnikKompletu: `Skladnik`, `Ilosc`,
`JednostkaMiaryAsortymentu`, `Cena`, `Wartosc`, `LiczbaPorzadkowa`, `BlokujIlosc`), `SkladnikiWKompletach`, `Rodzaj`.
Other verified members: `Symbol`, `Nazwa`, `Opis`, `PKWiU`, `KodCN`, `CenaEwidencyjna`, `StawkaVatSprzedaz`/`StawkaVatKupno`
(`StawkaVat`: `Id` Guid, `Symbol`, `Stawka` 0-1), `PoziomCen`, `Grupa`, `IsInRecycleBin`, `Naglowek`, `StanyMagazynowe[]`
(`StanMagazynowy`: `Magazyn_Id`, `IloscDostepna`, `IloscZarezerwowanaIlosciowo`, `IloscZarezerwowanaDostawowo`,
`IloscZadysponowana`), `RezerwacjeIlosciowe[]`, `IlosciDoRealizacji[]`, `OkresObowiazywania` ("for future use").

**Do not exist on Asortyment:** `EAN`, `KodEan`, `Aktywny`, `CzyZablokowany`, `CenaNetto`, `CenaBrutto`, `Masa`, `Waga`,
`Objetosc` (both on the unit), `PKWIU` (it is `PKWiU`), `StawkaVatSprzedazy` (it is `StawkaVatSprzedaz`), `NazwaPelna`,
`JestHandlowy`, `JestMagazynowy`, `StanMinimalny`/`StanMaksymalny` (business object methods
`IAsortyment.StanMinimalny(Magazyn)` / `StanOptymalny(Magazyn)`). Read in `Helpers/ProductReader.cs`.

**Activity:** there is no active flag. `IDane.Nieaktywne()` returns recycled ("skasowane") objects, `Wszystkie()` never
does; a product is inactive only when `IsInRecycleBin`. Sales prices are not on the product: see price lists below.

JednostkaMiaryAsortymentu: `JednostkaMiary` (`Symbol`, `Nazwa`, `Precyzja`, `Aliasy`, `WszystkieAliasy`), `Precyzja`, `Masa`
(gross, in `JednostkaMiaryMasy`), `MasaNetto`, `Objetosc` (in `JednostkaMiaryObjetosci`), `KodKreskowyOpakowania`
(collective package code, **not** the EAN), `PodstawowyKodKreskowy`, `KodyKreskowe`, `KodPLU`,
`PrzelicznikJednostkiNadrzednej`, `PrzelicznikJednostkiPodrzednej` (PrzelicznikJednostekMiarAsortymentu:
`JednostkaNadrzedna`, `JednostkaPodrzedna`, `LiczbaJednostkiNadrzednej`, `LiczbaJednostkiPodrzednej`).
Mass/volume units: `sfera.JednostkiMiar().DaneDomyslne.Kilogram/Gram/Tona/Litr/MetrSzescieny`.

KodKreskowy: `Id`, `Kod` (max 128), `JednostkaMiaryAsortymentu` (owner, read only), `JednostkaMiaryAsortymentuZKodemPodstawowym`.
`KodyKreskowe` holds every code of the unit, the primary one included. There is no barcode business API; the SDK FAQ
("ustawić podstawowy kod kreskowy") adds one like this, which `ProductWriter` follows:

```csharp
var kod = new KodKreskowy();
towarBO.Dane.PodstawowaJednostkaMiaryAsortymentu.KodyKreskowe.Add(kod);
kod.Kod = "5901234123457";
towarBO.Dane.PodstawowaJednostkaMiaryAsortymentu.PodstawowyKodKreskowy = kod;
```

Duplicates are reported by Nexo as `KodKreskowyZduplikowanyBlad` or only `...Ostrzezenie`, so the bridge refuses a code
of another product itself.

Business object `IAsortyment`: `JednostkiMiary` (IJednostkiMiarAsortymentu: `DodajJednostkeMiary(nowa, bazowa[, liczbaNowej, liczbaBazowej])`,
`UstawPodstawowaJednostkeMiary`, `UsunJednostkeMiary`, `ZnajdzJednostkeMiary`), `Skladniki` (ISkladnikiKompletu: `Dodaj(...)`, `Usun(...)`).

Adding a line in a chosen unit: `IPozycjeDokumentu.Dodaj(Asortyment, decimal ilosc, JednostkaMiaryAsortymentu)`;
changing later: `ZmienJednostkePozycji(PozycjaDokumentu, JednostkaMiaryAsortymentu, bool zaokraglijIlosc, OperacjaPrzeliczeniaCenyPoZmianieJednostki?)`.

## Podmiot (contractor)
Read in `Helpers/CustomerReader.cs`.
- Identity: `Id`, `Sygnatura.PelnaSygnatura` (the symbol), `NazwaSkrocona`, `NIP`, `NIPSformatowany`, `NIPUE`, `SUN`,
  `Typ` (`TypObiektu`: **Firma = 2, Osoba = 1**), `Podtyp`, `Kontrahent`, `RodzajKontrahenta`, `Aktywny`, `Jednorazowy`,
  `StatusKlienta`, `Naglowek`.
- Company / person: `Firma` (`Nazwa` = full name, `REGON`, `KRS`, `BDO`, `EORI`), `Osoba` (`Imie`, `Nazwisko`, `PESEL`).
- Contacts: `Telefon` (copy of the primary phone), `Domena` (e-mail domain used to bind incoming mail, not a website),
  `Kontakty[]` (`Kontakt`: `Rodzaj` → `RodzajKontaktu.Id`, `Wartosc`, `Podstawowy`); kinds:
  `sfera.RodzajeKontaktu().DaneDomyslne.Email/Telefon/StronaInternetowa/Fax/EDoreczenia`.
- Addresses: `AdresPodstawowy`, `DomyslnyAdresDostaw`, `DomyslnyAdresKorespondencyjny`, `Adresy[]` (`AdresPodmiotu` :
  `Adres`: `Szczegoly` (`Ulica`, `NrDomu`, `NrLokalu`, `Miejscowosc`, `KodPocztowy`, `Poczta`), `Linia1..3`, `Panstwo`
  (`Nazwa`, `KodPanstwaUE` ISO 3166 for the EU), `GLN`).
- Money: `LimitKredytuKupieckiego` (`decimal?`), `LimitKredytuNaSprzedazy/NaWydaniu/NaZamowieniu` (`Wartosc`,
  `LimitPonizejWartosci`, `LimitPowyzejWartosci`) + `...Aktywny`, `WalutaLimitow`, `ZezwalajNaKredytKupiecki`,
  `MaksymalnyTerminPlatnosciKredytu`, `MaksymalnaLiczbaNiesplaconychDok`, `MaksymalnyLiczbaDniSpoznien`,
  `TerminPlatnosciSprzedaz/Zakup`, `DzienTerminuPlatnosciSprzedaz/Zakup`, `RachunekPodstawowy`/`Rachunki[]`
  (`RachunekBankowy`: `Numer`, `PodmiotBankowy` = the bank, `Aktywny`).
- Consents and blocks: `PrzetwarzanieDanychOsobowych`, `PrzetwarzanieWCelachMarketingowych`,
  `PrzetwarzanieDrogaElektroniczna`, `Zgody[]`, `BlokadaWystawianiaDokumentow`, `WyswietlajKomunikat`, `TekstKomunikatu`.

**Do not exist on Podmiot:** `Symbol`, `NazwaPelna`, `REGON` (on `Firma`), `AdresGlowny`; on `Kontakt`: `Typ`, `Glowny`;
on `RachunekBankowy`: `NumerRachunku`, `NazwaBanku`, `Glowny`.

## Finance documents (KP/KW/BP/BW) and settling
`PrzeplywFinansowy` (base of `OperacjaKasowa`/`OperacjaBankowa`): `Kwota`, `Wplyw` (direction), `Tytul`, `Data`.
`OperacjaKasowa`: `Stanowisko`, `Podmiot` (`PodmiotHistoria`; set with `IOperacjaKasowa.UstawPodmiot(Podmiot)`), `Opis`,
`Rodzaj`, `Osoba`. `OperacjaBankowa`: `Rachunek`, `Kontrahent`, `RodzajOperacji`, `DataEfektywna`.
Settling (SDK sample "Dodawanie dokumentów finansowych"): `bp.Rozrachunek.Rozlicz(naleznosc, kwota)` before `Zapisz()`;
`IOperacjaKasowa/IOperacjaBankowa.Rozrachunek` is `IRozrachunek : IRozliczenie` (`Rozlicz(Rozrachunek, decimal)`,
`Rozlicz(PozycjaHarmonogramuRozrachunku, decimal)`), `Rozrachunkowa` (whether the operation creates a settlement).
**Do not exist:** `OperacjaKasowa.DataUtworzenia`, `OperacjaBankowa.Podmiot`, `OperacjaBankowa.Opis`. The bridge's
`POST /api/payments/cash/*` never sets `Kwota`/`Wplyw` and `bank/*` sets the missing members above: these write paths
need a fix and a test on a database copy before use; `documentIdsToSettle` is rejected until then.

## Production orders (kompletacja)
Managers: `sfera.ZleceniaProdukcyjneMontowania()` (ZPM, `TypDokumentu.ZlecenieProdukcyjneMontowania = 16384`) and
`sfera.ZleceniaProdukcyjneRozkompletowania()` (ZPR, `32768`). Business object `IZlecenieProdukcyjneMontowania`:
`Montuj(Asortyment)`, `PodajMaksymalnaIloscKompletu()`, `PozycjeSkladniki` (IPozycjeSkladniki.Dodaj/ZmienJednostkePozycji),
`Braki`, `PrzegenerujAutomatycznePW()`, `NiePrzeliczajSkladnikowPoZmianieIlosciKompletu`, `WypelnijnaPodstawieZK(PozycjaDokumentu[, decimal?])`.
Entity `DokumentZPM : DokumentProdukcyjny : Dokument`: `PozycjaKomplet` (PozycjaKomplet : PozycjaDokumentu, with `PozycjeSkladnik[]`
-> PozycjaSkladnik: `IloscSumaryczna`, `WartoscSumaryczna`, `UdzialKosztu`), `MagazynSkladnikow`, `DokumentPrzychodujacyPW`,
`DokumentRozchodujacy`, `DataPrzychodu`, `DataRozchodu`.

## Internal warehouse documents
RW: `sfera.RozchodyWewnetrzne()` (`IRozchodWewnetrzny`, entity `DokumentRW`, `TypDokumentu.RozchodWewnetrzny = 256`,
`WypelnijNaPodstawieZK(IEnumerable<PozycjaDokumentu>, Dokument, ParametryGrupowaniaPodstawowe)`, `Braki`).
PW: `sfera.PrzychodyWewnetrzne()` (`IPrzychodWewnetrzny`, `DokumentPW`, `128`, `WypelnijNaPodstawieIW/WZ/PZ/MMP/ZD`).
Create with `mgr.Utworz(konfiguracja)` where `konfiguracja = Konfiguracje.Dane.WszystkieOTypieDokumentu(typ).First()` or `Konfiguracje.DaneDomyslne.RozchodWewnetrzny`.

## KSeF (e-invoices)
- Outbound: `FabrykaGeneratorowEFaktury`, `KoordynatorWysylaniaEFaktur` (`PrzekazDoWysylki`, `SprawdzStatus`, `PobierzUpo`).
- **Inbound:** `sfera.KoordynatorOdbioruEFaktur()` -> `Pobierz()` (incremental), `Pobierz(DateTime? od, DateTime? do)`,
  `Pobierz(string numerKsef[, RolaMojejFirmyDlaEFaktury])`, async variants; result `IWynikSynchronizacjiDokumentu`
  (`Sukces`, `NumerKSeF`, `NumerPelny`, `Bledy`, `DodatkowaInformacja`, `NieoczekiwanyProblem`).
- Import received e-invoice: `DokumentyZakupu().UtworzFaktureZakupu()` / `KorektyDokumentowZakupu().UtworzKorekteFakturyZakupu()`
  then `bo.ObslugaImportuEFaktur.WypelnijNaPodstawieDokumentuElektronicznego(DokumentElektroniczny)` and `Zapisz()`.
- `DokumentElektroniczny`: `Rodzaj` (0 Utworzony, 1 Importowany), `StatusPrzetworzenia` (1 DoPrzetworzeniaWKsiegowosci,
  2 DoPrzetworzeniaWSubiekcie, 3 Przetworzona, 4 PrzetworzonaRecznie, 5 Nieokreslony, 6 Odrzucona), `RodzajFaktury`
  (0 VAT, 1 KOR, 2 ZAL, 3 ROZ, 4 UPR, 5 KOR_ZAL, 6 KOR_ROZ), `RolaPodmiotu` (1 Sprzedawca, 2 Nabywca, 3 Inny, 4 Autoryzowany),
  `EStatus` (StatusKSeF: 0 DoWyslaniaNieWygenerowano, 1 DoWyslania, 2 WTrakcieWysylania, 3 Wyslano, 4 BladWysylki,
  5 PrzyjetoWKsef, 6 PobranoUPO, 7 NumerNadanyRecznie, 8 NiezgodneZeSchematem, 9 NieDotyczy, 10 NiePodlegaWysylce, 11 Nieokreslony),
  `NumerKSeF`, `NumerDokumentu`, `NIPSprzedawcy`, `IdentyfikatorPodatkowyKlienta`, `NazwaKlienta`, `PodmiotId`,
  `StatusDopasowaniaKlienta`, `MagazynId`, `Wartosc`, `Waluta`, `TerminPlatnosci`, `StanOplacenia`, `Kosztowa`,
  `Zsynchronizowany`, `DataWystawienia`, `DataUtworzenia`, `DataWysylki`, `DataDostarczeniaDoKsef`, `DataNadaniaKsefId`,
  `DokumentPowiazany`, `DokumentyPowiazaneRecznie`, `Xml`, `Hash`, `UPO`, `LinkDoUpo`.

## Price lists (Cennik, PoziomCen, ICennikPozycje)
Managers: `sfera.Cenniki()` (`ICenniki`: `Dane.Wszystkie()`, `Znajdz(Cennik)`, `Znajdz(string numer)`, `UtworzDodatkowy(Cennik glowny)`),
`sfera.PoziomyCen()` (`IPoziomyCen`). Read in `Helpers/PriceListReader.cs`.

- `Cennik`: `Id`, `Tytul`, `Podtytul`, `Opis`, `Status` (`StatusCennika`: 0 Definiowany, 1 Zatwierdzony, 2 Zamkniety; only
  approved lists price documents), `Bazowy` (one base list per level), `PoziomCen`, `Waluta` (`Symbol`, `PrecyzjaCeny`),
  `RodzajKalkulowanejCeny` (`RodzajCeny`: 0 Netto, 1 Brutto), `DynamicznaKalkulacjaCen`, `CenaZerowa` (`KontrolaCenyZerowej`:
  0 Ostrzegaj, 1 Zezwalaj), `CenaPonizejMarzy` (`KontrolaCenyPonizejMarzy`: 0 Ostrzegaj, 1 ZezwalajDlaCenyZRabatem, 2 ZezwalajZawsze),
  `DataUtworzenia/Zatwierdzenia/Zamkniecia/OstatniejAktualizacji/OstatniejEdycji`, `HarmonogramWaznosci` (`Harmonogram`:
  `RodzajHarmonogramu` 0 Codzienny / 1 Cotygodniowy / 2 Comiesięczny, `DataPoczatkowa`, `DataKoncowa`, `DniTygodnia`,
  `DniMiesiaca`, `Miesiace`), `ParametryPozycji[]` (`ParametrGrupyPozycjiCennika`, the one with `Domyslne = true` holds the
  defaults: `WyliczajPozycjeWedlug` (`MetodaWyliczaniaPozycjiCennika`: 0 WedlugMarzy, 1 WedlugNarzutu, 2 WedlugZysku),
  `Marza`, `Narzut`, `Zysk`, `FunkcjaWyliczaniaCenyBazowej`, `CennikCenyBazowej`, `FunkcjaWyrownywaniaCen`),
  `PodmiotyDlaKtorychDodatkowy`, `MiejscaSprzedazyDlaKtorychDodatkowy`, `TimeStamp`, `Sygnatura` ("for future use").
  **Do not exist:** `Symbol`, `Nazwa`, `DataOd`, `DataDo`, `Aktywny`, `Pozycje`. `PozycjeCennika` exists but is discouraged
  and lacks the VAT rate and supplier ids.
- Main vs additional: an additional list linked to a main one (`ICennik.UstawJakoDodatkowy(glowny)`) shares the level and has
  `Bazowy = false`; an unlinked additional list has no `PoziomCen`. There is no explicit "main price list" member.
- `PoziomCen`: `Id`, `Symbol`, `Nazwa`, `Opis`, `Waluta`, `CennikCenyBazowejId`/`CennikCenyBazowej`,
  `FunkcjaWyliczaniaCenyBazowej`, `FunkcjaWyboruCennika`, `MinimalnaMarza`, `Cenniki[]`. **Do not exist:** `Domyslny`,
  `Aktywny`, `Priorytet`.
- Positions: `ICennik.Pozycje` (`ICennikPozycje`: `Wszystkie`, `WszystkieAktywne`, `ZnajdzPozycjeCennika(Asortyment)`,
  `Edytuj`, `AktualizujPozycjeCennika`). Item `IUproszczonaPozycjaCennika`: `Id`, `IdAsortymentu`, `SymbolAsortymentu`,
  `NazwaAsortymentu`, `Glowna`, `IloscMinAsortymentu` (0 for the main position), `CenaNetto`, `CenaBrutto`, `SymbolWaluty`,
  `PrecyzjaWaluty`, `StawkaVATSprzedaz` (symbol), `StawkaVATSprzedaz_Stawka` (value; `StawkaVat.Stawka` is 0-1), `CenaBazowa`,
  `CenaKalkulacyjna`, `ParametrKalkulacyjny`, `DomyslneWyliczajPozycjeWedlug`, `FunkcjaWyliczaniaCenyBazowej`,
  `IdCennikaCenyBazowej`, `FunkcjaWyrownywaniaCeny`, `ZnakKorektyCeny`, `KorektaCeny`, `CenaPoWyliczeniu`,
  `CenaPoZaokragleniu`, `SzacowanyKoszt`, `MinimalnaMarza`, `Marza`, `Narzut`, `Zysk`, `DataAktualizacji`,
  `IdDostawcowAsortymentu`, `IdPodstawowegoDostawcyAsortymentu`, `IdProducentaAsortymentu`, `CenaSztywnaNaDokumencie`.
- Function ids: `FunkcjeWyliczaniaCenyBazowej.*_ID` and `FunkcjeWyrownywaniaCeny.*_Id` (`public static readonly Guid`).

## Other enums (numeric)
`MetodaGrupowaniaPozycji`: 1 BezKonsolidacji, 2 KonsolidacjaWJednostceMiary, 3 KonsolidacjaBezWzgleduNaJednostkeMiary, 4 KonsolidacjaWJednostceMiaryICenie.
`TypDokumentu` (flags): ZK 1, ZD 2, WZ 4, PZ 8, KPZ 16, KWZ 32, FS 64, PW 128, RW 256, KFS 512, FZ 1024, KFZ 2048, MMW 4096, MMP 8192, ZPM 16384, ZPR 32768.

## SDK 61.1.0.9431 changes vs 61.0.x (relevant)
- Model: `DokumentDane.WygenerowanePrzezAI` (attachments/binary documents), `PozycjaZamowieniaWysylkowego.KodTaryfyCelnej`.
- API: e-commerce extension DTOs (`PozycjaZamowieniaDTO.KodTaryfyCelnej/NumerySeryjne`, `OfertaWynikDTO.ZdjeciaOferty`,
  pagination `PaginacjaParametry/PaginacjaWynik<T>` replacing `PobranieListyZmianWOfertachWynik.Oferty/IdentyfikatorOstatniegoZdarzenia/...`),
  `IZdjecie.WygenerowanePrzezAI` + `IGaleriaZdjec.UstawWygenerowanePrzezAI`, `RodzajFakturyZaliczkowej` +3 values,
  `DokumentHandlowyExtensions.ZaliczkowyKoncowyLubKorekta`. No new assemblies; no removed types.
