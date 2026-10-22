# Changelog

All notable changes to the Nexo Sfera API will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed (2026-10-22) - recalculate-costs did not refresh the base price
- In the first version, `POST .../items/recalculate-costs` ran only `AktualizujCeneKalkulacyjna`.
  - That method recalculates the calculation price from the current base price and leaves the base price as it is.
  - So a kit with base price 0 stayed at 0: the dry run on DMSUMDP04282_0108 returned cost 0, where the components
    give 12.34.
- New `mode` parameter:
  - `full` (default): locks the sales prices first (`UstawBlokadeCen`), then recalculates the whole position from the
    base price (`AktualizujPozycjeCennika`). It needs `lockPrices`.
  - `estimate`: switches on `SzacujKosztKompletuZeSkladnikow` for kits, then runs `PrzeliczSzacunkowyKoszt` and
    `AktualizujCeneKalkulacyjna`.
  - `calculation`: the previous behaviour.
- The sales price guard is unchanged: if a sales price would change, nothing is saved.
- The response now also has `oldEstimatedCost` / `estimatedCost`.

### Added (2026-10-22) - refresh the cost of price list positions and keep the sales price
- `POST /api/dictionary/price-lists/by-id/{id}/items/recalculate-costs` with `productIds` (1-200), `lockPrices`
  (default true) and `dryRun`.
  - It runs `ICennikPozycje.AktualizujCeneKalkulacyjna` on the main positions. The base and calculation price come
    from the price list defaults, which for a kit is the sum of its components. Nexo then recalculates the margin,
    markup and profit, and the sales price stays.
  - `lockPrices` locks the sales prices (`UstawBlokadeCen`), so a later cost change moves the margin and not the price.
- Why: a kit's cost is refreshed only when the price list is updated. After its components change, or for a new kit,
  the price list keeps the old cost or 0, and the margin looks like 100%.
  - On 2026-10-22: 36 new DMSUMDP04282 cards had cost 0.
  - 11 sand DMSUMDP02063 cards had 3.27 instead of 10.15.
- All-or-nothing, with one `ICennik.Zapisz()`. Nothing is saved, and the request returns 400, when:
  - the price list is closed;
  - a product or its single main position is missing;
  - a kit position is priced from its components (`WyliczCeneKompletuZeSkladnikow`);
  - Nexo would change a sales price.

### Fixed (2026-10-22) - discounts were never returned
- `GET /api/discounts`, `/{id}` and `/for-contractor/{id}` read the discount (`Rabat`) through members it does not have:
  `Aktywny`, `OdDaty` / `DoDaty`, `Symbol`, `Opis`, `Priorytet`, `ProcentRabatu`, `KwotaRabatu`. As a result:
  - the default `activeOnly=true` filtered every discount out;
  - without it, mapping threw ("Cannot perform runtime binding on a null reference");
  - the list never carried `subjects`, which the WolfFire discount sync needs to link a discount to contractors.
- The new typed `DiscountReader` runs two SQL queries per page (discounts, then their contractors):
  - `name` from `Nazwa`;
  - the new `kind` (`RodzajRabatu`: Procentowy, Kwotowy, KwotowyOdWartosci, ProcentowyOdWartosci);
  - `type` (Percentage / Amount), with `amountValue` from `Kwota`, `percentValue` from `Procent`, and
    `currencySymbol`. Nexo keeps `Procent` as a fraction ("Standardowy 5%" is 0.05), and `percentValue` reports it in
    percent (5), which is what the WolfFire `PriceResolver` expects;
  - `subjects` from `Podmioty`, now on list rows too.
- A Nexo discount has no symbol, validity period, priority or activity flag. `symbol`, `validFrom` / `validTo` and
  `priority` stay null, and `isActive` is true (the recycle bin is never listed). `activeOnly` and `validAt` filter
  nothing. `by-symbol` is unchanged and still finds nothing, because there is no symbol.
- `pageSize` above 500 or `page` below 1 return 400.

### Fixed (2026-10-22) - contractor create
- `POST /api/customers` always ended with 500. It created the card with the generic `Utworz()` and assigned members that
  do not exist on `Podmiot`: `Symbol`, `NazwaPelna`, `REGON`, `AdresGlowny`, `Kontakty.DodajEmail`. It also wrote the
  wrong `Typ` values (0/1 instead of `TypObiektu`). The address, e-mail, phone and bank account were dropped silently.
- The endpoint is now typed (`CustomerCreator`), following the SDK sample `ImportEksportPracownikow`:
  - the card is created with `UtworzFirme()` or `UtworzOsobe()`; a person needs the new `firstName` and `lastName`;
  - symbol through `Sygnatura.PelnaSygnatura`, or `AutoSymbol()` when it is omitted (`symbol` is no longer required);
  - NIP (Polish check digit validated), EU VAT number (`NIPUE` with `PodatnikUE`), REGON;
  - primary address through `DodajAdres(TypyAdresu.DaneDomyslne.Glowny)` with the country from the dictionary;
  - e-mail, phone and website as primary contacts of the default kinds;
  - `notes`, `isActive`, `isOneTime`, `isEuTaxpayer`, `documentBlock`, and the new `groupIds` (`Podmiot.Grupy`);
  - everything is saved with one `Zapisz()`.
- Responses:
  - 400 lists one message per invalid field. A refused save carries the Nexo errors, read typed through `SdkErrors`.
  - 409 when the symbol or NIP already belongs to a contractor, with that contractor in `data`. A retried request never
    creates a second card.
  - Fields the endpoint does not write (bank account, payment terms, credit limits, SUN, VAT flags other than EU, message)
    return 400 when set; before, they were dropped.
    
### Fixed (2026-10-22) - customer order (ZK) create
- `POST /api/customer-orders` is typed (`CustomerOrderCreator`). The late-bound version:
  - failed with 500 whenever `issueDate` was sent: it set `Dokument.DataWystawienia`, which does not exist; the date is
    `DataWydaniaWystawienia`. WolfFire `PushShopOrderToNexoJob` always sends it;
  - dropped line prices silently (`CenaNetto` / `CenaBrutto` inside `try/catch`);
  - dropped manual lines silently: `DodajUslugeJednorazowa(name, price, quantity)` was called, but the parameters are
    (name, quantity, unit symbol);
  - skipped unknown products and saved the ZK without the line;
  - read every product and contractor for each lookup.
- All-or-nothing:
  - validation first, then the customer (id, or exactly one contractor with the NIP raw or without separators), the
    recipient, the warehouse and every product are resolved in SQL;
  - any problem returns 400 with one message per field, and nothing is created;
  - a refused `Zapisz()` returns the Nexo errors.
- Line prices go to `PozycjaDokumentu.Cena` in the calculation mode of the order: `NettoPrzedRabatem` for net orders,
  `BruttoPrzedRabatem` for gross orders. A price of the other kind is converted with the line VAT rate and rounded to
  0.01. `discountPercent` (percent, 0-100) goes to `Cena.RabatProcent` as a fraction, because Nexo keeps 8.61 % as
  0.0861. Lines without a price keep the price list price.
- The document is created with `UtworzZamowienieOdKlienta()`. Before, it used the default configuration, or `Utworz()`,
  which always throws.
- Unchanged: the response shape (`CustomerOrderDto`, lines in request order). The reservation type, split payment and
  price level are still set late-bound, as before.
- Not applied, as before: item `unit` (products use the sale unit, services the template unit), item `vatRate`,
  `createReservation`, `currency` and `reserveNumber`.

### Fixed (2026-10-22) - refused saves report their errors
- The save error readers of the 13 write controllers read `InvalidData[].Errors` / `MemberErrors` late-bound. Nexo
  entities implement those members explicitly (`ITypedDataErrorInfo`), so the reads found nothing, and a refused
  `Zapisz()` returned 400 with an empty `errors` list.
- The readers now take the errors typed first, through `IObiektBiznesowy.Bledy` (`SdkErrors`, the same helper as #36 and
  #38), and keep the old read as a fallback.
- Affected: customers, customer orders, documents, warehouse documents, offers, orders, payments, products, product
  groups, attributes and templates, contractor groups, KSeF and assembly.
  
### Changed (2026-10-22) - document delta loads only the changed documents
- `GET /api/documents?modifiedSince=…` loads only the documents changed since then: header or settlement change time, the
  same condition as before, in one SQL query. Before, it loaded every document of the database on the SDK thread and
  then kept the changed ones. The other filters and the mapping run on those entities unchanged, so the result is the
  same.
- With `sortBy=modified_*`, a delta reads the stamps of the listed documents only (up to 2000 ids), instead of every
  document.
- Lists without `modifiedSince`, such as the nightly full pass, still scan; replacing that scan needs every list filter
  as SQL.
  
### Changed (2026-10-22) - lookups by id run in SQL
- `DynamicPropertyHelper.FindById` composes `Where(e => e.Id == @id)` on `Dane.Wszystkie()`, so the lookup runs as one
  SQL query. It used to enumerate `Wszystkie()`, materializing the whole entity set on the SDK thread until the id came
  up. Sets that are not a LINQ query, or whose element has no int `Id`, are still scanned.
- 53 hand-written copies of that scan in the controllers (`foreach SafeGetAll … if GetId == id … break`) now call
  `FindById`, with the same result. Among them are the details and write lookups of contractors, documents, customer
  orders, product groups, attributes and templates, KSeF, and `GET /api/products/{id}`.
  - The other product lookups are left to #38, and the ZK create lookups to #46; both rewrite them typed.
- The WolfFire product detail refresh calls `GET /api/products/{id}` once per product, so each call no longer reads the
  whole catalogue.
  
### Added (2026-10-22) - settlement state of many documents
- **`GET /api/documents/settlements`** returns documents with the state of their settlement (`Dokument.Rozrachunek`):
  `amount`, `remainingAmount`, `paidAmount`, `unsettledAmount`, `currency`, `dueDate`, `nextDueDate`, `overdueAmount`,
  `daysOverdue`, `installmentCount`, `isSettled`, `isOverdue`, `settlementModifiedAt`, plus the document number,
  contractor, gross value and `amountToPay`.
- It is one typed SQL query for the page and one count (`DocumentSettlementReader`). Every filter runs in SQL: `symbols`,
  `ids` (up to 200), `customerId`, `type`, `status` (same meaning as `/api/payments/receivables`), `open`, `overdue`,
  issue and due date ranges, `modifiedSince` (document or settlement changed, as in `GET /api/documents`), `isCanceled`
  and `includeWithoutSettlement`. Sort is `id`, `due_date`, `issue_date` or `issue_date_desc`. `pageSize` goes up to 1000.
- Overdue is decided per instalment of the payment schedule (`Rozrachunek.Pozycje`): `overdueAmount` is the remaining
  amount of the instalments past their due date, and `daysOverdue` counts from the earliest open one. A settlement without
  instalments uses `Rozrachunek.TerminPlatnosci`.
- Read only. The list and detail of `GET /api/documents` are unchanged.

### Changed (2026-10-22) - product list as one SQL query
- `GET /api/products` runs the filters (`search`, `type`, `activeOnly`, `modifiedSince`), the order and the page as one SQL
  query and projects only the list columns. Before, every request loaded every product card on the SDK thread
  (`Dane.Wszystkie()` materialized in memory) and filtered and paged it in memory: a 100-row page of a 6000-card
  catalogue cost 6000 entities, and all other bridge requests queued behind it.
- Same response and semantics. `search` still matches the symbol, the name or any unit barcode, now case-insensitive
  through the database collation and trimmed. The default order is explicitly by id; it was implicit before.

### Added (2026-10-22) - product delta and real kind/variant
- **`GET /api/products?modifiedSince=…&sortBy=modified_asc|modified_desc`**: only products whose card changed at or
  after the given time (`Asortyment.Naglowek.Zmieniono`, one SQL query). It is the same contract as the document delta.
  Every list row has `modifiedAt` and `createdAt`, so a client can detect bridges that support the delta.
  - Not covered: prices in price lists, stock levels, and products moved to the recycle bin (they leave
    `Dane.Wszystkie()`). They are not part of the card, so a periodic full pass stays.
- **Real product kind and variant**:
  - `type` in the list and the detail comes from `Asortyment.Rodzaj` through the SDK kind checks (`CzyKomplet`,
    `CzyUsluga`). Before, it was always `Goods`, because the mapper read `Rodzaj_Id`, which does not exist. Kinds that
    are neither goods, service nor set are still reported as `Goods`; the new `kindName` gives the Nexo name.
  - The `type` list filter, previously ignored, now filters. `Material` matches no Nexo kind.
  - `isVariant` / `modelId` come from `Asortyment.Model` (before: always `false` / `null`). `modelId` is the id of the
    Nexo model card (`Model`), not a product id.
  - The product detail has `kindName` and real `createdAt` / `modifiedAt` (local time, as for documents).
- `sortBy` other than `modified_asc` / `modified_desc`, and `page` / `pageSize` below 1, return 400.

### Fixed (2026-10-22) - contractor group membership
- Group membership is `Podmiot.Grupy` (a contractor belongs to any number of groups). The endpoints read and wrote
  `Podmiot.Grupa`, which does not exist:
  - `GET /api/contractor-groups/{id}/contractors` always returned an empty list, so the WolfFire group sync assigned no
    contractors;
  - member counts (`includeContractorCount`, `contractorCount` in the group detail) were always 0;
  - adding a member failed, and removing one always answered "not in this group".
- All of them now go through the typed `ContractorGroupMembers`:
  - the member list is one SQL query with paging, ordered by short name;
  - counts are one SQL query for all groups;
  - add and remove edit `Podmiot.Grupy` and keep the contractor's other groups.
- Behaviour changes:
  - adding a contractor that is already in the group answers 200 without saving;
  - `pageSize` above 500 or `page` below 1 returns 400;
  - the bulk add lists every contractor that was not added in `errors` (the count in `data` is unchanged in meaning).
- Members of subgroups are not included. The group hierarchy (`Grupa.KategoriaNadrzedna`) is not read yet.

### Fixed (2026-10-22) - kit components could not be added or removed
- **`POST /api/products/{id}/components`** and **`DELETE /api/products/{id}/components/{componentProductId}`** failed
  with 500 `'AsortymentKartotekowyBO' does not contain a definition for 'Dodaj'`. The business object implements the
  components interface explicitly, so the `dynamic` call never found `Dodaj`/`Usun`. Both now go through the typed
  `IAsortyment.Skladniki` (`ISkladnikiKompletu.Dodaj(symbol, quantity, unit)` / `Usun(productId)`).

### Added (2026-10-22) - fixed price for kits priced from components
- **`PUT /api/dictionary/price-lists/by-id/{id}/items`:** kit positions whose price is the sum of their components
  (`WyliczCeneKompletuZeSkladnikow`) are still refused by default. With `disableKitComponentPricing: true` on the item the
  flag is switched off and the sent price becomes the fixed kit price; the result reports `kitComponentPricingDisabled`.

### Fixed (2026-10-22) - stale SDK DLLs after a build against the same nexo version
- **SDK sync:** when the runtime `InsERT.Moria.Sfera.dll` matched the nexo deployment, the synchronizer only added
  missing DLLs and never replaced existing ones. MSBuild copies the dependency closure found in `src\lib\nexo-sdk`, and
  earlier runs leave files behind, so `bin` could mix SDK 62 Sfera with modules from an older SDK. Sfera then refused the
  up-to-date database with "baza zawiera oczekujące aktualizacje". With `PreferDeploymentBinaries` (the default), a hash
  match now re-aligns every DLL that differs from the deployment, the same way as after a version change. Identical
  files are skipped.
- **SDK sync, related directories:** the fill of DLLs missing from the deployment searched every nexo deployment and
  `.zip-cache` of every version (and Program Files) and copied any missing file, including InsERT modules from other
  versions (seen on 2026-10-22: `InsERT.Mox.VMPackage.55.0.1.8249.dll` from a 2025 deployment). InsERT assemblies are
  now taken only from the selected deployment; third-party DLLs are still filled from anywhere.

### Fixed (2026-10-22) - price list write did not build
- `PriceListWriter` called `ICennik.PodajBledy()`, which is not part of the compiled interface (CS1061). Save errors are
  now read late-bound and best effort (`InvalidData[].Errors` + `PodajBledy()`), the same way as in the other controllers.

### Added (2026-10-22) - price change and conditional price write
- **`POST /api/dictionary/price-lists/by-id/{id}/reprice`** runs Nexo's price change (Subiekt: Przecena,
  `ICennik.Przecen`) on the positions of 1-200 products: `kind` (`decreasePercent` / `increasePercent` /
  `decreaseAmount` / `increaseAmount`), `value` (percent as a fraction, 0.10 = 10%), optional `roundingFunction` (the
  names positions report, e.g. `WyrownywanieDoJednostek`), optional signed `priceCorrection` (-0.01 for .99 endings),
  `resetDefaultDiscount`, `mainOnly` (quantity tiers are repriced by default). All-or-nothing: a closed price list, an
  unknown product, a product without a position, a kit position priced from its components or any position Nexo cannot
  reprice returns 400 and nothing is saved. The response lists every position (`isMain`, `minQuantity`, old and new
  prices, `calculationParameter`, `status`).
- **`PUT /api/dictionary/price-lists/by-id/{id}/items` conditional write:** an item may carry `expectedPriceGross`
  and/or `expectedPriceNet`, the price the client last read. When the current price differs (after rounding to the
  currency precision), nothing is saved and the request returns **409** with one message per product. Items without
  expected prices behave as before. This is the precondition WolfFire's price push (F4b) waits for.

### Fixed (2026-10-22) - price write ignored a rejected position edit
- `PUT /api/dictionary/price-lists/by-id/{id}/items` ignored the result of `ZakonczEdycje()`. When Nexo rejected a
  position edit the other edits were still saved and the item was reported as `updated`. A rejected edit now returns
  400 and nothing is saved.
- Price list save errors were always empty, so a refused save only said "Nexo refused to save the price list". The
  late-bound reader looked up `Errors` on the entities, which implement `ITypedDataErrorInfo` explicitly, and called the
  `PodajBledy` extension method through `dynamic`, which never binds. New `SdkErrors` reads the typed
  `IObiektBiznesowy.Bledy` first. The other controllers still use their own late-bound readers.

### Added (2026-10-22) - price list price write
- **`PUT /api/dictionary/price-lists/by-id/{id}/items`** sets the gross or net price of main price list positions:
  `{"items": [{"productId": 105003, "priceGross": 38.00}]}`, 1-200 items, exactly one of `priceGross` / `priceNet` per
  item. Writes go through the price list business object (`ICennik.Pozycje`, typed `PriceListWriter`). Nexo derives
  the other price and the calculation parameter. The calculation method, rounding function and quantity tiers are not
  changed, so a later base price change recalculates the position like a price set in Subiekt.
- All-or-nothing. Unknown products, a missing or ambiguous main position, or a kit position priced from its components
  (`WyliczCeneKompletuZeSkladnikow`) return 400 before anything is edited, and the price list is saved once. The response
  lists `oldPriceNet/oldPriceGross`, the saved `priceNet/priceGross`, `calculationParameter` and `status`
  (`updated` / `unchanged`) per product.
- The `priceNet` refusal of `PUT /api/products/{id}` now points to this endpoint.

### Added (2026-10-22) - additional price lists and their status
- **`POST /api/dictionary/price-lists`** creates an additional price list supplementing a main one (Subiekt: "Dodaj
  cennik dodatkowy"; SDK `ICenniki.Utworz()` + `ICennik.UstawJakoDodatkowy`, as in the SDK sample
  `ObslugaCennikow.DodawanieCennikaDodatkowego`): `title`, optional `subtitle` / `description`, `mainPriceListId`,
  optional `productIds` (0-200; positions get the main price list's calculation parameters through
  `DodajIPrzepiszParametryPozycjiZGlownego`, so they start at the main prices) and `approve`. The new price list is
  **defined** (a draft that takes no part in pricing) unless `approve: true`. Returns 201 with the price list header.
  All-or-nothing: an unknown, non-main or closed main price list, an unknown product, a product without a main position
  in the main price list, or a refused save returns 400 and nothing is created. Main price lists (a new price level)
  are not created through the API.
- **`PUT /api/dictionary/price-lists/by-id/{id}/status`** sets `defined` / `approved` / `closed` on an additional price
  list (`ICennik.UstawStatus`). Main price lists are refused with 400. The status is set as the Nexo user the API is
  logged in as (`Uzytkownik.Login` = the API operator); when no such user exists the request returns 400.

### Added (2026-10-22) - product group change
- **`PUT /api/products/{id}` `groupId`** moves the product to an existing product group (`Asortyment.Grupa`, ids from
  `GET /api/product-groups`). An unknown group returns 400 (`groupId: product group 123 not found`). The change is
  reported in `fieldResults` with the old and new group names.

### Fixed (2026-10-22) - product create and delete are typed
- **`POST /api/products`** set every field on a `dynamic` and failed with 500 for `ean` (`Asortyment.EAN`), `pkWiU`
  (`PKWIU`), `weight` (`Masa`), `volume` (`Objetosc`) and `priceNet` (`CenaNetto`), none of which exist. It silently
  ignored `type`, `saleUnit`, `purchaseUnit` and `vatRate`, and applied `templateSymbol` only when it happened to equal
  a template's default product symbol. It is now typed and all-or-nothing (new `ProductCreator`), following the SDK
  sample `ObslugaKartotek`: `IAsortymenty.Utworz()` + `WypelnijNaPodstawieSzablonu`. The kind, base unit and VAT come
  from `templateId` (new), the legacy `templateSymbol` (must match exactly one template) or the default template of
  `type` (Goods / Service / Set = kit; default Goods, Material refused). `symbol` is optional (omitted:
  `AutoSymbol()`). New `baseUnit` (dictionary unit instead of the template's, `UstawPodstawowaJednostkeMiary`),
  `groupId` and `components` (kit only, typed `IAsortyment.Skladniki.Dodaj`). `vatRate` (a symbol as products report
  it) sets the sales and purchase VAT rate. `name`, `description`, `ean`, `pkWiU`, `weight`, `volume`, `groupId` and
  `symbol` go through the same `ProductWriter` as `PUT /api/products/{id}` (duplicate symbol and barcode checks
  included). `saleUnit` / `purchaseUnit` must be the base unit, `priceNet` returns 400. Any problem returns 400 with
  "field: reason" and nothing is created; save errors are read typed (`SdkErrors`, `IObiektBiznesowy.Bledy`) instead
  of the late-bound reader, which always came back empty. `saleUnit`, `vatRate` and `type` lost their implicit
  defaults ("szt.", "23%", Goods), which were never applied.
- **`DELETE /api/products/{id}`** loaded every product to find one and called `Usun()` on a `dynamic`. It now looks the
  product up by id, deletes it through the typed business object and says whether Nexo kept it in the recycle bin
  ("Product moved to the recycle bin" / "Product deleted"). A product already in the recycle bin returns 200 instead of
  404, so a retried DELETE is not reported as a missing product. Nexo's refusal reasons are returned with the 400.

### Fixed (2026-10-22) - product group id
- **Products, `groupId`** was always `null`: it was read from `Grupa_Id`, which `Asortyment` does not have. It now
  comes from the `Grupa` navigation, the same object that already gave `groupName`.

### Fixed (2026-10-22) - decimal range validation on a Polish Windows
- **`POST /api/products/{id}/components`** returned 400 `0.000001 is not a valid value for Decimal` for every request,
  and so did `POST /api/products/{id}/units` (same attribute on `newUnitCount`/`baseUnitCount`; `RangeAttribute` parses
  its limits before it checks for null, so the request fails even without those fields). `[Range(typeof(decimal), "0.000001",
  ...)]` parses its limits in the current culture, and on a pl-PL host `0.000001` is not a number (the separator is a
  comma). The limits are now parsed in the invariant culture (`ParseLimitsInInvariantCulture`). No other decimal
  `Range` uses string limits.
  
### Documentation (2026-10-22) - SDK coverage and roadmap
- New `docs/SDK-COVERAGE.md`: per area (products, groups, contractors, price lists, sales/purchase and warehouse
  documents, orders, line editing, settlements, KSeF, assembly, stocktaking) what SDK 62 offers (verified members), what
  the bridge exposes and whether it works, and what WolfFire uses. Ends with a prioritized roadmap. Cross-cutting
  findings: save errors were never returned (explicit `ITypedDataErrorInfo`), `dynamic` calls on explicit interface
  members, members that do not exist, false 200s and full-table scans under the SDK lock.

### Changed (2026-10-22) - SDK 62.0.0.9537
- **SDK reference bumped to nexo 62.0.0.9537** (README, `scripts/setup-sdk.ps1`, local-SDK fallback in `build.yml` and
  `release.yml`). Reviewed `ZmianyAPI_61.1.1_62.0.0.txt` and `ZmianyModeluDanych_61.1.1_62.0.0.txt`, and diffed the
  public metadata of the referenced assemblies between 61.1.0.9431 and 62.0.0.9537 (which also covers 61.1.1). The
  bridge uses none of the removed types or members. Removed in 62: online calendars, automatic action definitions,
  Portal Podatkowy VAT/NIP checks, the e-invoice generators FA1/FA2, `OperacjaSP.NumerKSeF` (now `NumeryKSeF`),
  `ZgloszenieKlienta.DataZakonczenia`, `Paczka` on courier shipment DTOs, the `Link*WSerwisieZewnetrznym` overloads
  without an integration account, and the old `IWydrukNaklejek.Drukuj` overloads. `OstrzegajOPowtorzonymNumerzeKSeF`
  changed from `bool` to `byte`; the bridge does not read it.
- SDK 62 adds standalone assemblies (`InsERT.Mox.DataModeling.Abstractions`,
  `InsERT.WebServices.Client.KSeF2.CollectiveIdentifiers`, `InsERT.WebServices.Extensions.Http`,
  `InsERT.Mox.Controls.TelerikUI`) and drops `CoreWCF.*`. The csproj references none of them; `NexoSdkSynchronizer`
  copies them from the nexo installation at startup. The SDK still targets .NET 8.
- **SDK 62.0.0 is now the minimum.** Production nexo runs 61.1.1 and is upgraded to 62, so the bridge no longer keeps
  building on 60.x/61.x. New members are read typed (`SdkMember.Read` on the entity), so a wrong name fails the build
  instead of returning `null`. `Adres.GLN` is typed again (the by-name read from 2026-10-10 existed only for 60.x).
  The last 61.x state of the code is tagged `sdk-61.1.0.9431`.
- **Deploy order:** upgrade nexo on the server first, then deploy this build. Against a 61.x installation the three new
  fields come back `null`, because the typed read guard swallows the missing member.

### Added (2026-10-22) - SDK 62.0.0 fields
- **E-commerce, `GET /api/ecommerce/shipping-orders` and `/{id}`:** `shipByDate` (`ZamowienieWysylkowe.WysylkaDoDnia`),
  the ship-by date set by the marketplace.
- **Payments, `GET /api/payments/receivables`, `/{id}`:** `ksefNumber` (`Rozrachunek.NumerKSeF`), the KSeF number of
  the settled invoice, for matching payments to e-invoices.
- **KSeF, `GET /api/ksef/documents`, `/{id}`, `/by-ksef-number/{ksefNumber}`, `/inbox`:** `deliveryOrServiceEndDate`
  (`DokumentElektroniczny.DataZakonczeniaDostawyLubUslug`).

### Added (2026-10-13) - product symbol change
- **Products, `PUT /api/products/{id}` `symbol`.** Changes `Asortyment.Symbol` in the same all-or-nothing update as the
  other fields, reported in `fieldResults` (`updated` / `unchanged`). Empty or whitespace symbols return 400, and so does
  a symbol already used by another product (`symbol: X already belongs to product(s) 123`); products in the recycle bin
  are not visible to that check and are left to Nexo's own validation on save. The card keeps its id, so documents,
  stock and price list positions stay attached. Used by WolfFire to renumber symbols that break its numbering (one
  number per product, supplier + group prefix) instead of editing them one by one in Subiekt.

### Fixed (2026-10-11) - failed reads no longer look like real values
- **Customers, contact kinds.** A failed read of the default contact kinds was cached for the tenant as "no kinds"
  until reconnect, so every customer came back with `email: null` and `emails: []`. WolfFire reads `null` as "e-mail
  removed in nexo" and clears the contact (and then Google Contacts). The lookup now throws, so nothing is cached and
  the request fails.
- **Customers, contacts.** A failed read of `Podmiot.Kontakty` was returned as an empty list. It now fails the card.
- **Customers, `full=true`.** A card that cannot be mapped is returned in the light list shape (`id`, `name`, `taxId`,
  `phone`, `isActive`, `contractorType`) instead of a full card with empty `email`, `regon`, `krs`, ... A client keeps
  the values it has.
- **Price lists, prices.** A failed read of `CenaNetto`/`CenaBrutto` became a valid `0.00`. The request now fails.

### Fixed (2026-10-10) - build against SDK 60.x
- **Customers, address `gln`.** `Adres.GLN` was read as a typed member, which exists only from SDK 61.0, so the bridge
  did not compile against a 60.x SDK (`CS1061: 'Adres' does not contain a definition for 'GLN'`). It is now read by
  name: the value on 61.x, `null` on 60.x. No other member added since #20 is missing in SDK 60.

### Fixed (2026-10-09) - SDK field mapping audit (members that do not exist in SDK 61.1)
All fixes use typed, compile-checked readers/writers (`Helpers/ProductReader`, `ProductWriter`, `CustomerReader`,
`OrderReader`, `DocumentReader`); old JSON fields keep their names, new fields are additive.
- **Products, EAN (H2).** `ean` was read from `Asortyment.EAN` (missing). It is now the primary barcode of the base unit
  (`JednostkiMiar[].PodstawowyKodKreskowy`), falling back to any base unit barcode, the sale unit, then the first unit
  with one. New `eanUnitSymbol`, `barcodes[]` (`code`, `unitSymbol`, `isPrimary`), `units[].primaryBarcode`,
  `units[].barcodes`. `units[].barcode` keeps its meaning: the collective package code (`KodKreskowyOpakowania`), not
  the EAN. `GET /products/by-ean/{ean}`, the list `search` and the MCP product tools match unit barcodes in one SQL
  query; inventory `productEan` (read `KodEan`, missing) is filled the same way.
- **Products, `isActive`** was false for every product (`Aktywny` is missing). The SDK has no activity flag on
  `Asortyment`; `isActive` is now `!IsInRecycleBin` (inactive = recycle bin, which `Dane.Wszystkie()` never returns).
- **Products, other reads:** `pkWiU` (member is `PKWiU`), `weight` in kg from the base unit `Masa` (+ `weightUnitSymbol`),
  `volume` + `volumeUnitSymbol` from the base unit, `saleUnit`/`purchaseUnit` symbols (`.JednostkaMiary.Symbol`), new
  `baseUnit`, `vatRate`/`vatRateSalesId` (`StawkaVatSprzedaz`). `priceNet`/`priceGross` stay null (prices live in price
  lists).
- **`PUT /api/products/{id}`** assigned missing members (`EAN`, `CenaNetto`, `Aktywny`, `PKWIU`, `Masa`, `Objetosc`) on a
  `dynamic`, so any update with those fields returned 500, name change included. Now typed and all-or-nothing:
  `name`, `description`, `ean` (+ optional `eanUnitSymbol`; a code of another product is refused), `pkWiU`, `weight`
  (kg), `volume`. `priceNet`, `vatRate` and `isActive` are rejected with 400 (`errors[]`: "field: reason"). The response
  `data` is the product plus `fieldResults[]` (`field`, `status` updated/unchanged, `oldValue`, `newValue`, `unit`).
- **Customers, `full=true` (C4)** was declared but ignored. It now returns the full card per item
  (`CustomerFullListItemDto` = `CustomerDto` + `name`), filters/order (by id)/paging in SQL, `pageSize` 1..200, duration
  logged. The card itself read missing members: `symbol` (now `Sygnatura.PelnaSygnatura`), `fullName` (`Firma.Nazwa`, or
  first + last name for persons), `regon` (`Firma.REGON`), `email` (contacts by `RodzajeKontaktu.DaneDomyslne.Email`;
  matched `Kontakt.Typ`, missing), `bankAccount`/`bankName` (`RachunekPodstawowy.Numer` / `PodmiotBankowy`), `type`
  (`Podmiot.Typ` is `TypObiektu`, Firma = 2: every company was reported as a person; the legacy list `type` filter is
  fixed too). New: `firstName`, `lastName`, `krs`, `emails[]`, `emailDomain` (`Domena`, the previous `website` value,
  kept as its fallback), `creditLimitCurrency`, `salesCreditLimit`, `deliveryCreditLimit`, `orderCreditLimit`,
  `deliveryAddress` (`DomyslnyAdresDostaw`), `address.countryEuCode`.
- **`PUT /api/customers/{id}`**: `fullName`/`regon` are written to `Firma` (they threw → 500); on a person they are
  rejected with 400. Every other request field PUT never wrote is now rejected with 400 instead of being ignored.
- **Customer orders**: lines read `IloscZrealizowana`/`Zarezerwowana`/`IloscZarezerwowana` (missing). Realization now
  comes from `StanRealizacjiZamowienia` (`ZrealizowanaIlosc` in the base unit, `ProcentowyStanRealizacji` as a fraction)
  and `IloscDoRealizacji.PozostalaIlosc`; reservations from `Rezerwacja` (`Ilosc - IloscZrealizowana`, stock unit).
  `quantityRealized`/`quantityRemaining`/`reservedQuantity` are in the line unit; new `*InBaseUnit`, `realizationPercent`,
  `lastRealizationDate`, `realizingDocumentNumbers`, `isRealizationBlocked`, `reservationKind`, `reservationExpiresAt`,
  `baseUnit`, `quantityInBaseUnit`. Lines also get the real product id, unit, prices, values and VAT; the header gets
  totals, currency, issue date, status, created/modified and `realizationPercent`. `GET .../{id}/realization` adds
  `lines[]`.
- **`GET /api/inventory/reservations`** scanned every order with the same missing members and returned nothing; it is
  now one SQL query over open `Rezerwacja` of ZK lines (new `reservationKind`, `totalReservedQuantity`,
  `consumedQuantity`).
- **Documents**: the list gains `isCanceled` (`StatusDokumentu.Uniewazniony`), `createdAt`/`modifiedAt`
  (`Naglowek.Utworzono/Zmieniono`) and `settlementModifiedAt` (`Rozrachunek.Naglowek.Zmieniono`); new `modifiedSince`
  filter (document or settlement changed) and `sortBy=modified_asc|modified_desc`; `isCanceled` filters on the real
  status. The detail's `isCanceled`/`createdAt`/`modifiedAt` read `Anulowany`/`DataUtworzenia`/`DataModyfikacji`
  (missing) and are now filled.
- **Payments (C6)**: `documentIdsToSettle` on KP/KW/BP/BW was accepted and ignored; a non-empty list now returns 400 and
  nothing is created.
- **`GET /api/inventory/stock`** scanned all products for every page: the sorted result is a snapshot per tenant and
  filter set, rebuilt on `page=1`, `refresh=true` or after 5 minutes (`X-Stock-Snapshot-At` header); warehouses are read
  once per scan instead of once per stock row.

### Security (2026-10-09)
- New `ISferaService.GetTenantState<T>`: cross-request state owned by the pooled connection and partitioned by
  `TenantCacheKey` (database, API key operator, warehouse, branch; never the password). Every cache of SDK data must use
  it instead of a static field. The first version of the stock snapshots (commit `e3ec236`) used a process-wide
  dictionary and could serve one company's stock to another; fixed in `e8f1a5a` before release. **Never install a build
  between those two commits.**

### Added (2026-10-08) - price lists by id (COD-995 F4a-R, read only)
- `GET /api/dictionary/price-lists/by-id/{id}` - price list header by `Cennik.Id` (price lists have no symbol in Nexo).
- `GET /api/dictionary/price-lists/by-id/{id}/items` - positions read through the price list business object
  (`ICenniki.Znajdz(Cennik)` -> `ICennik.Pozycje` = `ICennikPozycje`, items `IUproszczonaPozycjaCennika`) instead of the
  entity collection the SDK discourages. Query: `productIds` (comma-separated, max 200, looked up with
  `ZnajdzPozycjeCennika(Asortyment)`, unknown ids in `missingProductIds`), `mainOnly`, `activeProductsOnly`
  (`WszystkieAktywne`), `page`, `pageSize` (max 1000). Response = `PagedResponse` + `priceList` (header read in the same
  SDK call) + `missingProductIds`. Position fields: main flag and quantity tiers (`isMain`, `minQuantity`,
  `quantityTierCount`), net/gross price, currency, sales VAT (`vatRateSymbol`, `vatRate`, `vatRatePercent` from
  `StawkaVATSprzedaz`/`StawkaVATSprzedaz_Stawka`), base price, calculation price/parameter/method, base price function and
  source price list, rounding function and correction, price after calculation/rounding, estimated cost, minimum margin,
  `updatedAt` (`DataAktualizacji`), supplier/primary supplier/manufacturer ids.
- `PriceListDto` (also on `GET /api/dictionary/price-lists` and `.../price-lists/{symbol}`) gains `title`, `subtitle`,
  `status`/`statusCode`/`isApproved`, `isBase`, `priceLevelId/Symbol/Name`, `mainPriceListId` (base list of the same level
  for an additional list linked with `UstawJakoDodatkowy`), `currencyPricePrecision`, `calculatedPriceKind`,
  `dynamicPricing`, default calculation method/margin/markup/profit, default base price function + source price list,
  default rounding function, zero/below-margin policies, dates and the validity `schedule` (`HarmonogramWaznosci`).
- `PriceLevelDto` gains `currencySymbol`, `basePriceListId` + `basePriceListCount`, `priceListIds`,
  `basePriceSourcePriceListId` (`CennikCenyBazowejId`) and `basePriceFunctionId/basePriceFunction`.
- New `Helpers/PriceListReader` - typed (non-`dynamic`) reader, so every SDK member name is checked at compile time.

### Unchanged on purpose
- Legacy symbol routes (`price-lists/{symbol}`, `price-lists/{symbol}/items`) and the legacy fields of `PriceListDto`
  (`symbol`, `name`, `validFrom`, `validTo`, `isActive`, `itemCount`) and `PriceLevelDto` (`isDefault`, `isActive`,
  `priority`) keep their previous values. `PoziomCen` has no `Domyslny` member; `isDefault` stays `false` so consumers
  that derive a company default price level from it do not switch document pricing.

### Fixed (2026-10-09) - supplier ids of a price list position (COD-1146)
- `supplierIds` on price list positions is `null` when the SDK fails to read `IdDostawcowAsortymentu`, instead of `[]`.
  WolfFire reads `[]` as "the product has no suppliers" and removes its supplier links after a complete sync; `null`
  means "unknown" and leaves them. A product without suppliers still returns `[]`.


### Fixed (2026-09-04)
- Inventory `unit` was always "szt.": `JednostkaMagazynowa` is a `JednostkaMiaryAsortymentu` whose symbol lives in
  `JednostkaMiary.Symbol`. New `DynamicPropertyHelper.StockUnitSymbol/UnitSymbol` (warehouse unit → base unit →
  "szt.") used for stock rows and document positions in the inventory endpoints — threads now report `m`.
- **`GET /api/inventory/stock` (and `stock/low`, `stock/product/{id}`, MCP inventory tools) returned no rows**: products were
  filtered on `JestHandlowy`/`JestMagazynowy`, which do not exist on `Asortyment` (always false). New
  `DynamicPropertyHelper.TracksStock` uses the kind dictionary `Rodzaj.StanyMagazynowe` (false = service) with a fallback
  to existing stock rows. Warehouses are resolved from a dictionary built once per request instead of rescanning the
  manager for every stock row.
- Root cause of the empty unit conversions: `PrzelicznikJednostkiNadrzednej/Podrzednej` are `WrappedEntityCollection`s, not
  single references — `MapUnits` now iterates them (the SQL fallback stays as a safety net).
- **`ProductDto.Units` conversions were empty** (`toBaseFactor: null`, one all-null entry in `conversions`) on a live
  instance: the EF lazy proxies behind `PrzelicznikJednostkiNadrzednej/Podrzednej` do not expose their members through
  reflection. `MapUnits` now drops empty converter entries and, whenever a non-base unit has no factor, reads the rows of
  `ModelDanychContainer.PrzelicznikiJednostekMiarAsortymentu` directly (read-only SQL, unit ids only) — thread spool
  `szt` = 5000 `m` now yields `toBaseFactor: 5000`.
- `GET /api/products/debug/properties/{id}?path=JednostkiMiar.1.PrzelicznikJednostkiNadrzednej` (development only)
  walks properties / collection indexes before dumping — for diagnosing nested SDK objects without rebuilding.

### Changed (2026-09-04)
- **Upgraded to InsERT Nexo SDK 61.1.0.9431** (from 61.0.0.9362). Version strings bumped in `README.md`, `scripts/setup-sdk.ps1`,
  `.github/workflows/build.yml` + `release.yml` (via the `sync-sdk-version.ps1` logic); `/docs/nexoSDK_*/` now ignored by git as a pattern.
  61.1.0 adds no assemblies and removes no types; changes are e-commerce extension DTOs (pagination, photos, serial numbers,
  customs tariff code), `IZdjecie.WygenerowanePrzezAI`, `DokumentDane.WygenerowanePrzezAI`, 3 new `RodzajFakturyZaliczkowej` values
- **Verified SDK property names** (new `docs/SDK-ENTITY-REFERENCE.md`, extracted from the SDK assembly metadata) and fixed the mappers that
  read properties which do not exist on the SDK entities and therefore silently returned `0`/`null`:
  - Documents (list + detail + FS/FZ/ZK mappers): totals now from `Dokument.Wartosc.NettoPoRabacie/BruttoPoRabacie/VatPoRabacie`
    (previously `WartoscNetto`/`WartoscBrutto`/`WartoscVat` -> always 0); `SaleDate` from `DokumentHandlowy.DataSprzedazy` with fallback;
    `IssueDate` unified; **`DueDate`/`PaymentDate` now come from `PlatnosciDokumentow[].Termin` (else `Rozrachunek.TerminPlatnosci`)** -
    `Dokument.TerminPlatnosci` never existed, so due dates were always null; `PaymentDays` from `PlatnoscDokumentu.TerminDni`
  - Document lines: unit from `PozycjaDokumentu.JednostkaMiaryAs.JednostkaMiary.Symbol` (previously `Jednostka`/`JednostkaMiary` -> always "szt.");
    discounts from `Cena.RabatProcent/RabatWartosc`; new `QuantityInBaseUnit` (`IloscWJednostceBazowej`) and `BaseUnit`
  - Warehouse document lines (WZ/PZ/RW/PW/MM): product from `AsortymentAktualny` (previously `Asortyment` -> `ProductId`/`ProductSymbol` were null),
    unit from `JednostkaMiaryAs`, new `UnitId`, `QuantityInBaseUnit`, `BaseUnit`
  - Settlements (`PaymentsController` receivables/overdue/balance): `KwotaPozostala` instead of non-existent `KwotaDoRozliczenia`,
    `TerminPlatnosci` instead of `DataPlatnosci`, `DataPowstania`/`DataDokumentuZrodlowego` instead of `DataWystawienia`,
    `DocumentId` from `Rozrachunek.Dokument.Id` - status/overdue filters and `DaysOverdue` were computed from missing fields before
- **Units of measure honoured on document creation**: `CreateDocumentItemRequest.Unit` is now optional (`null` = product default unit;
  previously the hard-coded default `"szt."` was ignored anyway) and, when given, the line is added with
  `Pozycje.Dodaj(asortyment, qty, JednostkaMiaryAsortymentu)` for sales invoices, receipts, purchase docs, corrections and all
  warehouse documents (WZ/PZ/RW/PW/MM). Unknown units are rejected with HTTP 400 listing the product's available units
  (`ValidateRequestedUnits`) instead of silently posting the quantity in the base unit

### Added (2026-09-04)
- **Paid / unpaid state of invoices** - `DocumentDto` gains `Settlement` (`DocumentSettlementDto`: amount, remaining, unsettled, due date,
  last settlement date, days overdue, `IsSettled`/`IsOverdue`, from `Dokument.Rozrachunek`), `Payments` (`DocumentPaymentDto[]`: kind
  prepayment/immediate/deferred, form, amount, due date/days, cash/transfer, from `PlatnosciDokumentow`), `IsPaid`, `IsOverdue`;
  `DocumentListItemDto.IsPaid/IsOverdue/PaidAmount` now use the settlement's `KwotaPozostala` when present
- `GET /api/payments/receivables/by-document/{documentId}` - settlements linked to a document; `ReceivableDto` gains
  `IsOverdue`, `IsSettled`, `UnsettledAmount`, `VatAmount`, `DocumentDate`, `LastSettlementDate`, `Title`, `Subtype`, `IsCollectible`, `SplitPayment`
- **KSeF inbound (receiving purchase e-invoices)** in `KsefController`:
  - `POST /api/ksef/receive` (`{dateFrom?, dateTo?}`; empty body = incremental pull of everything new) and `POST /api/ksef/receive/{ksefNumber}` -
    pull e-invoices from KSeF into the Nexo buffer via `IKoordynatorOdbioruEFaktur.Pobierz(...)`
  - `GET /api/ksef/inbox` (`processingStatus`, `pendingOnly`, `dateFrom/To`, `sellerTaxId`, paging) - received e-invoices with processing status
  - `POST /api/ksef/inbox/{id}/import` (`{warehouseSymbol?}`) - import one received e-invoice as a purchase invoice (FZ) or purchase
    correction (KFZ) via `ObslugaImportuEFaktur.WypelnijNaPodstawieDokumentuElektronicznego`; `POST /api/ksef/inbox/import-pending`
    (`maxCount`, `warehouseSymbol`) - import every e-invoice waiting "do przetworzenia w Subiekcie" (for scheduled automation together with `receive`)
  - `ElectronicDocumentDto` gains `Direction`, `ProcessingStatus(+Code)`, `InvoiceKind`, `IsCorrection`, `MyCompanyRole`, `CustomerId`,
    `CustomerMatchStatus`, `WarehouseId/Symbol`, `CurrencySymbol`, `ManuallyLinkedDocumentNumbers`
- `POST /api/warehouse-documents/rw/from-order` - RW realizing a customer order (whole order or selected `LineIds`, consolidation method,
  warehouse override) via `IRozchodWewnetrzny.WypelnijNaPodstawieZK` so the ZK realization state is updated
- `DynamicPropertyHelper.FindProductUnit / GetLineUnitSymbol / GetProductBaseUnitSymbol` helpers (unit lookup by symbol, name or alias)
- SferaService manager names: `KoordynatorOdbioruEFaktur`, `MenedzerImportuEFaktur`, `ZleceniaProdukcyjneMontowania`,
  `ZleceniaProdukcyjneRozkompletowania`, `OperacjeRozrachunkowe`
- `docs/SDK-ENTITY-REFERENCE.md` - verified entity/enum reference for SDK 61.1 (what exists, what does not, numeric enum values)
- **Product units of measure & kit composition** (`ProductsController`): `GET/POST /api/products/{id}/units`, `DELETE .../units/{unitSymbol}`,
  `PUT .../units/base`, `PUT .../units/defaults` (SDK `IAsortyment.JednostkiMiary.DodajJednostkeMiary/UsunJednostkeMiary/UstawPodstawowaJednostkeMiary`,
  conversion factors `newUnitCount`/`baseUnitCount`, optional set-as-sale/purchase unit); `GET/POST /api/products/{id}/components`,
  `DELETE .../components/{componentProductId}` (SDK `IAsortyment.Skladniki`); `ProductDto.IsKit` (SDK `RodzajeAsortymentowExtensions.CzyKomplet`)
  and `ProductDto.Components` (`ProductComponentDto`); requests in `ProductUnitRequest.cs`
- **Production orders (ZPM/ZPR) reworked** (`AssemblyController`): English aliases `POST /api/assembly/assemble` / `disassemble`
  (Polish `montaz`/`demontaz` kept), shared creation path (`Utworz(konfiguracja)`, `Montuj`/`Rozkompletuj` called directly, kit qty via
  `PozycjaKomplet.Ilosc` + `Przelicz()`, components warehouse `Dane.MagazynSkladnikow`, optional unit on kit and component lines, `IssueDate`,
  `ReserveNumber`); new `GET /api/assembly/max-quantity` (transient ZPM + `PodajMaksymalnaIloscKompletu()` + per-component available stock),
  `GET /api/assembly/{id}/shortages` (`Braki.Lista` with stock-comparison fallback), `POST /api/assembly/from-order-line` (`WypelnijnaPodstawieZK`),
  `DELETE /api/assembly/{id}`; list filters fixed (`dateFrom/To` used non-existent `DataDokumentu`, `warehouseSymbol` was ignored), `type` accepts
  `assemble|disassemble|all`; DTO exposes units, `QuantityInBaseUnit`, costs, auto PW/RW document refs, component cost share
- **E-commerce shipping orders** (read-only): `GET /api/ecommerce/shipping-orders` (filters `integrationId`, `status`, `externalId`, paging) and
  `GET /api/ecommerce/shipping-orders/{id}` with lines incl. `CustomsTariffCode` (SDK 61.1 `PozycjaZamowieniaWysylkowego.KodTaryfyCelnej`)
- **Photo gallery**: `PhotoDto.IsAiGenerated` (SDK 61.1 `IZdjecie.WygenerowanePrzezAI`), `FileType`, `SortOrder`; `PUT /api/photo-gallery/{id}/ai-generated`
  (`IGaleriaZdjec.UstawWygenerowanePrzezAI`, 501 on older SDK)
- SferaService manager name `ZamowieniaWysylkowe`

### Fixed (2026-09-04)
- **`NexoSdkSynchronizer` picked a stale deployment**: candidates were ranked by DLL size/write time, so on a machine with several nexo
  deployments it selected a 61.0.0.9362 `Binaries` folder while the database was already 61.1.0.9431 (`Podana baza danych jest w innej wersji
  niż użyte biblioteki sferyczne`). Candidates are now ranked by the file version of `InsERT.Moria.Sfera.dll`, then DLL count, then write time
- `PhotoGalleryController` GET endpoints enumerated the gallery manager via `Dane.Wszystkie()` which `IGaleriaZdjec` does not have (always empty) -
  now `WszystkieZdjecia()`; photo fields mapped from the real `IZdjecie` members (`Nazwa`, `RozmiarBajty`, `CzyGlowneZdjecie`) with legacy fallbacks
- `AssemblyController`: `PozycjeSkladniki.Dodaj(asortyment)` (non-existent overload), `PozycjaKomplet.Asortyment` writes, forced
  `ZlecenieProdukcyjne_DoRealizacji` status and the reflection-based `Rozkompletuj` call removed; manager resolved via `GetManager` with
  `PodajObiektTypu` fallback (calling the extension method on a `dynamic` handle never bound)
- `CreateDocumentItemRequest.Unit` default `"szt."` removed (was ignored; now null = product default)
- **17 `GetManager` keys pointed at extension methods that do not exist in the SDK** (verified by scanning every SDK assembly), so
  Archives, AuditTrail, CustomFields, AccountingImport, Contracts, Payroll, Devices, VatRegistry, exchange rates and the employee lookup in
  Documents/WarehouseDocuments always failed with 500. Remapped via new `GetFirstExtensionManager(...)` fallback chains
  (`Archiwa`, `ZdarzeniaSladuRewizyjnego`, `KonfiguracjePolWlasnych`, `SchematyImportu`, `UmowyPracowniczeGr/MGr`, `ListyPlacGr/MGr`,
  `RachunkiDoUmowPracowniczychGr/MGr`, `UrzadzeniaZewnetrzne`, `ZapisyWEwidencjiVAT`, `TabeleKursowWalut`, `Podmioty`, ...). `Absencje`,
  `Kurierzy`, `GaleriaZdjec`, `CentraKosztow` have no SDK accessor at all and still resolve to null - documented in `docs/AUDIT-2026-09-04.md`
- `docs/AUDIT-2026-09-04.md` - root-cause audit, SDK coverage (83 of 587 accessors used), full list of unused managers by domain, open items


### Added (2026-09-03)
- **`ProductDto.Units`** (`ProductUnitDto[]`): all units of measure of a product from `Asortyment.JednostkiMiar`
  with `IsBase/IsSale/IsPurchase/IsWarehouse`, precision, package barcode, weight/volume and the conversion to the
  base unit (`ToBaseFactor`, `BaseUnitSymbol`, raw `Conversions` from `PrzelicznikJednostekMiarAsortymentu`).
  Needed by WolfFireApp to interpret stock levels kept in the base unit (embroidery thread: "szt" = 5000 "m").

### Added (2026-06-26)
- **SDK 61.0.0 fields exposed in DTOs** (all dynamic, null-safe, backward-compatible with older SDKs):
  - `AddressDto.GLN` (Global Location Number, `Adres.GLN`) - mapped in `CustomersController` from the address entity with fallback to `Szczegoly`
  - `ElectronicDocumentDto`: `IsCostInvoice` (`DokumentElektroniczny.Kosztowa`), `PaymentStatus` (`StanOplacenia`), `PaymentDueDate` (`TerminPlatnosci`), `IsSynchronized` (`Zsynchronizowany`) - mapped in `KsefController.MapElectronicDocument`
  - `ProductDto.ExternalStocks` + `ExternalWarehouseStockDto` (e-commerce external warehouse stock, `Asortyment.StanyMagazynoweZewnetrzne`) - mapped in the product detail mapper only (list view unaffected). The `StanMagazynowyZewnetrzny` field names (`Ilosc`, `MagazynZewnetrzny.Nazwa`/`IdZewnetrzny`) are best-effort and need confirmation against a live 61.0.0 instance via `debug/item-properties`
- **`scripts/sync-sdk-version.ps1`** - single-source-of-truth SDK version bumper: derives the version from the newest `docs/nexoSDK_<ver>` folder and rewrites it across `README.md` (badge + path), `scripts/setup-sdk.ps1`, and both CI workflows. Supports `-DryRun`, `-Version`, `-Tag` (creates the `sdk-<ver>` bookmark tag)

### Changed (2026-06-26)
- **Upgraded to InsERT Nexo SDK 61.0.0.9362** (from 60.1.1.9292). Verified all 64 required DLLs are present; the API/model changes (186 added / 11 removed / 80 changed API types; 12/1/70 model types) are contained within `InsERT.Moria.API.dll` and `InsERT.Moria.ModelDanych.dll` - no new standalone assemblies, so the required-DLL list and `.csproj` references are unchanged
  - Reviewed every removed / signature-changed / `[Obsolete]` SDK member against the codebase: zero usages - no breaking changes for our endpoints
  - Bumped version paths in `scripts/setup-sdk.ps1`, `.github/workflows/build.yml` + `release.yml` (replaced the stale `nexoSDK_58.0.2.8985` local fallback), and the README SDK badge
- Tagged the aligned commit as `sdk-61.0.0.9362` (bookmark tag for diffing across SDK versions; does not trigger the release pipeline)

### Added (2026-06-12)
- **True multi-company support** - new `SferaConnectionPool` (one SDK connection with its own STA thread per Nexo database) and `SferaServiceRouter` (routes every `ISferaService` call to the tenant's connection based on API key claims). Per-key `Database`, `NexoLogin`/`NexoPassword`, `DefaultWarehouse`, `DefaultBranch` are now enforced on EVERY request across all 61 controllers - previously operator switching worked only in 2 controllers and `Database` was ignored. Keys without an operator override are forced back to the default operator (no tenant leakage). Requires verification on Windows: multiple `Uchwyt` connections in one process.
  - Behavior changes: reconnect endpoints (`/api/health/reconnect`, `/api/settings/connection/reconnect`) now reconnect only the calling key's database connection; failed per-key operator login now surfaces as HTTP 500 with a clear message (previously a 4xx-ish error shaped per endpoint); the duplicated operator-switching helpers were removed from DocumentsController and WarehouseDocumentsController (the router owns switching now)
- **SDK DLLs removed from git entirely** - legacy root `lib/nexo-sdk/` deleted; `src/lib/nexo-sdk` stays the (untracked) build location, filled by `scripts/setup-sdk.ps1` locally or the `NEXO_SDK_URL` download step in CI (`.github/workflows/build.yml`)
- **Client-machine-first SDK binaries** - `Sfera:PreferDeploymentBinaries` (default `true`): when the client's nexo deployment (`%LocalAppData%\InsERT\Deployments\Nexo\*\Binaries`) differs from the build-shipped DLLs, the deployment's full consistent set now OVERWRITES the runtime DLLs (previously only missing DLLs were filled, leaving a version mix); `Sfera:SdkFallbackUrl`/`SdkFallbackToken` - when no nexo exists on the machine and the build shipped no DLLs, the SDK zip is downloaded at startup (same package as CI). `setup-sdk.ps1` also probes deployment Binaries now

### Security (2026-06-12)
- **MCP endpoint now requires authorization** - `/mcp` exposed write tools (invoices, receipts, customers) without any auth; now protected by the same API key scheme as REST
- **Secrets untracked from git** - `src/appsettings.json` (API key + Nexo credentials) removed from tracking; rotate the exposed keys
- **Debug endpoints gated to Development** - `debug/*` endpoints and `DiagnosticsController` return 404 outside the Development environment

### Fixed (2026-06-12)
- **Thread safety completed across all 61 controllers** - wrapped the last 22 unlocked endpoints (DocumentsController ×10 incl. `GET /api/documents`, corrections, advance/VAT-margin invoices, associations; CustomersController debug ×8; DiagnosticsController ×4) in `ExecuteWithLockAsync`
- **Reconnect actually reconnects** - new `ReinitializeAsync()` disposes the stale SDK handle and reconnects on the STA thread; previously `POST /api/health/reconnect` was a no-op and `POST /api/settings/connection/reconnect` disposed the singleton service permanently
- **Operator restore after failed switch** - previous operator is restored with its own tracked password (was: always the default password), with fallback to the default operator
- **Async `ExecuteWithLockAsync` deadlock risk** - now pumps WinForms messages while waiting so continuations posted to the STA synchronization context can run
- **Failed login no longer leaves a half-initialized connection** - `IsConnected` reflects a fully usable connection
- **Manager-unavailable no longer masked as 404** - `GET /api/documents/{id}` and `by-number` return 500 when the SDK manager cannot be obtained

### Removed (2026-06-12)
- Stale work logs (`FIXES_SUMMARY.md`, `IMPLEMENTATION_SUMMARY.md`, `RECEIPT_FIX_SUMMARY.md`, `SERVICE_VALIDATION_FIX.md`, `API-COMPLETION-PLAN.md`, `sdk-structure.md`), ghost dirs (`src/src/`, empty `src/scripts/`)
- Untracked from git: `src/lib/nexo-sdk/` DLL mirror (canonical copy stays in `lib/nexo-sdk/`), `.idea/`, auto-generated `CLAUDE.md` stubs

### Docs (2026-06-12)
- README rewritten: full module map (61 controllers), API conventions (response envelope, pagination, STA concurrency), Laravel integration example, honest multi-tenant status; audit report at `docs/AUDIT-2026-06-12.md` with SDK 60.1.1 coverage roadmap

### Added
- **Service-type product detection in stock validation** - Added `IsService()` method to `StockValidationHelper` that identifies service products by checking `Rodzaj_Id = 1`
- **Automatic stock validation skip for services** - Services no longer trigger "insufficient stock" errors since they don't require physical inventory
- **StatusSymbol in document DTOs** - Added `StatusSymbol` field to all document mapping methods for better status tracking
- **Enhanced error diagnostics for receipts** - Improved `GetBusinessObjectErrors()` to properly extract validation errors from SDK's `InvalidData` collection
- **Comprehensive receipt item logging** - Added detailed logging to `AddReceiptItemsById()` to track item addition and diagnose issues

### Changed
- **Stock validation logic** - Modified `ValidateStock()` in `StockValidationHelper` to skip stock checks for service-type items (Rodzaj_Id = 1)
- Stock validation now logs debug messages when skipping services: `"Skipping stock validation for service: {Symbol} (Rodzaj_Id=1)"`
- **IsService() method improvements** - Now prioritizes reliable `Rodzaj_Id` FK check over navigation property access, with multiple fallback mechanisms
- **Receipt creation simplified** - Removed problematic status manipulation that was causing validation failures; now follows minimal SDK pattern

### Fixed
- **Stock validation errors for services** - Services no longer fail with "insufficient stock" errors when creating sales documents (invoices, receipts)
- **Document status in API responses** - Status information now properly included in all document DTOs (StatusId, Status, StatusSymbol)
- **Receipt validation failures** - Fixed MoznaZapisac=false issue by removing direct StatusDokumentuId manipulation that put receipts in invalid state
- **IsService() null reference exceptions** - Fixed exceptions when Rodzaj navigation property is null; now handles null values gracefully
- **Receipt creation for historical dates** - Simplified historical document handling to use minimal flags instead of status manipulation

### Verified
- **Import remarks handling** - Confirmed that invoices and receipts skip "Import ILUO" type notes via `ShouldSkipImportRemarks()` and `ShouldSkipImportRemarksForContext()` methods
- **Multi-tenant authentication** - Verified that per-API-key Nexo credentials (NexoLogin/NexoPassword) work correctly via claims-based authentication

## Technical Details

### Stock Validation for Services
Previously, service items (like "DMSUSGI00081") would fail stock validation with errors like:
```
Sales invoice creation failed - insufficient stock: Insufficient stock for 'DMSUSGI00081': 
requested 1,0, available 0 (shortage: 1,0)
```

Now, the system detects services by checking:
1. `Rodzaj_Id = 1` (primary check - Service type in Nexo)
2. `Rodzaj.Symbol = "Usluga"` or `"U"` (fallback check)

Services are marked as available without checking stock levels:
- `HasSufficientStock = true`
- `AvailableQuantity = 0` (services don't track inventory)

### Document Status Enhancement
All document mapping methods now include:
- `StatusId` - Numeric status identifier
- `Status` - Status name (e.g., "W realizacji", "Zrealizowano")
- `StatusSymbol` - Status symbol for programmatic checks

This applies to:
- `MapSalesDocumentToDto()` - Sales invoices, receipts
- `MapPurchaseDocumentToDto()` - Purchase invoices
- `MapOrderToDto()` - Customer orders

### Authentication Architecture
The API supports multi-tenant scenarios where different API keys can use different Nexo operators:

1. **API Key Configuration** (`appsettings.json`):
   ```json
   {
     "ApiKeys": {
       "Keys": [
         {
           "Key": "api-key-1",
           "Name": "Company A",
           "NexoLogin": "operator1",
           "NexoPassword": "password1"
         },
         {
           "Key": "api-key-2",
           "Name": "Company B",
           "NexoLogin": "operator2",
           "NexoPassword": "password2"
         }
       ]
     }
   }
   ```

2. **Authentication Flow**:
   - API key is validated by `ApiKeyAuthenticationHandler`
   - NexoLogin/NexoPassword are added as claims if configured
   - Controllers call `GetOperatorCredentialsFromClaims()` to retrieve credentials
   - `SwitchToRequestOperator()` switches Nexo operator context before document operations

3. **Usage**:
   ```bash
   curl -X POST http://localhost:5000/api/documents/sales-invoice \
     -H "Authorization: Bearer api-key-1" \
     -H "Content-Type: application/json" \
     -d '{ ... }'
   ```

This allows the same API instance to serve multiple companies/operators with proper data isolation.

---

## Previous Releases

See git history for changes prior to this changelog.
