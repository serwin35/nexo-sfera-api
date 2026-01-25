# Nexo Sfera API - Plan Kompletnego REST API

## Wzorzec Async dla Entity Framework 6

> **WAZNE:** Wszystkie operacje tworzenia dokumentow uzywaja wzorca async z `ExecuteWithLockAsync`:

```csharp
public async Task<ActionResult<ApiResponse<T>>> CreateDocument([FromBody] CreateRequest request)
{
    var result = await _sferaService.ExecuteWithLockAsync<(bool Success, T? Data, string Message, List<string> Errors)>(() =>
    {
        // 1. Ustaw magazyn na Dokument.Magazyn (nie Dane.Magazyn!)
        dokument.Dokument.Magazyn = magazyn;

        // 2. Zarezerwuj numer PRZED dodaniem pozycji
        dokument.ZarezerwujNumer();

        // 3. Dodaj pozycje przez ID produktu
        foreach (var item in request.Items)
        {
            var pozycja = dokument.Pozycje.Dodaj(towarId);
            pozycja.Ilosc = item.Quantity;
        }

        // 4. Zapisz dokument
        dokument.Zapisz();
    });
}
```

Ten wzorzec zapewnia poprawne dzialanie Entity Framework 6 na .NET 8 dzieki:
- `WindowsFormsSynchronizationContext` - wymagany dla EF6
- STA Thread - operacje SDK wykonywane w watku STA
- Thread-safety przez `ExecuteWithLockAsync`

---

## Status Obecny

### Istniejące Controllery (18)

| Controller | Endpoint Base | Funkcjonalność | Status |
|------------|--------------|----------------|--------|
| AssemblyController | `/api/assembly` | Montaż/demontaż | ✅ OK |
| CustomersController | `/api/customers` | Kontrahenci CRUD | ✅ OK |
| DiagnosticsController | `/api/diagnostics` | SDK discovery | ✅ OK |
| DictionaryController | `/api/dictionary` | Słowniki (VAT, waluty, jednostki) | ✅ OK |
| DiscountsController | `/api/discounts` | Rabaty + atrybuty + grupy | ✅ OK |
| DocumentsController | `/api/documents` | Faktury, korekty, paragony, zamowienia | ✅ OK + Async |
| EmployeesController | `/api/employees` | Pracownicy (basic) | ⚠️ Basic |
| FinanceReportsController | `/api/finance` | Raporty kasowe/bankowe | ✅ OK |
| HealthController | `/api/health` | Health checks | ✅ OK |
| InventoryController | `/api/inventory` | Stany magazynowe | ✅ OK |
| KsefController | `/api/ksef` | E-faktury KSeF | ✅ OK |
| OffersController | `/api/offers` | Oferty (Gestor) - tworzenie, akceptacja, zamykanie | ✅ OK + Async |
| OrdersController | `/api/orders` | Zamówienia do dostawców | ⚠️ Partial |
| PaymentsController | `/api/payments` | Operacje kasowe/bankowe (KP, KW, BP, BW) | ✅ OK + Async |
| ProductsController | `/api/products` | Asortyment CRUD | ✅ OK |
| SystemController | `/api/system` | Info o firmie/operatorze | ✅ OK |
| WarehouseDocumentsController | `/api/warehouse-documents` | WZ/PZ/RW/PW/MM | ✅ OK + Async |
| WarehousesController | `/api/warehouses` | Magazyny | ✅ OK |

---

## Analiza SDK - Extension Methods na Uchwyt

| Extension Method | Manager | Pokrycie w API | Brakuje |
|-----------------|---------|----------------|---------|
| `Asortymenty()` | Products | ✅ ProductsController | - |
| `SzablonyAsortymentu()` | Product Templates | ❌ | **Nowy controller** |
| `Podmioty()` | Contractors | ✅ CustomersController | - |
| `Dokumenty()` | Documents | ✅ DocumentsController | - |
| `DokumentySprzedazy()` | Sales Docs | ✅ DocumentsController | - |
| `DokumentyZakupu()` | Purchase Docs | ✅ DocumentsController | - |
| `DokumentyElektroniczne()` | E-Documents | ✅ KsefController | - |
| `KorektyDokumentowSprzedazy()` | Sales Corrections | ✅ DocumentsController | - |
| `KorektyDokumentowZakupu()` | Purchase Corrections | ✅ DocumentsController | - |
| `Magazyny()` | Warehouses | ✅ WarehousesController | - |
| `WydaniaZewnetrzne()` | WZ | ✅ WarehouseDocumentsController | - |
| `PrzyjeciaZewnetrzne()` | PZ | ✅ WarehouseDocumentsController | - |
| `WydaniaMiedzymagazynowe()` | MM | ✅ WarehouseDocumentsController | - |
| `RozchodyWewnetrzne()` | RW | ✅ WarehouseDocumentsController | - |
| `ZamowieniaOdKlientow()` | Customer Orders | ⚠️ Partial | **Dedicated endpoints** |
| `ZamowieniaDoDostawcow()` | Supplier Orders | ✅ OrdersController | - |
| `Oferty()` | Offers | ✅ OffersController | - |

---

## Dodatkowe Managery w SferaService (do wykorzystania)

| Manager | Namespace | Pokrycie | Brakuje |
|---------|-----------|----------|---------|
| `GrupyAsortymentu()` | Asortymenty | ⚠️ Partial | **Dedicated endpoints** |
| `CechyAsortymentu()` | Asortymenty | ⚠️ Partial | **Dedicated endpoints** |
| `GrupyKontrahentow()` | Klienci | ⚠️ Partial | **Dedicated endpoints** |
| `StawkiVat()` | Slowniki | ✅ DictionaryController | - |
| `JednostkiMiar()` | Slowniki | ✅ DictionaryController | - |
| `Waluty()` | Slowniki | ✅ DictionaryController | - |
| `KursyWalut()` | Slowniki | ✅ DictionaryController | - |
| `FormyPlatnosci()` | Slowniki | ✅ DictionaryController | - |
| `Cenniki()` | Cenniki | ✅ DictionaryController | - |
| `PoziomyCen()` | Cenniki | ✅ DictionaryController | - |
| `Rabaty()` | Rabaty | ✅ DiscountsController | - |
| `OperacjeKasowe()` | Finanse | ✅ PaymentsController | - |
| `OperacjeBankowe()` | Finanse | ✅ PaymentsController | - |
| `Rozrachunki()` | Finanse | ✅ PaymentsController | - |
| `StanowiskaKasowe()` | Finanse | ✅ FinanceReportsController | - |
| `RachunkiBankowe()` | Finanse | ✅ FinanceReportsController | - |
| `RaportyKasowe()` | Finanse | ✅ FinanceReportsController | - |
| `WyciagiBankowe()` | Finanse | ✅ FinanceReportsController | - |
| `RodzajeOperacjiKasowych()` | Finanse | ❌ | **Nowy endpoint** |
| `RodzajeOperacjiBankowych()` | Finanse | ❌ | **Nowy endpoint** |

---

## SDK Interfaces via PodajObiektTypu<T>

| Interface | Namespace | Opis | Status |
|-----------|-----------|------|--------|
| `IMagazynier` | EgzekutorMagazynowy | Advanced inventory operations | ⚠️ Partial |
| `IKonfiguracje` | Dokumenty.Logistyka | Document configurations | ❌ Missing |
| `IDeklaracje` | Deklaracje | Tax declarations (VAT) | ❌ Missing |
| `IDeklaracjeZUS` | DeklaracjeZUS | ZUS declarations | ❌ Missing |
| `IDeklaracjeIntrastat` | Intrastat | Intrastat declarations | ❌ Missing |
| `IEwidencjaCzasuPracy` | Kadry.Duze | Time tracking | ❌ Missing |
| `ISlownikiWlasne` | PolaWlasne2 | Custom dictionaries | ❌ Missing |
| `IKonfiguracjePolWlasnych` | PolaWlasne | Custom fields config | ❌ Missing |

---

# PLAN IMPLEMENTACJI

## Faza 1: Uzupełnienie Core Managers (Priorytet: WYSOKI)

### 1.1 CustomerOrdersController (Zamówienia od klientów)
**Endpoint:** `/api/customer-orders`

```
GET    /api/customer-orders              - Lista zamówień od klientów
GET    /api/customer-orders/{id}         - Szczegóły zamówienia
GET    /api/customer-orders/by-number/{number}
GET    /api/customer-orders/for-customer/{customerId}
GET    /api/customer-orders/pending      - Zamówienia do realizacji
GET    /api/customer-orders/overdue      - Przeterminowane
POST   /api/customer-orders              - Utwórz zamówienie
PUT    /api/customer-orders/{id}         - Aktualizuj
DELETE /api/customer-orders/{id}         - Anuluj
POST   /api/customer-orders/{id}/realize - Realizuj (utwórz fakturę/WZ)
GET    /api/customer-orders/{id}/items   - Pozycje zamówienia
POST   /api/customer-orders/{id}/items   - Dodaj pozycję
```

### 1.2 ProductTemplatesController (Szablony asortymentu)
**Endpoint:** `/api/product-templates`

```
GET    /api/product-templates            - Lista szablonów
GET    /api/product-templates/{id}       - Szczegóły szablonu
GET    /api/product-templates/by-symbol/{symbol}
POST   /api/product-templates            - Utwórz szablon
PUT    /api/product-templates/{id}       - Aktualizuj
DELETE /api/product-templates/{id}       - Usuń
POST   /api/product-templates/{id}/create-product - Utwórz produkt z szablonu
```

### 1.3 ProductGroupsController (Grupy asortymentu)
**Endpoint:** `/api/product-groups`

```
GET    /api/product-groups               - Lista grup (tree)
GET    /api/product-groups/{id}          - Szczegóły grupy
GET    /api/product-groups/{id}/products - Produkty w grupie
POST   /api/product-groups               - Utwórz grupę
PUT    /api/product-groups/{id}          - Aktualizuj
DELETE /api/product-groups/{id}          - Usuń
POST   /api/product-groups/{id}/move     - Przenieś w hierarchii
```

### 1.4 ProductAttributesController (Cechy asortymentu)
**Endpoint:** `/api/product-attributes`

```
GET    /api/product-attributes           - Lista cech
GET    /api/product-attributes/{id}      - Szczegóły cechy
GET    /api/product-attributes/{id}/values - Wartości cechy
POST   /api/product-attributes           - Utwórz cechę
PUT    /api/product-attributes/{id}      - Aktualizuj
DELETE /api/product-attributes/{id}      - Usuń
```

### 1.5 ContractorGroupsController (Grupy kontrahentów)
**Endpoint:** `/api/contractor-groups`

```
GET    /api/contractor-groups            - Lista grup
GET    /api/contractor-groups/{id}       - Szczegóły grupy
GET    /api/contractor-groups/{id}/contractors - Kontrahenci w grupie
POST   /api/contractor-groups            - Utwórz grupę
PUT    /api/contractor-groups/{id}       - Aktualizuj
DELETE /api/contractor-groups/{id}       - Usuń
POST   /api/contractor-groups/{id}/add-contractor/{contractorId}
DELETE /api/contractor-groups/{id}/remove-contractor/{contractorId}
```

---

## Faza 2: Rozszerzenie Dictionary/Finance (Priorytet: ŚREDNI) ✅ ZAIMPLEMENTOWANE

### 2.1 Rozszerzenie DictionaryController ✅

Dodane do `/api/dictionary`:

```
GET    /api/dictionary/cash-operation-types     ✅ Rodzaje operacji kasowych
GET    /api/dictionary/bank-operation-types     ✅ Rodzaje operacji bankowych
GET    /api/dictionary/document-types           ✅ Typy dokumentów (statyczne)
GET    /api/dictionary/document-statuses        ✅ Statusy dokumentów (statyczne)
GET    /api/dictionary/countries                ✅ Kraje (z filtrowaniem EU)
GET    /api/dictionary/countries/{isoCode}      ✅ Kraj po kodzie ISO
GET    /api/dictionary/regions                  ❌ Brak w SDK (województwa)
GET    /api/dictionary/delivery-methods         ❌ Brak standardowego managera
GET    /api/dictionary/shipping-carriers        ❌ Brak standardowego managera
```

### 2.2 Rozszerzenie FinanceReportsController ✅

Dodane do `/api/finance`:

```
GET    /api/finance/settlements                 ✅ Rozliczenia (lista z filtrami)
GET    /api/finance/settlements/{id}            ✅ Szczegóły rozliczenia
GET    /api/finance/aging-report                ✅ Raport wiekowania należności
GET    /api/finance/aging-report/payables       ✅ Raport wiekowania zobowiązań
GET    /api/finance/cash-flow                   ❌ Wymaga agregacji (przyszła faza)
GET    /api/finance/settlements/{id}/payments   ❌ Wymaga analizy Rozliczenia (przyszła faza)
POST   /api/finance/settlements/match           ❌ Wymaga logiki dopasowywania (przyszła faza)
```

### 2.3 Rozszerzenie InventoryController ✅

Dodane do `/api/inventory`:

```
GET    /api/inventory/movements                 ✅ Historia ruchów magazynowych
GET    /api/inventory/movements/product/{id}    ✅ Ruchy dla produktu
GET    /api/inventory/valuation                 ✅ Wycena magazynu
GET    /api/inventory/stocktaking               ✅ Lista dokumentów inwentaryzacyjnych
GET    /api/inventory/stocktaking/{id}          ✅ Szczegóły inwentaryzacji
GET    /api/inventory/stocktaking/{id}/items    ✅ Pozycje inwentaryzacji
POST   /api/inventory/stocktaking               ❌ Tworzenie (przyszła faza)
POST   /api/inventory/stocktaking/{id}/items    ❌ Dodawanie pozycji (przyszła faza)
POST   /api/inventory/stocktaking/{id}/close    ❌ Zamykanie (przyszła faza)
```

---

## Faza 3: Podatki i Deklaracje (Priorytet: ŚREDNI) ✅ ZAIMPLEMENTOWANE

### 3.1 DeclarationsController (Deklaracje VAT) ✅
**Endpoint:** `/api/declarations`

```
GET    /api/declarations/vat                    ✅ Lista deklaracji VAT
GET    /api/declarations/vat/{id}               ✅ Szczegóły deklaracji
GET    /api/declarations/vat/summary            ✅ Podsumowanie VAT za rok
GET    /api/declarations/types                  ✅ Dostępne typy deklaracji
POST   /api/declarations/vat/generate           ❌ Generowanie (przyszła faza)
GET    /api/declarations/vat/{id}/preview       ❌ Podgląd (przyszła faza)
POST   /api/declarations/vat/{id}/send          ❌ Wysyłka do US (przyszła faza)
GET    /api/declarations/vat/{id}/upo           ❌ Pobierz UPO (przyszła faza)
```

### 3.2 JPKController (Jednolity Plik Kontrolny) ✅
**Endpoint:** `/api/jpk`

```
GET    /api/jpk                                 ✅ Lista wygenerowanych JPK
GET    /api/jpk/{id}                            ✅ Szczegóły
GET    /api/jpk/{id}/status                     ✅ Status wysyłki
GET    /api/jpk/types                           ✅ Lista typów JPK
GET    /api/jpk/types/{typeCode}/versions       ✅ Wersje definicji JPK
GET    /api/jpk/packages                        ✅ Lista paczek JPK
GET    /api/jpk/packages/{id}                   ✅ Szczegóły paczki
POST   /api/jpk/vat/generate                    ❌ Generuj JPK_VAT (przyszła faza)
POST   /api/jpk/fa/generate                     ❌ Generuj JPK_FA (przyszła faza)
POST   /api/jpk/mag/generate                    ❌ Generuj JPK_MAG (przyszła faza)
GET    /api/jpk/{id}/download                   ❌ Pobierz plik XML (przyszła faza)
POST   /api/jpk/{id}/send                       ❌ Wyślij do MF (przyszła faza)
```

### 3.3 IntrastatController (Deklaracje Intrastat) ✅
**Endpoint:** `/api/intrastat`

```
GET    /api/intrastat                           ✅ Lista deklaracji
GET    /api/intrastat/{id}                      ✅ Szczegóły
GET    /api/intrastat/{id}/items                ✅ Pozycje deklaracji
GET    /api/intrastat/summary                   ✅ Podsumowanie za okres
POST   /api/intrastat/generate                  ❌ Generuj deklarację (przyszła faza)
POST   /api/intrastat/{id}/send                 ❌ Wyślij (przyszła faza)
```

---

## Faza 4: Zaawansowane funkcje (Priorytet: NISKI) ✅ ZAIMPLEMENTOWANE

### 4.1 ConfigurationsController (Konfiguracje) ✅
**Endpoint:** `/api/configurations`

```
GET    /api/configurations/categories           ✅ Lista kategorii konfiguracji
GET    /api/configurations/documents            ✅ Konfiguracje dokumentów
GET    /api/configurations/documents/{type}     ✅ Dla typu dokumentu
GET    /api/configurations/numbering            ✅ Konfiguracje numeracji
GET    /api/configurations/numbering/placeholders ✅ Dostępne placeholdery
GET    /api/configurations/vat                  ✅ Konfiguracja VAT
GET    /api/configurations/currencies           ✅ Konfiguracja walut
GET    /api/configurations/payments             ✅ Konfiguracja płatności
PUT    /api/configurations/documents/{type}     ❌ Modyfikacja (przyszła faza)
```

### 4.2 CustomFieldsController (Pola własne) ❌
**Endpoint:** `/api/custom-fields`

```
❌ Wymaga innego podejścia - PolaWlasne2 działa jako accessor per-encja, nie jako manager
```

### 4.3 PrintController (Wydruki) ✅
**Endpoint:** `/api/print`

```
GET    /api/print/headers                       ✅ Nagłówki wydruków
GET    /api/print/headers/{id}                  ✅ Szczegóły nagłówka
GET    /api/print/footers                       ✅ Stopki wydruków
GET    /api/print/footers/{id}                  ✅ Szczegóły stopki
GET    /api/print/parameters                    ✅ Parametry wydruku
GET    /api/print/logs                          ✅ Historia wydruków
GET    /api/print/labels                        ✅ Szablony etykiet
GET    /api/print/labels/{id}                   ✅ Szczegóły szablonu etykiet
GET    /api/print/template-types                ✅ Typy szablonów (statyczne)
POST   /api/print/{id}/generate                 ❌ Generowanie (przyszła faza)
```

### 4.4 ReportsController (Raporty) ✅
**Endpoint:** `/api/reports`

```
GET    /api/reports/types                       ✅ Lista typów raportów
GET    /api/reports/sales/summary               ✅ Podsumowanie sprzedaży
GET    /api/reports/sales/by-product            ✅ Bestsellery produktów
GET    /api/reports/sales/by-customer           ✅ Ranking klientów
GET    /api/reports/purchases/summary           ✅ Podsumowanie zakupów
GET    /api/reports/inventory/turnover          ✅ Rotacja magazynowa
POST   /api/reports/{id}/execute                ❌ Wykonaj raport (przyszła faza)
```

### 4.5 EcommerceController (Handel elektroniczny) ✅
**Endpoint:** `/api/ecommerce`

```
GET    /api/ecommerce/integrations              ✅ Lista integracji
GET    /api/ecommerce/integrations/{id}         ✅ Szczegóły integracji
GET    /api/ecommerce/offers                    ✅ Oferty internetowe
GET    /api/ecommerce/offers/{id}               ✅ Szczegóły oferty
GET    /api/ecommerce/offer-groups              ✅ Grupy ofert
GET    /api/ecommerce/shipping-lists            ✅ Listy wysyłkowe
GET    /api/ecommerce/packages                  ✅ Paczki wysyłkowe
GET    /api/ecommerce/packages/{id}             ✅ Szczegóły paczki
GET    /api/ecommerce/packages/track/{tracking} ✅ Śledzenie po numerze
GET    /api/ecommerce/package-dimensions        ✅ Gabaryty paczek
GET    /api/ecommerce/platforms                 ✅ Obsługiwane platformy (statyczne)
POST   /api/ecommerce/orders/{id}/import        ❌ Import zamówień (przyszła faza)
POST   /api/ecommerce/products/sync             ❌ Synchronizacja (przyszła faza)
```

### 4.6 AutomationController (Automatyzacje) ❌
**Endpoint:** `/api/automation`

```
❌ Brak standardowego managera w SDK - wymaga specyficznej integracji
```

---

## Faza 5: HR/Kadry (opcjonalnie - zależy od produktu)

### 5.1 HRController (Kadry)
**Endpoint:** `/api/hr`

```
GET    /api/hr/employees                        - Lista pracowników (rozszerzona)
GET    /api/hr/employees/{id}                   - Szczegóły pracownika
GET    /api/hr/employees/{id}/contracts         - Umowy pracownika
GET    /api/hr/employees/{id}/time-records      - Ewidencja czasu pracy
POST   /api/hr/employees/{id}/time-records      - Dodaj wpis
GET    /api/hr/contracts                        - Wszystkie umowy
GET    /api/hr/contracts/{id}                   - Szczegóły umowy
```

### 5.2 PayrollController (Płace)
**Endpoint:** `/api/payroll`

```
GET    /api/payroll/lists                       - Listy płac
GET    /api/payroll/lists/{id}                  - Szczegóły listy
POST   /api/payroll/lists/generate              - Generuj listę płac
GET    /api/payroll/lists/{id}/items            - Pozycje listy
GET    /api/payroll/components                  - Składniki płacowe
```

### 5.3 PPKController (Pracownicze Plany Kapitałowe)
**Endpoint:** `/api/ppk`

```
GET    /api/ppk/participants                    - Uczestnicy PPK
GET    /api/ppk/participants/{id}               - Szczegóły uczestnika
GET    /api/ppk/contributions                   - Wpłaty
POST   /api/ppk/reports/generate                - Generuj raport PPK
GET    /api/ppk/reports/{id}                    - Szczegóły raportu
POST   /api/ppk/reports/{id}/send               - Wyślij do instytucji
```

---

## Podsumowanie

### Nowe Controllery do utworzenia:

| Faza | Controller | Priorytet | Szacowany nakład |
|------|------------|-----------|------------------|
| 1 | CustomerOrdersController | WYSOKI | 4-6h |
| 1 | ProductTemplatesController | WYSOKI | 2-3h |
| 1 | ProductGroupsController | WYSOKI | 2-3h |
| 1 | ProductAttributesController | WYSOKI | 2-3h |
| 1 | ContractorGroupsController | WYSOKI | 2-3h |
| 2 | Rozszerzenie Dictionary | ŚREDNI | 2-3h |
| 2 | Rozszerzenie Finance | ŚREDNI | 3-4h |
| 2 | Rozszerzenie Inventory | ŚREDNI | 3-4h |
| 3 | DeclarationsController | ŚREDNI | ✅ Zrobione |
| 3 | JPKController | ŚREDNI | ✅ Zrobione |
| 3 | IntrastatController | ŚREDNI | ✅ Zrobione |
| 4 | ConfigurationsController | NISKI | ✅ Zrobione |
| 4 | CustomFieldsController | NISKI | ❌ Wymaga innego podejścia |
| 4 | PrintController | NISKI | ✅ Zrobione |
| 4 | ReportsController | NISKI | ✅ Zrobione |
| 4 | EcommerceController | NISKI | ✅ Zrobione |
| 4 | AutomationController | NISKI | ❌ Brak managera w SDK |
| 5 | HRController | OPCJA | 4-5h |
| 5 | PayrollController | OPCJA | 4-5h |
| 5 | PPKController | OPCJA | 3-4h |

### Rozszerzenie istniejących DTO:

1. **CustomerOrderDto** - nowy DTO
2. **ProductTemplateDto** - nowy DTO
3. **ProductGroupDto** - rozszerzenie
4. **ContractorGroupDto** - nowy DTO
5. **DeclarationDto** - nowy DTO
6. **JPKDto** - nowy DTO
7. **IntrastatDto** - nowy DTO
8. **ConfigurationDto** - nowy DTO
9. **CustomFieldDto** - nowy DTO
10. **PrintTemplateDto** - nowy DTO
11. **ReportDto** - nowy DTO

---

## Następne kroki

1. **Decyzja**: Które fazy implementować?
2. **Priorytet**: W jakiej kolejności?
3. **Start**: Zacznij od Fazy 1 (core managers)
