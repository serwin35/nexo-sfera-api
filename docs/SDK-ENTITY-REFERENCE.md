# Nexo SDK entity reference (verified against SDK 61.1.0.9431)

Property names below were extracted from the `.NET` metadata of `InsERT.Moria.ModelDanych.dll` /
`InsERT.Moria.API.dll` (SDK 61.1.0.9431) and the shipped CHM documentation - they are **not** guesses.
Use them when mapping `dynamic` SDK objects in controllers. Names that do **not** exist are listed
explicitly because the codebase used them for a long time (and silently returned `0`/`null`).

How to re-verify (macOS, no dotnet needed): `python3 -m venv .venv && .venv/bin/pip install dnfile`,
then dump `TypeDef -> Property` from the DLL (see `scripts/` history / session notes), or extract the
CHM with `7zz x InsERT.nexo.Sfera.chm` and grep the `<title>` index.

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

**Do not exist on Dokument:** `WartoscNetto`, `WartoscBrutto`, `WartoscVat`, `TerminPlatnosci`, `DataWystawienia`,
`OdroczonaPlatnoscDni`, `Potwierdzony`, `DataUtworzenia`, `Kurs`, `DataKursu`.

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
| Order state | `StanRealizacjiZamowienia.ProcentowyStanRealizacji`, `PozycjeRealizowane`, `PozycjeRealizujace` |
| Misc | `LP`, `Opis`, `Termin`, `Rezerwacja`, `Przyjecie`, `Wydanie`, `CenaRecznieEdytowana`, `RabatRecznieEdytowany` |

**Do not exist:** `Jednostka`, `JednostkaMiary`, `Asortyment`, `RabatProcent`, `RabatKwota`, `CenaNetto`, `CenaJednostkowa`, `Marza`.

## Asortyment (product) - units & kits
`JednostkiMiar[]` (JednostkaMiaryAsortymentu), `PodstawowaJednostkaMiaryAsortymentu`, `JednostkaSprzedazy`, `JednostkaZakupu`,
`JednostkaMagazynowa`, `JednostkaPorownawcza`, `SkladnikiKompletu[]` (SkladnikKompletu: `Skladnik`, `Ilosc`,
`JednostkaMiaryAsortymentu`, `Cena`, `Wartosc`, `LiczbaPorzadkowa`, `BlokujIlosc`), `SkladnikiWKompletach`, `Rodzaj`.

JednostkaMiaryAsortymentu: `JednostkaMiary` (`Symbol`, `Nazwa`, `Precyzja`, `Aliasy`, `WszystkieAliasy`), `Precyzja`, `Masa`,
`MasaNetto`, `Objetosc`, `KodKreskowyOpakowania`, `PodstawowyKodKreskowy`, `KodyKreskowe`, `KodPLU`,
`PrzelicznikJednostkiNadrzednej`, `PrzelicznikJednostkiPodrzednej` (PrzelicznikJednostekMiarAsortymentu:
`JednostkaNadrzedna`, `JednostkaPodrzedna`, `LiczbaJednostkiNadrzednej`, `LiczbaJednostkiPodrzednej`).

Business object `IAsortyment`: `JednostkiMiary` (IJednostkiMiarAsortymentu: `DodajJednostkeMiary(nowa, bazowa[, liczbaNowej, liczbaBazowej])`,
`UstawPodstawowaJednostkeMiary`, `UsunJednostkeMiary`, `ZnajdzJednostkeMiary`), `Skladniki` (ISkladnikiKompletu: `Dodaj(...)`, `Usun(...)`).

Adding a line in a chosen unit: `IPozycjeDokumentu.Dodaj(Asortyment, decimal ilosc, JednostkaMiaryAsortymentu)`;
changing later: `ZmienJednostkePozycji(PozycjaDokumentu, JednostkaMiaryAsortymentu, bool zaokraglijIlosc, OperacjaPrzeliczeniaCenyPoZmianieJednostki?)`.

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

## Other enums (numeric)
`MetodaGrupowaniaPozycji`: 1 BezKonsolidacji, 2 KonsolidacjaWJednostceMiary, 3 KonsolidacjaBezWzgleduNaJednostkeMiary, 4 KonsolidacjaWJednostceMiaryICenie.
`TypDokumentu` (flags): ZK 1, ZD 2, WZ 4, PZ 8, KPZ 16, KWZ 32, FS 64, PW 128, RW 256, KFS 512, FZ 1024, KFZ 2048, MMW 4096, MMP 8192, ZPM 16384, ZPR 32768.

## SDK 61.1.0.9431 changes vs 61.0.x (relevant)
- Model: `DokumentDane.WygenerowanePrzezAI` (attachments/binary documents), `PozycjaZamowieniaWysylkowego.KodTaryfyCelnej`.
- API: e-commerce extension DTOs (`PozycjaZamowieniaDTO.KodTaryfyCelnej/NumerySeryjne`, `OfertaWynikDTO.ZdjeciaOferty`,
  pagination `PaginacjaParametry/PaginacjaWynik<T>` replacing `PobranieListyZmianWOfertachWynik.Oferty/IdentyfikatorOstatniegoZdarzenia/...`),
  `IZdjecie.WygenerowanePrzezAI` + `IGaleriaZdjec.UstawWygenerowanePrzezAI`, `RodzajFakturyZaliczkowej` +3 values,
  `DokumentHandlowyExtensions.ZaliczkowyKoncowyLubKorekta`. No new assemblies; no removed types.
