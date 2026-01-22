# Nexo Sfera API - Plan Kompletnego REST API

## Status Obecny

### Istniejące Controllery (18)

| Controller | Endpoint Base | Funkcjonalność | Status |
|------------|--------------|----------------|--------|
| AssemblyController | `/api/assembly` | Montaż/demontaż | ✅ OK |
| CustomersController | `/api/customers` | Kontrahenci CRUD | ✅ OK |
| DiagnosticsController | `/api/diagnostics` | SDK discovery | ✅ OK |
| DictionaryController | `/api/dictionary` | Słowniki (VAT, waluty, jednostki) | ✅ OK |
| DiscountsController | `/api/discounts` | Rabaty + atrybuty + grupy | ✅ OK |
| DocumentsController | `/api/documents` | Faktury, korekty, paragony | ✅ OK |
| EmployeesController | `/api/employees` | Pracownicy (basic) | ⚠️ Basic |
| FinanceReportsController | `/api/finance` | Raporty kasowe/bankowe | ✅ OK |
| HealthController | `/api/health` | Health checks | ✅ OK |
| InventoryController | `/api/inventory` | Stany magazynowe | ✅ OK |
| KsefController | `/api/ksef` | E-faktury KSeF | ✅ OK |
| OffersController | `/api/offers` | Oferty | ✅ OK |
| OrdersController | `/api/orders` | Zamówienia do dostawców | ⚠️ Partial |
| PaymentsController | `/api/payments` | Operacje kasowe/bankowe | ✅ OK |
| ProductsController | `/api/products` | Asortyment CRUD | ✅ OK |
| SystemController | `/api/system` | Info o firmie/operatorze | ✅ OK |
| WarehouseDocumentsController | `/api/warehouse-documents` | WZ/PZ/RW/PW/MM | ✅ OK |
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

## Faza 2: Rozszerzenie Dictionary/Finance (Priorytet: ŚREDNI)

### 2.1 Rozszerzenie DictionaryController

Dodać do `/api/dictionary`:

```
GET    /api/dictionary/cash-operation-types     - Rodzaje operacji kasowych
GET    /api/dictionary/bank-operation-types     - Rodzaje operacji bankowych
GET    /api/dictionary/document-types           - Typy dokumentów
GET    /api/dictionary/document-statuses        - Statusy dokumentów
GET    /api/dictionary/countries                - Kraje
GET    /api/dictionary/regions                  - Województwa
GET    /api/dictionary/delivery-methods         - Metody dostawy
GET    /api/dictionary/shipping-carriers        - Przewoźnicy
```

### 2.2 Rozszerzenie FinanceReportsController

Dodać do `/api/finance`:

```
GET    /api/finance/aging-report                - Raport wiekowania należności
GET    /api/finance/aging-report/payables       - Raport wiekowania zobowiązań
GET    /api/finance/cash-flow                   - Cash flow
GET    /api/finance/settlements                 - Rozliczenia
GET    /api/finance/settlements/{id}/payments   - Płatności dla rozliczenia
POST   /api/finance/settlements/match           - Dopasuj płatności
```

### 2.3 Rozszerzenie InventoryController

Dodać do `/api/inventory`:

```
GET    /api/inventory/movements                 - Historia ruchów magazynowych
GET    /api/inventory/movements/product/{id}    - Ruchy dla produktu
GET    /api/inventory/valuation                 - Wycena magazynu
GET    /api/inventory/valuation/method/{method} - Wycena wg metody (FIFO/LIFO/AVG)
POST   /api/inventory/stocktaking               - Rozpocznij inwentaryzację
GET    /api/inventory/stocktaking/{id}          - Status inwentaryzacji
POST   /api/inventory/stocktaking/{id}/items    - Dodaj pozycję
POST   /api/inventory/stocktaking/{id}/close    - Zamknij inwentaryzację
```

---

## Faza 3: Podatki i Deklaracje (Priorytet: ŚREDNI)

### 3.1 DeclarationsController (Deklaracje VAT)
**Endpoint:** `/api/declarations`

```
GET    /api/declarations/vat                    - Lista deklaracji VAT
GET    /api/declarations/vat/{id}               - Szczegóły deklaracji
POST   /api/declarations/vat/generate           - Generuj deklarację
GET    /api/declarations/vat/{id}/preview       - Podgląd
POST   /api/declarations/vat/{id}/send          - Wyślij do US
GET    /api/declarations/vat/{id}/upo           - Pobierz UPO
```

### 3.2 JPKController (Jednolity Plik Kontrolny)
**Endpoint:** `/api/jpk`

```
GET    /api/jpk                                 - Lista wygenerowanych JPK
GET    /api/jpk/{id}                            - Szczegóły
POST   /api/jpk/vat/generate                    - Generuj JPK_VAT
POST   /api/jpk/fa/generate                     - Generuj JPK_FA
POST   /api/jpk/mag/generate                    - Generuj JPK_MAG
GET    /api/jpk/{id}/download                   - Pobierz plik XML
POST   /api/jpk/{id}/send                       - Wyślij do MF
GET    /api/jpk/{id}/status                     - Status wysyłki
```

### 3.3 IntrastatController (Deklaracje Intrastat)
**Endpoint:** `/api/intrastat`

```
GET    /api/intrastat                           - Lista deklaracji
GET    /api/intrastat/{id}                      - Szczegóły
POST   /api/intrastat/generate                  - Generuj deklarację
GET    /api/intrastat/{id}/items                - Pozycje
POST   /api/intrastat/{id}/send                 - Wyślij
```

---

## Faza 4: Zaawansowane funkcje (Priorytet: NISKI)

### 4.1 ConfigurationsController (Konfiguracje)
**Endpoint:** `/api/configurations`

```
GET    /api/configurations/documents            - Konfiguracje dokumentów
GET    /api/configurations/documents/{type}     - Dla typu dokumentu
PUT    /api/configurations/documents/{type}     - Aktualizuj
GET    /api/configurations/numbering            - Konfiguracje numeracji
GET    /api/configurations/numbering/{type}     - Dla typu
```

### 4.2 CustomFieldsController (Pola własne)
**Endpoint:** `/api/custom-fields`

```
GET    /api/custom-fields                       - Lista definicji pól własnych
GET    /api/custom-fields/{entityType}          - Pola dla typu encji
POST   /api/custom-fields                       - Utwórz pole własne
PUT    /api/custom-fields/{id}                  - Aktualizuj
DELETE /api/custom-fields/{id}                  - Usuń
GET    /api/custom-fields/dictionaries          - Słowniki własne
```

### 4.3 PrintTemplatesController (Szablony wydruków)
**Endpoint:** `/api/print-templates`

```
GET    /api/print-templates                     - Lista szablonów
GET    /api/print-templates/{id}                - Szczegóły
GET    /api/print-templates/for-document/{type} - Szablony dla typu dokumentu
POST   /api/print-templates/{id}/generate       - Generuj wydruk
GET    /api/print-templates/{id}/preview        - Podgląd PDF
```

### 4.4 ReportsController (Raporty)
**Endpoint:** `/api/reports`

```
GET    /api/reports                             - Lista dostępnych raportów
GET    /api/reports/{id}                        - Definicja raportu
POST   /api/reports/{id}/execute                - Wykonaj raport
GET    /api/reports/sales/summary               - Podsumowanie sprzedaży
GET    /api/reports/purchases/summary           - Podsumowanie zakupów
GET    /api/reports/inventory/turnover          - Rotacja magazynowa
GET    /api/reports/customers/ranking           - Ranking klientów
GET    /api/reports/products/bestsellers        - Bestsellery
```

### 4.5 E-commerceController (Handel elektroniczny)
**Endpoint:** `/api/ecommerce`

```
GET    /api/ecommerce/integrations              - Lista integracji
GET    /api/ecommerce/orders                    - Zamówienia z platform
POST   /api/ecommerce/orders/{id}/import        - Importuj zamówienie
GET    /api/ecommerce/products/sync-status      - Status synchronizacji
POST   /api/ecommerce/products/sync             - Synchronizuj produkty
```

### 4.6 AutomationController (Automatyzacje)
**Endpoint:** `/api/automation`

```
GET    /api/automation/rules                    - Lista reguł automatyzacji
GET    /api/automation/rules/{id}               - Szczegóły reguły
POST   /api/automation/rules                    - Utwórz regułę
PUT    /api/automation/rules/{id}               - Aktualizuj
DELETE /api/automation/rules/{id}               - Usuń
POST   /api/automation/rules/{id}/execute       - Wykonaj manualnie
GET    /api/automation/history                  - Historia wykonań
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
| 3 | DeclarationsController | ŚREDNI | 4-5h |
| 3 | JPKController | ŚREDNI | 4-5h |
| 3 | IntrastatController | ŚREDNI | 3-4h |
| 4 | ConfigurationsController | NISKI | 2-3h |
| 4 | CustomFieldsController | NISKI | 3-4h |
| 4 | PrintTemplatesController | NISKI | 3-4h |
| 4 | ReportsController | NISKI | 4-5h |
| 4 | E-commerceController | NISKI | 4-5h |
| 4 | AutomationController | NISKI | 3-4h |
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
