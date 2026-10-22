# SDK coverage and roadmap (nexo SDK 62.0.0.9537, 2026-10-22)

What the InsERT nexo Sfera SDK offers per area, what the bridge exposes today, whether it works, and what is worth
building next for WolfFire. Written against `origin/main` at `c8ef41e` (after PR #31). PRs #32 and #33 were open and are
noted where they change the picture.

**How it was checked.** Every SDK member named here was looked up in `docs/nexoSDK_62.0.0.9537/Bin/InsERT.Moria.API.XML`,
`InsERT.Moria.ModelDanych.XML`, or decoded from the DLL metadata (type, interface list, method signatures, explicit
interface implementations). Call sequences come from the SDK samples in `Przyklady/`. Bridge status was read from the
source and cross-checked with the deployed swagger (349 paths, 2026-10-22). Nothing was run against a Nexo database, so
"broken" means "cannot work as written". Each such claim points to a member that does not exist or cannot bind. The
WolfFire column comes from a read-only grep of `WolfFireApp-Chart` (`origin/develop`, `packages/InsertNexo`, 27 bridge
paths).

**Status:** `read` / `write` work as documented. `partial`: some fields or filters are wrong. `broken`: fails (500), or
reports success without doing the work. `dev`: `[DevelopmentOnly]`. `-`: not exposed.
**WolfFire value:** H / M / L, with the reason.

## 1. Cross-cutting findings

These explain most of the `broken` entries below.

1. **Explicit interface implementations are invisible to `dynamic`.** Examples:
   - `AsortymentKartotekowyBO`: `ISkladnikiKompletu`, `IDostawcy`, `IOdbiorcy`, `IProducent`, `IMaterialyUslugi`.
   - `CennikBO`: `ICennikPozycje`.
   - `PodmiotBO`: `IPrzedstawicieleFirmy`.
   - `DokumentSprzedazyBO`: `IPlatnosciNaDokumencie.DodajPlatnosc*`.
   - `SpisInwentaryzacyjnyBO`: `IPozycjeSpisuInwentaryzacyjnego`.

   Several getters return the business object itself (`ZK.Pozycje`, `FS.Platnosci`, `ZPM.Braki`, `ZPM.PozycjeSkladniki`,
   `SpisInwentaryzacyjny.Pozycje`). A `dynamic` call through them sees only the public members. New code goes through
   the typed interface (`IAsortyment`, `ICennik`, `IPodmiot`, ...).
2. **Save errors were never returned.** Every `GetBusinessObjectErrors` copy reads `InvalidData[].Errors/MemberErrors`
   by reflection. Entities implement `ITypedDataErrorInfo` explicitly, so the lists are always empty. `PodajBledy` is an
   extension method, which `dynamic` never binds. A refused save comes back as "Failed to ..." with no reason.
   - Fix: `Helpers/SdkErrors.cs` reads the typed `IObiektBiznesowy.Bledy`.
   - Used by #36 and #38. Other controllers can switch one by one.
3. **Member names that do not exist**, assigned or read on `dynamic`. Writes throw (500); reads swallow the error (`0`,
   `null`, empty filter). Most frequent:
   - `Dokument.DataWystawienia`: does not exist. The entity has `DataWprowadzenia` and `DataWydaniaWystawienia`.
   - `PozycjaDokumentu.Dane`, `PozycjaDokumentu.CenaNetto`: the price is `Cena.NettoPrzedRabatem`.
   - `Asortyment.EAN`, `CenaNetto`, `Masa`, `Objetosc`, `Rodzaj_Id`, `Model_Id`.
   - `Podmiot.Symbol`, `Podmiot.Grupa`. Groups are `Podmiot.Grupy`, an `ICollection<Grupa>`.
   - `Grupa*.Symbol`, `Rodzic`. Contractor groups use `KategoriaNadrzedna`; product groups are flat.
   - `DaneDomyslne.PrzesuniecieMiedzymagazynowe`: it is `WydanieMiedzymagazynowe`.
   - `DaneDomyslne.OfertaDlaKlienta`: it is `Oferta`.
   - Manager `Edytuj`: no manager has it. Reopen with `Znajdz(encja)` and `Zapisz()`.
4. **False success.** A `try { ... } catch { }` around a failing member, followed by `Zapisz()` of an unchanged object,
   returns 200 for work that was never done. Affected:
   - ZK `close`;
   - document `associate`;
   - contractor group bulk add;
   - product feature assignment.
5. **Full-table scans under the single SDK lock.** `DynamicPropertyHelper.SafeGetAll` / `FindById` materialise a whole
   table to find one id: products, contractors, documents, ZK, ZD, settlements. The typed alternatives are
   `manager.Znajdz(e => e.Id == id)` (verified overload `Znajdz(Expression<Func<T,bool>>)`) or
   `Dane.Wszystkie().FirstOrDefault(e => e.Id == id)` (SQL side). This is the likely cause of the queueing after timeouts
   that WolfFire works around (`NexoPriceListReader`).

## 2. Coverage matrix

### 2.1 Products (`Asortyment`, `IAsortymenty` / `IAsortyment`)

| Capability | SDK (verified) | Bridge | Status | WolfFire |
|---|---|---|---|---|
| Read card | `Dane.Wszystkie()`, `Nieaktywne()`, units, barcodes, `StawkaVatSprzedaz` | `GET /api/products`, `/{id}`, `/by-symbol`, `/by-ean` | partial: `type` always Goods (`Rodzaj_Id`), `isVariant` always false, list `type` filter ignored, no `modifiedSince` | H: catalogue sync, details refreshed in a 7-day window because there is no delta |
| Create | `Utworz()`, `WypelnijNaPodstawieSzablonu(ISzablonyAsortymentu.DaneDomyslne.Towar/Usluga/Komplet/Opakowanie/Oplata or SzablonAsortymentu)`, `AutoSymbol()` | `POST /api/products` | broken: `ean`, `pkWiU`, `weight`, `volume`, `priceNet` throw (500); type, units, VAT ignored. **Fixed in #38** | M: local variants and imported products cannot be sold until they exist in Nexo |
| Delete / recycle bin | `IObiektBiznesowy.Usun()`, `Deaktywuj()`, `Aktywuj()`, `Dane.Nieaktywne()`, `Asortyment.IsInRecycleBin` | `DELETE /api/products/{id}` | write (dynamic, full scan, 404 for products already in the bin). **Typed in #38**. No restore | L |
| Update card | entity setters, `JednostkiMiary`, `KodyKreskowe` | `PUT /api/products/{id}` (typed `ProductWriter`) | write: symbol, name, description, ean, pkWiU, weight, volume, groupId. Swagger still advertises `priceNet` / `vatRate` / `isActive`, which return 400 | H: `PushProductToNexoJob` |
| Kind / kit | `Rodzaj` (`RodzajeAsortymentowExtensions.CzyTowar/CzyUsluga/CzyKomplet`), `ISkladnikiKompletu.Dodaj/Usun` (explicit) | `GET/POST/DELETE /{id}/components` | read OK; add and remove broken on `main` (dynamic on explicit). **Fixed by PR #33** | M: BOM for production (WolfFire still asks for `/komplet`, which exists as `/components`) |
| Units | `IJednostkiMiarAsortymentu.DodajJednostkeMiary`, `UsunJednostkeMiary`, `UstawPodstawowaJednostkeMiary` | `GET/POST/DELETE /{id}/units`, `PUT units/base`, `PUT units/defaults` | write. Changing the factor of an existing unit is not exposed | M: thread spool lengths |
| Barcodes | `JednostkaMiaryAsortymentu.KodyKreskowe`, `PodstawowyKodKreskowy` | primary barcode via `PUT /{id}` `ean` | partial: no add or remove of secondary codes | L |
| Group | `Asortyment.Grupa` (`GrupaAsortymentu`) | `PUT /{id}` `groupId` | write | M |
| Suppliers / manufacturer / receivers | `IAsortyment.Dostawcy.Dodaj/Usun/Podstawowy`, `Producent.Ustaw/Usun`, `Odbiorcy` (all explicit) | read only (price list positions carry supplier ids) | - | M: supplier-based symbols and replenishment |
| Features | `Asortyment.Cechy` (`ICollection<CechaAsortymentu>`); `CechaAsortymentu` has only `Nazwa` | `/api/product-attributes` | broken: create throws (`Symbol`), assign is a silent no-op (`DodajCeche`), `groups` returns contractor feature groups | L |
| Templates | `SzablonAsortymentu` (`Id`, `Symbol` = default product symbol, `Nazwa`, `RodzajAsortymentu`, `JednostkaMiary`, `StawkaVat`) | `/api/product-templates` | partial / broken: wrong member names, `create-product` never applies the template | L |
| Photos | `IAsortyment.PobierzGalerieZdjec()` | `/api/photo-gallery` | broken: the manager lookup is always null (500) | L |
| Stock min/max | `IAsortyment.StanMinimalny/UstawStanMinimalny(magazyn)` | - | `minStockLevel` / `maxStockLevel` always null | L |

### 2.2 Product groups (`GrupaAsortymentu`, `IGrupyAsortymentu`)

The SDK group is flat: `Nazwa` (1-50 characters) only, with no symbol, parent or description.

| Capability | Bridge | Status | WolfFire |
|---|---|---|---|
| Read | `GET /api/product-groups`, `/tree`, `/{id}`, `/children` | partial: empty symbol, flat tree | H: `SyncProductGroupsFromNexoJob` |
| Create / move | `POST`, `PUT /{id}/move` | broken (`Symbol`, `Rodzic`) | L |
| Rename / delete / assign | `PUT`, `DELETE`, `assign-products` | write: only the name is applied; assign scans all products once per id | L |

### 2.3 Contractors (`Podmiot`, `IPodmioty` / `IPodmiot`)

| Capability | SDK (verified) | Bridge | Status | WolfFire |
|---|---|---|---|---|
| Read card | addresses, delivery address (`DomyslnyAdresDostaw`), contacts, bank account, limits | `GET /api/customers`, `/{id}`, `/by-nip` (typed `CustomerReader`) | read. Not exposed: groups, price level, discount, representatives | H: contractor and address sync |
| Create | `UtworzFirme()` / `UtworzOsobe()`, `AutoSymbol()`, `Firma.Nazwa`, `DodajAdres(ITypyAdresu.DaneDomyslne.*)`, `Kontakty.Add` | `POST /api/customers` | **broken: always 500** (`Symbol`); generic `Utworz()`; wrong `Typ` values; address, e-mail, phone and bank account silently dropped | **H: `EnsureNexoCustomer` calls it for every shop order** (behind the write flag, which is off today) |
| Update | entity setters, `Firma.*` | `PUT /api/customers/{id}` | write: shortName, fullName, nip, regon. No KRS, GLN, terms, price level | M: `PushContractorToNexoJob` keeps KRS/GLN as local overrides |
| Addresses / contacts | `DodajAdres`, `UsunAdres`, `Kontakty.Add`, `UsunKontakt` | - | - | M-H: delivery addresses only local, ZK `deliveryAddress` always null |
| Bank accounts | `Rachunki` (`ICollection<RachunekBankowy>`) | read | - | L |
| Representatives | `IPodmiot.Przedstawiciele` (explicit `IPrzedstawicieleFirmy`) | - | - | L |
| Terms | `TerminPlatnosciSprzedaz/Zakup`, `PoziomCen`, `Rabat`, `CennikDodatkowy` | read partly (dynamic) | partial | M: payment deadlines for dunning |
| Delete | `Usun()` | `DELETE /api/customers/{id}` | write (hard delete attempt) | L |

### 2.4 Contractor groups (`Grupa`, `IGrupy`)

Membership is `Podmiot.Grupy`, a many-to-many `ICollection<Grupa>`. `Podmiot.Grupa` does not exist.

| Capability | Bridge | Status | WolfFire |
|---|---|---|---|
| Group tree | `GET /api/contractor-groups`, `/tree`, `/{id}`, `/children` | partial: flat (reads `Rodzic`, not `KategoriaNadrzedna`), empty symbol | H: `SyncContractorGroupsFromNexoJob` |
| Members | `GET /{id}/contractors` | **broken: always empty** (reads `Podmiot.Grupa`) | **H: WolfFire's nightly membership sync never assigns a group** |
| Add / remove member | `POST /{id}/contractors`, `/bulk`, `DELETE /{id}/contractors/{cid}` | broken: 500 / 200 with `added: 0` / always "not in group" | M |
| Create / rename / delete group | `POST`, `PUT`, `DELETE` | create broken (`Symbol`, `Rodzic`); rename name-only; delete via `Usun()` instead of `IGrupy.UsunGrupe(grupa, AkcjaZPoddrzewem)` | L |

### 2.5 Price lists and price levels (`Cennik`, `ICenniki` / `ICennik`, `PoziomCen`)

| Capability | SDK (verified) | Bridge | Status | WolfFire |
|---|---|---|---|---|
| Read headers and levels | `ICenniki.Dane`, `PoziomCen` | `GET /api/dictionary/price-levels`, `/price-lists`, `/price-lists/by-id/{id}` | read (legacy `isActive` is always false) | H: `SyncPriceLevelsFromNexoJob`, `SyncPriceListsFromNexoJob` |
| Read positions | `ICennik.Pozycje` (`ICennikPozycje`, explicit on `CennikBO`), `IUproszczonaPozycjaCennika` | `GET .../by-id/{id}/items` (typed `PriceListReader`) | read | H: price sync, live read for price proposals |
| Set price | `RozpocznijEdycje`, `CenaBrutto/CenaNetto`, `ZakonczEdycje` (returns `bool`), `Zapisz` | `PUT .../by-id/{id}/items` | write. It ignored a rejected `ZakonczEdycje()`; **fixed in #36** | H |
| Conditional set price | (bridge-side precondition) | `expectedPriceGross/Net` on `PUT .../items`, 409 on mismatch | **added in #36** | **H: unblocks WolfFire F4b (`PushPriceListItemsToNexoJob`, `nexo_push_available`)** |
| Price change (Przecena) | `ICennik.Przecen(pozycje, RodzajPrzeceny, wartosc, funkcjaWyrownywania?, znakKorekty?, korekta?, wyzerujRabat)`; returns the refused positions | `POST .../by-id/{id}/reprice` | **added in #36** | M: promotions, supplier price increases |
| Kit priced from components | `WyliczCeneKompletuZeSkladnikow` | refused with 400; PR #33 adds an explicit switch-off | - | M |
| Create additional price list | `ICenniki.Utworz()`, `UstawJakoDodatkowy(glowny)`, `Pozycje.DodajIPrzepiszParametryPozycjiZGlownego(ids, progress)`, `UstawStatus(uzytkownik, StatusCennika)` | `POST /api/dictionary/price-lists`, `PUT .../by-id/{id}/status` | **added in #37** | M: promotional / client price lists |
| Add / remove positions, tiers | `Pozycje.Dodaj(asortyment[, iloscMin, jm])`, `Usun(pozycja)`, `IloscMinAsortymentu` | - | - | M: new products must appear in the main price list |
| Calculation method / discounts | `DomyslneWyliczajPozycjeWedlug`, `ParametrKalkulacyjny`, `PrzeliczPozycje()`, `RabatDomyslny/Dopuszczalny`, `UstawBlokadeCen` | - | - | L |
| Recalculate after cost change | `AktualizujPozycjeCennika(...)`, `AktualizujCeneKalkulacyjna(...)` | - | - | M |
| Scheduled replacement | `ICenniki.Zastap(cennik, data)` (approved, non-base lists only) | - | - | L |
| Validity window | `Cennik.HarmonogramWaznosci` (`Harmonogram`: `RodzajHarmonogramu`, `DataPoczatkowa/Koncowa`, masks) | read | semantics of the masks need a test before writing | M: time-boxed promotions |
| New price level / main price list | `IPoziomyCen.Utworz()`, `Bazowy = true`, `WypelnijCennik()` | - | deliberately not exposed: changes the whole price structure | - |

### 2.6 Sales and purchase documents and corrections (FS, FZ, PA, KFS, KFZ)

**SDK:**
- Create: `IDokumentySprzedazy.UtworzFaktureSprzedazy/UtworzParagon*/UtworzFaktureZaliczkowa/UtworzFaktureVATMarza`,
  `IDokumentyZakupu.UtworzFaktureZakupu`.
- Realise ZK: `IDokumentSprzedazy.WypelnijNaPodstawieZK(IEnumerable<DokumentZK|PozycjaDokumentu>, DokumentZK,
  ParametryGrupowaniaDS)`.
- Corrections: `UtworzKorekteFakturySprzedazy`, `UtworzZwrotDoParagonu`, `UtworzKorekteFakturyZakupu`, with
  `KorygujPozycjeWedlugId` and `ZwrocCaloscDokumentuZrodlowego`.
- Sample: `Przyklady/PrzykladyRealizacjiDokumentow`.

| Capability | Bridge | Status | WolfFire |
|---|---|---|---|
| Read | `GET /api/documents`, `/{id}`, `/by-number` | partial: full scans; several DTO names wrong. False 404s were seen on 2026-10-06 | H: every document sync |
| Create FS / FZ / PA | `POST /api/documents/sales-invoice`, `purchase-invoice`, `receipt` | write. Payment method on documents is being fixed in another session | H later (write flag) |
| Realise ZK into FS / PA | `POST /api/documents/realize-orders` | **broken: `WypelnijNaPodstawieZK(object[], ...)` cannot bind** (`List<object>`) | **H: `RealizeOrderInNexoJob`** (write flag) |
| Advance / VAT margin invoice | `advance-invoice`, `vat-margin-invoice` | partial: 500 when a line price or discount is sent | L |
| Corrections | `sales-invoice-correction`, `purchase-invoice-correction`, `receipt-return` | broken for line changes: `PozycjeKorygowane` / `Koryguj` do not exist, so corrections are a silent no-op | M |
| Edit header | `PATCH /api/documents/{id}` | broken (`Edytuj`) | L |
| Associations | `POST /{id}/associate`, `DELETE ...` | broken: reports success without saving | L |
| Delete / cancel | - | - | L |

### 2.7 Warehouse documents (PZ, WZ, RW, PW, MM)

| Capability | SDK | Bridge | Status | WolfFire |
|---|---|---|---|---|
| Create WZ / PZ / RW / PW | `IWydaniaZewnetrzne`, `IPrzyjeciaZewnetrzne`, `IRozchodyWewnetrzne`, `IPrzychodyWewnetrzne` | `POST /api/warehouse-documents/wz/pz/rw/pw`, `rw/from-order` | write (the PZ external date is dropped) | M: monthly RW/PW for shipping materials are typed by hand today |
| Create MM | `IWydaniaMiedzymagazynowe` (`DaneDomyslne.WydanieMiedzymagazynowe`), `IPrzyjeciaMiedzymagazynowe.WypelnijNaPodstawieMMW` | `POST .../mm` | **broken: always 500** (`PrzesuniecieMiedzymagazynowe`) | M: WMS (write-free by design today) |
| Read | `Dane`, `Pozycje` | `GET /api/warehouse-documents` | partial: only WZ/PZ are queried, no paging skip, empty dates and values. No `GET /{id}` (WolfFire uses `/api/documents/{id}`) | M |
| Edit / delete / fill from ZK or ZD / corrections | `Znajdz` + `Zapisz`, `Usun()`, `WypelnijNaPodstawieZK/ZD`, `IKorekty*` | `PATCH` (broken, `Edytuj`) | - | L-M |

### 2.8 Orders (ZK, ZD, offers)

**SDK:**
- ZK: `IZamowieniaOdKlientow` (`Utworz`, `Znajdz(encja | Expression)`) and `IZamowienieOdKlienta` (`Pozycje`,
  `UstawTerminRealizacji`, `Przelicz`, `Usun`, `WypelnijNaPodstawieZKRoznicowo`).
- Closing: through a status with `StatusDokumentu.Zamkniety`.
- ZD: `IZamowieniaDoDostawcow` and `IZamowienieDoDostawcy.WypelnijNaPodstawieZK`.
- Offers: `IOferty`.

| Capability | Bridge | Status | WolfFire |
|---|---|---|---|
| ZK read | `GET /api/customer-orders`, `/{id}`, `/by-number`, `/{id}/realization` | detail read (typed `OrderReader`). The list is partial: totals 0; date, status and closed filters return empty | H: order sync uses `/api/documents` |
| ZK create | `POST /api/customer-orders` | partial: 500 when `issueDate` is sent (`DataWystawienia`); line price, discount and manual lines silently dropped | **H: `PushShopOrderToNexoJob`** (write flag) |
| ZK header edit | `PUT /api/customer-orders/{id}` | partial: `statusId` accepted and ignored | M |
| ZK close | `POST /{id}/close` | **broken: returns 200 without closing** | M |
| ZK delete | `DELETE /{id}` | write | L |
| ZD create / read | `POST /api/orders/supplier`, `GET` | broken: 500 when a date or price is sent; read lines are empty | M-H: replenishment is exported to CSV and ordered by hand |
| Offers | `POST /api/orders/offers`, `POST /api/offers`, accept, close | broken: always 500 / 500 with a price; accept and close work | M: OF to ZK is local only |

### 2.9 Editing lines of existing documents

No endpoint edits lines today; `PUT customer-orders` and both `PATCH` endpoints are header-only.

- **Safe targets:** ZK, ZD, OE and drafts. Every manager reopens with `Znajdz(encja)`, and changes are saved with
  `Zapisz()`.
- **Fiscal documents:** FS/FZ lock once they have a KSeF number or are booked, and PA once it is fiscalised. After that
  only corrections are allowed, so line edits there are a fiscal risk.

Verified ZK sequence (sample `OperacjeSferyczne/OperacjeNaDokumentach/UsunPozycje.cs` for removal):
1. `using IZamowienieOdKlienta zk = sfera.ZamowieniaOdKlientow().Znajdz(d => d.Id == id);`
2. Change quantity: `zk.Dane.Pozycje.Single(p => p.Id == lineId).Ilosc = q`. Refuse `q` below the realised quantity
   (`StanRealizacjiZamowienia.ZrealizowanaIlosc`, base unit).
3. Add: `zk.Pozycje.Dodaj(asortyment, ilosc, jednostka)`, then optionally `Cena.NettoPrzedRabatem` and `Cena.RabatProcent`
   (a fraction).
4. Remove: `zk.Dane.Pozycje.Remove(line)`.
5. `zk.Przelicz()`, then `Zapisz()`. Read errors through `SdkErrors`.

WolfFire blocks this in the UI today: "Pozycje ZK z Nexo edytuje się w Subiekcie" (`OrderShow.php`). Value M.

### 2.10 Settlements and payments

**SDK:**
- `OperacjeKasowe().Utworz()`, `OperacjeBankowe().Utworz()`.
- `Rozrachunki().Operacje.RozliczenieOperacjaKasowa/RozliczenieRozrachunkiem/RozliczenieWyciagiemBankowym`, then
  `Rozlicz(pozycja, kwota)`.
- `Dokument.Rozrachunek` is the paid/unpaid source of truth.

| Capability | Bridge | Status | WolfFire |
|---|---|---|---|
| Receivables / settlement state | `GET /api/payments/receivables`, `/api/finance/settlements` | partial: full scans; WolfFire reports `/api/finance/settlements` empty and settlements without due date | **H: 1,007 of 1,020 FS look unpaid (about 5 mln zł "overdue"), so dunning is blocked** |
| KP / KW | `POST /api/payments/cash/kp`, `kw` | **broken: the amount is never assigned**; 500 on `DataUtworzenia` | M (money: must not stay as is) |
| BP / BW | `POST /api/payments/bank/bp`, `bw` | broken: 500 (`Dane.Podmiot`, `Dane.Opis` outside try) | M |
| Settle | - | `documentIdsToSettle` returns 400 | M |
| Payment on FS / PA | `SetPaymentMethodOnDocument` | broken (explicit `IPlatnosciNaDokumencie`); owned by another session | H |

### 2.11 KSeF

**SDK** (sample `Przyklady/PrzykladyKsef`): `FabrykaGeneratorowEFaktury().PobierzAktualny().WygenerujEFaktury(...)`,
`KoordynatorWysylaniaEFaktur.PrzekazDoWysylki/SprawdzStatus/PobierzUpo(List<DokumentElektroniczny>)`, receive with
`Pobierz(...)`, and import with `ObslugaImportuEFaktur.WypelnijNaPodstawieDokumentuElektronicznego`.

| Capability | Bridge | Status | WolfFire |
|---|---|---|---|
| Read / inbox | `GET /api/ksef/documents`, `/summary`, `/inbox` | read | M |
| Receive / import into FZ | `POST /api/ksef/receive`, `inbox/{id}/import` | write (empty error details) | M: purchases are entered by hand from KSeF today |
| Generate / send / status / UPO | `POST generate`, `send`, `status/check`, `upo/download` (+ batch) | broken: arrays passed where `List<>` is required, an ambiguous `null` overload, wrong assembly lookup | L for WolfFire (sending stays in Subiekt); high fiscal risk |

### 2.12 Assembly and production (ZPM / ZPR)

| Capability | SDK | Bridge | Status | WolfFire |
|---|---|---|---|---|
| Assemble / disassemble | `Montuj`, `Rozkompletuj`, `PodajMaksymalnaIloscKompletu`, `WypelnijnaPodstawieZK` | `/api/assembly/*` | write. Custom components are broken (`PozycjeSkladniki.Usun(poz)` does not exist; the no-argument `Usun()` would delete the order) | M: production waits on an owner decision (O5) |
| Shortages | `IBraki.Lista/PosiadaBraki` (explicit) | `GET /api/assembly/{id}/shortages` | broken: always empty | M |
| Regenerate PW | `PrzegenerujAutomatycznePW` | - | - | L |

### 2.13 Stocktaking and stock

**SDK** (sample `InwentaryzacjaPrzyklady`): `ISpisyInwentaryzacyjne.Utworz` with `Pozycje.Dodaj/Usun` (explicit),
`IInwentaryzacje.WyliczRoznice/UstawStatus` (which generates RW/PW), `IMagazynier.IloscWolna`, `IRemanenty`.

| Capability | Bridge | Status | WolfFire |
|---|---|---|---|
| Stock levels | `GET /api/inventory/stock` (snapshot, `X-Stock-Snapshot-At`) | read | H: stock sync, PrestaShop stock push |
| Reservations | `GET /api/inventory/reservations` | read (typed SQL) | M |
| Stocktaking, movements, batches, low stock, valuation | `/api/inventory/*` | broken reads: zero quantities, empty movements (`Asortyment`, should be `AsortymentAktualny`), valuation 0 | L-M (WolfFire keeps a local ledger) |
| Stock count create / close | - | - | M later; closing creates RW/PW, so high risk |

## 3. Roadmap

Order = WolfFire value x (1 / effort) x risk. Fiscal and money writes go last, and only after a joint test on a copy of
the database. Effort is S (under a day), M (1-3 days), L (more).

| # | Item | Value | Effort | Risk | State |
|---|---|---|---|---|---|
| 1 | Conditional price write (`expectedPrice*`, 409) and the rejected-edit fix | H: unblocks WolfFire F4b price push | S | M (prices) | #36 |
| 2 | Typed save errors (`SdkErrors`) in every write controller | H: every refused save currently says nothing | S per controller | L | started in #36 and #38 |
| 3 | Contractor group membership: typed `GET /{id}/contractors` (`Podmiot.Grupy`, one query), add and remove | H: the nightly sync assigns nothing today | S | L | next |
| 4 | Settlement state on FS (paid / remaining / due date) as one typed SQL read | H: dunning, aging, KPIs | M | L (read) | next |
| 5 | Product create and delete, typed | M | S | M | #38 |
| 6 | Price change (Przecena) | M | S | M | #36 |
| 7 | Additional price lists: create, approve and close | M | S | M | #37 |
| 8 | `POST /api/customers` typed (`UtworzFirme/UtworzOsobe`, address, contacts) | H when shop orders go to Nexo | M | M (master data) | next |
| 9 | ZK create fixes (issue date, line prices, manual lines) and close via status | H when shop orders go to Nexo | M | M | next |
| 10 | Product `modifiedSince` / `modifiedAt` (`Naglowek.Zmieniono`), real `type` and `isVariant` | M: replaces the 7-day rolling refresh | S | L | |
| 11 | Main price list positions for new products (`Pozycje.Dodaj`), quantity tiers | M | S | M | |
| 12 | ZK line editing (sequence in 2.9) | M | M | M | |
| 13 | ZD create/read fixes, then ZD to PZ/FZ | M-H: replaces the CSV export | M | M | |
| 14 | Contractor addresses and contacts write; ZK delivery address | M-H: packing and shipping | M | M | |
| 15 | MM create fix (`WydanieMiedzymagazynowe`) and warehouse `GET /{id}` | M | S | M | |
| 16 | Replace `SafeGetAll`/`FindById` scans in the hot read paths | M: fewer timeouts and queueing | M | L | |
| 17 | Realise ZK into FS/PA with typed lists | H when enabled | S code, M test | **H (fiscal)** | after a joint test |
| 18 | KP/KW amount and BP/BW fixes, then settling | M | S-M | **H (money)** | after a joint test. Until then the endpoints should refuse requests |
| 19 | Corrections through `KorygujPozycjeWedlugId` | M | M | **H (fiscal)** | after a joint test |
| 20 | KSeF generate/send typed rewrite | L for WolfFire | M | **H (fiscal)** | owner decision |
| 21 | Product features, templates, photos, groups CRUD clean-up | L | S each | L | |
| 22 | Stocktaking create/close | M later | L | H | |

Do not "fix" `AssemblyController` custom components by calling the no-argument `Usun()`: that deletes the whole order.

## 4. What has to be decided by the owner

- Whether WolfFire may approve additional price lists itself (`approve` / `PUT .../status`), or whether approval stays
  manual in Subiekt.
- Whether `vatRate` on product create should keep setting the purchase VAT rate to the sales rate.
- KP/KW and BP/BW write endpoints: fix them now (money) or switch them to 400 until a joint test.
- Order of the H items 3, 4, 8 and 9 against WolfFire's write-flag rollout.
