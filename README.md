<div align="center">

# 🔗 Nexo Sfera REST API

**Profesjonalne REST API dla systemu InsERT Nexo**
Subiekt · Rachmistrz · Rewizor · Gratyfikant · Gestor

[![.NET](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Swagger](https://img.shields.io/badge/Swagger-85EA2D?style=for-the-badge&logo=swagger&logoColor=black)](http://mssql-insert.sys.dmservice.pl:5000/swagger)
[![Windows](https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)](https://www.microsoft.com/windows)

[![GitHub stars](https://img.shields.io/github/stars/serwin35/nexo-sfera-api?style=flat-square)](https://github.com/serwin35/nexo-sfera-api/stargazers)
[![GitHub issues](https://img.shields.io/github/issues/serwin35/nexo-sfera-api?style=flat-square)](https://github.com/serwin35/nexo-sfera-api/issues)
[![Last commit](https://img.shields.io/github/last-commit/serwin35/nexo-sfera-api?style=flat-square)](https://github.com/serwin35/nexo-sfera-api/commits)

Rozwijane przez **[DMservice](https://dmservice.pl)** | 📧 mateusz.serwinowski@dmservice.pl

</div>

---

> 🚀 **Demo:** http://mssql-insert.sys.dmservice.pl:5000/swagger
> 🔑 Wymagany klucz API w nagłówku `X-API-Key`

---

## 📋 Spis treści

- [Wymagania](#wymagania)
- [Konfiguracja](#konfiguracja)
- [Multi-tenant (wiele firm)](#multi-tenant-wiele-firm)
- [Autentykacja](#autentykacja)
- [Endpointy API](#endpointy-api)
- [Przykłady użycia](#przykłady-użycia)

---

## ⚙️ Wymagania

- Windows 10/11 lub Windows Server
- .NET 8.0 SDK
- Zainstalowany InsERT Nexo z licencją na Sferę
- SQL Server z bazą danych Nexo

## 🔧 Konfiguracja

Edytuj plik `src/appsettings.json`:

```json
{
  "Sfera": {
    "Server": "(local)\\INSERTNEXO",
    "Database": "NazwaTwojejBazy",
    "UseWindowsAuth": true,
    "SqlLogin": null,
    "SqlPassword": null,
    "NexoLogin": "Szef",
    "NexoPassword": "robocze",
    "Product": "Subiekt"
  }
}
```

### Parametry konfiguracji

| Parametr | Opis |
|----------|------|
| `Server` | Nazwa instancji SQL Server |
| `Database` | Nazwa bazy danych Nexo |
| `UseWindowsAuth` | `true` dla autentykacji Windows, `false` dla SQL |
| `SqlLogin` | Login SQL (gdy UseWindowsAuth = false) |
| `SqlPassword` | Hasło SQL (gdy UseWindowsAuth = false) |
| `NexoLogin` | Login operatora Nexo |
| `NexoPassword` | Hasło operatora Nexo |
| `Product` | Produkt: Subiekt, Rachmistrz, Rewizor, Gratyfikant, Gestor |

## 🚀 Uruchomienie

```bash
cd src
dotnet run
```

API będzie dostępne pod adresem: `http://localhost:5000`
Swagger UI: `http://localhost:5000/swagger`

## 🏢 Multi-tenant (wiele firm)

API obsługuje scenariusze multi-tenant, gdzie różne klucze API mogą korzystać z różnych operatorów Nexo. To umożliwia obsługę wielu firm lub działów w ramach jednej instancji API.

### Konfiguracja kluczy API z osobnymi operatorami

Edytuj plik `src/appsettings.json`:

```json
{
  "ApiKeys": {
    "Keys": [
      {
        "Key": "klucz-api-firma-a",
        "Name": "Firma A - Główna",
        "IsActive": true,
        "NexoLogin": "operator1",
        "NexoPassword": "haslo1",
        "DefaultWarehouse": "MG1",
        "DefaultBranch": "WAW",
        "CompanyDescription": "Firma A - Oddział Warszawa"
      },
      {
        "Key": "klucz-api-firma-b",
        "Name": "Firma B - E-commerce",
        "IsActive": true,
        "NexoLogin": "operator2",
        "NexoPassword": "haslo2",
        "DefaultWarehouse": "MG2",
        "DefaultBranch": "KRK",
        "CompanyDescription": "Firma B - Oddział Kraków"
      },
      {
        "Key": "klucz-api-default",
        "Name": "Klucz domyślny",
        "IsActive": true,
        "CompanyDescription": "Używa domyślnego operatora z sekcji Sfera"
      }
    ]
  }
}
```

### Parametry klucza API

| Parametr | Wymagany | Opis |
|----------|----------|------|
| `Key` | **Tak** | Unikalny klucz API (używany w nagłówku Authorization) |
| `Name` | **Tak** | Nazwa opisowa klucza dla celów identyfikacji |
| `IsActive` | **Tak** | Czy klucz jest aktywny (`true`/`false`) |
| `NexoLogin` | Nie | Login operatora Nexo dla tego klucza (opcjonalnie) |
| `NexoPassword` | Nie | Hasło operatora Nexo dla tego klucza (opcjonalnie) |
| `Database` | Nie | Nazwa bazy danych Nexo (dla scenariuszy z wieloma bazami) |
| `DefaultWarehouse` | Nie | Domyślny symbol magazynu |
| `DefaultBranch` | Nie | Domyślny symbol oddziału |
| `CompanyDescription` | Nie | Opis firmy/kontekstu dla celów logowania |

### Jak działa multi-tenant

1. **Autentykacja**: Każde żądanie API jest autoryzowane kluczem API przekazanym w nagłówku `Authorization: Bearer {klucz}`

2. **Przełączanie operatora**: Jeśli klucz API ma zdefiniowane `NexoLogin` i `NexoPassword`, system automatycznie przełącza się na tego operatora przed wykonaniem operacji

3. **Izolacja danych**: Każdy operator w Nexo ma własne uprawnienia i widzi tylko dane, do których ma dostęp

4. **Domyślny operator**: Jeśli klucz API nie ma zdefiniowanych danych operatora, używany jest operator z sekcji `Sfera.NexoLogin`/`Sfera.NexoPassword`

### Przykład użycia

```bash
# Firma A - operator1
curl -X POST http://localhost:5000/api/documents/sales-invoice \
  -H "Authorization: Bearer klucz-api-firma-a" \
  -H "Content-Type: application/json" \
  -d '{
    "CustomerId": 123,
    "Items": [...]
  }'

# Firma B - operator2
curl -X POST http://localhost:5000/api/documents/sales-invoice \
  -H "Authorization: Bearer klucz-api-firma-b" \
  -H "Content-Type: application/json" \
  -d '{
    "CustomerId": 456,
    "Items": [...]
  }'
```

### Bezpieczeństwo

⚠️ **Ważne**:
- Przechowuj klucze API w bezpiecznym miejscu (np. zmienne środowiskowe, Azure Key Vault)
- Używaj HTTPS w produkcji
- Regularnie rotuj klucze API
- Nadawaj minimalne wymagane uprawnienia operatorom Nexo

## 🌐 Środowisko testowe

- **API URL:** `http://mssql-insert.sys.dmservice.pl:5000/`
- **Swagger:** `http://mssql-insert.sys.dmservice.pl:5000/swagger`
- **API Key:** Wymagany w headerze `X-API-Key`

## 📡 Endpointy API

### 🔷 Health Check
- `GET /api/health` - Status API
- `GET /api/health/sfera` - Status połączenia ze Sferą
- `POST /api/health/reconnect` - Ponowne połączenie

### 🔷 Kontrahenci (Customers)
- `GET /api/customers` - Lista kontrahentów
- `GET /api/customers/{id}` - Kontrahent po ID
- `GET /api/customers/by-nip/{nip}` - Kontrahent po NIP
- `POST /api/customers` - Utwórz kontrahenta
- `PUT /api/customers/{id}` - Aktualizuj kontrahenta
- `DELETE /api/customers/{id}` - Usuń kontrahenta

### 🔷 Produkty (Products)
- `GET /api/products` - Lista produktów
- `GET /api/products/{id}` - Produkt po ID
- `GET /api/products/by-symbol/{symbol}` - Produkt po symbolu
- `GET /api/products/by-ean/{ean}` - Produkt po EAN
- `POST /api/products` - Utwórz produkt
- `PUT /api/products/{id}` - Aktualizuj produkt
- `DELETE /api/products/{id}` - Usuń produkt

### 🔷 Dokumenty handlowe (Documents)
- `GET /api/documents` - Lista dokumentów
- `GET /api/documents/{id}` - Dokument po ID
- `GET /api/documents/by-number/{number}` - Dokument po numerze
- `POST /api/documents/sales-invoice` - Utwórz fakturę sprzedaży (FS)
- `POST /api/documents/receipt` - Utwórz paragon
- `POST /api/documents/customer-order` - Utwórz zamówienie od klienta (ZK)
- `POST /api/documents/purchase-invoice` - Utwórz fakturę zakupu (FZ)

### 🔷 Dokumenty magazynowe (Warehouse Documents)
- `GET /api/warehouse-documents` - Lista dokumentów magazynowych
- `GET /api/warehouse-documents/{id}` - Dokument po ID
- `POST /api/warehouse-documents/wz` - Utwórz wydanie zewnętrzne (WZ)
- `POST /api/warehouse-documents/pz` - Utwórz przyjęcie zewnętrzne (PZ)
- `POST /api/warehouse-documents/rw` - Utwórz rozchód wewnętrzny (RW)
- `POST /api/warehouse-documents/pw` - Utwórz przychód wewnętrzny (PW)
- `POST /api/warehouse-documents/mm` - Utwórz przesunięcie międzymagazynowe (MM)
- `POST /api/warehouse-documents/{id}/associate` - Powiąż dokumenty (np. RW z PW)

### 🔷 Płatności (Payments)
- `GET /api/payments/cash` - Lista operacji kasowych
- `GET /api/payments/cash/{id}` - Operacja kasowa po ID
- `POST /api/payments/cash/kp` - Utwórz KP (przyjęcie gotówki)
- `POST /api/payments/cash/kw` - Utwórz KW (wydanie gotówki)
- `GET /api/payments/bank` - Lista operacji bankowych
- `GET /api/payments/bank/{id}` - Operacja bankowa po ID
- `POST /api/payments/bank/bp` - Utwórz BP (wpłata bankowa)
- `POST /api/payments/bank/bw` - Utwórz BW (wypłata bankowa)
- `GET /api/payments/settlements` - Lista rozrachunków

### 🔷 Oferty (Offers) — Gestor
- `GET /api/offers` - Lista ofert
- `GET /api/offers/{id}` - Oferta po ID
- `GET /api/offers/by-number/{number}` - Oferta po numerze
- `POST /api/offers` - Utwórz ofertę
- `POST /api/offers/{id}/accept` - Zaakceptuj ofertę
- `POST /api/offers/{id}/close` - Zamknij ofertę

### 🔷 Magazyny (Warehouses)
- `GET /api/warehouses` - Lista magazynów
- `GET /api/warehouses/{symbol}` - Magazyn po symbolu
- `GET /api/warehouses/stock/{productSymbol}` - Stan magazynowy produktu

### 🔷 Stany magazynowe (Inventory)
- `GET /api/inventory` - Lista stanów magazynowych
- `GET /api/inventory/product/{id}` - Stan dla produktu
- `GET /api/inventory/movements` - Historia ruchów magazynowych
- `GET /api/inventory/valuation` - Wycena magazynu

### 🔷 Słowniki (Dictionary)
- `GET /api/dictionary/vat-rates` - Stawki VAT
- `GET /api/dictionary/units` - Jednostki miary
- `GET /api/dictionary/currencies` - Waluty
- `GET /api/dictionary/exchange-rates` - Kursy walut
- `GET /api/dictionary/payment-methods` - Formy płatności
- `GET /api/dictionary/price-lists` - Cenniki
- `GET /api/dictionary/countries` - Kraje

### 🔷 Raporty finansowe (Finance Reports)
- `GET /api/finance/cash-registers` - Stanowiska kasowe
- `GET /api/finance/cash-reports` - Raporty kasowe
- `GET /api/finance/bank-accounts` - Rachunki bankowe
- `GET /api/finance/bank-statements` - Wyciągi bankowe
- `GET /api/finance/aging-report` - Raport wiekowania

### 🔷 System
- `GET /api/system/info` - Informacje o firmie
- `GET /api/system/operator` - Aktualny operator
- `GET /api/system/license` - Informacje o licencji

### 🔷 Deklaracje (Declarations)
- `GET /api/declarations/vat` - Lista deklaracji VAT
- `GET /api/declarations/vat/{id}` - Szczegóły deklaracji
- `GET /api/declarations/types` - Typy deklaracji

### 🔷 JPK
- `GET /api/jpk` - Lista plików JPK
- `GET /api/jpk/{id}` - Szczegóły JPK
- `GET /api/jpk/types` - Typy JPK

### 🔷 Konfiguracje
- `GET /api/configurations/categories` - Kategorie konfiguracji
- `GET /api/configurations/documents` - Konfiguracje dokumentów
- `GET /api/configurations/numbering` - Konfiguracje numeracji

### 🔷 Raporty
- `GET /api/reports/types` - Typy raportów
- `GET /api/reports/sales/summary` - Podsumowanie sprzedaży
- `GET /api/reports/sales/by-product` - Sprzedaż wg produktów
- `GET /api/reports/sales/by-customer` - Sprzedaż wg klientów

### 🔷 E-commerce
- `GET /api/ecommerce/integrations` - Integracje e-commerce
- `GET /api/ecommerce/offers` - Oferty internetowe
- `GET /api/ecommerce/packages` - Paczki wysyłkowe

## 💡 Przykłady użycia

### Utworzenie kontrahenta

```bash
curl -X POST http://localhost:5000/api/customers \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "symbol": "KLIENT001",
    "shortName": "Firma ABC",
    "fullName": "Firma ABC Sp. z o.o.",
    "nip": "1234567890",
    "email": "kontakt@firmaabc.pl",
    "phone": "123456789",
    "type": 0,
    "address": {
      "street": "ul. Przykladowa",
      "buildingNumber": "10",
      "city": "Warszawa",
      "postalCode": "00-001"
    }
  }'
```

### Utworzenie faktury sprzedaży

```bash
curl -X POST http://localhost:5000/api/documents/sales-invoice \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "type": 1,
    "customerNIP": "1234567890",
    "warehouseSymbol": "MAG",
    "notes": "Faktura testowa",
    "items": [
      {
        "productId": 123,
        "productSymbol": "TOWAR001",
        "quantity": 5,
        "priceNet": 100.00
      }
    ]
  }'
```

### Utworzenie paragonu

```bash
curl -X POST http://localhost:5000/api/documents/receipt \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "warehouseSymbol": "MAG",
    "notes": "Paragon testowy",
    "items": [
      {
        "productSymbol": "TOWAR001",
        "quantity": 2,
        "priceGross": 123.00
      }
    ]
  }'
```

### Utworzenie dokumentu WZ

```bash
curl -X POST http://localhost:5000/api/warehouse-documents/wz \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "warehouseSymbol": "MAG",
    "customerNIP": "1234567890",
    "notes": "Wydanie zewnetrzne",
    "items": [
      {
        "productId": 123,
        "quantity": 10
      }
    ]
  }'
```

### Utworzenie dokumentu PW (Przychód wewnętrzny)

```bash
curl -X POST http://localhost:5000/api/warehouse-documents/pw \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "warehouseSymbol": "MG",
    "issueDate": "2024-05-06T00:00:00",
    "notes": "Import z systemu zewnetrznego",
    "items": [
      {
        "productSymbol": "TOWAR001",
        "quantity": 4.0,
        "unit": "szt.",
        "priceNet": 16.05
      },
      {
        "productEan": "5901234123457",
        "quantity": 10.0,
        "priceNet": 25.00
      }
    ]
  }'
```

### Utworzenie dokumentu RW (Rozchód wewnętrzny)

```bash
curl -X POST http://localhost:5000/api/warehouse-documents/rw \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "warehouseSymbol": "MG",
    "issueDate": "2024-05-06T11:08:08",
    "notes": "Zuzycie wewnetrzne - produkcja",
    "items": [
      {
        "productSymbol": "MATERIAL001",
        "quantity": 4.0,
        "unit": "szt.",
        "priceNet": 13.90
      },
      {
        "productId": 456,
        "quantity": 2.5,
        "priceNet": 19.95
      }
    ]
  }'
```

**Uwaga:** Dla dokumentów RW ceny mogą być ignorowane — system automatycznie wycenia rozchód wg metody FIFO/LIFO/średniej ważonej skonfigurowanej w systemie.

### Utworzenie dokumentu MM (Przesunięcie międzymagazynowe)

```bash
curl -X POST http://localhost:5000/api/warehouse-documents/mm \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "warehouseSymbol": "MAG1",
    "targetWarehouseSymbol": "MAG2",
    "issueDate": "2024-05-06T00:00:00",
    "notes": "Przesuniecie towaru",
    "items": [
      {
        "productSymbol": "TOWAR001",
        "quantity": 5.0
      }
    ]
  }'
```

### Powiązanie dokumentów magazynowych (skojarzenie RW z PW)

```bash
# Najpierw utwórz dokumenty PW i RW, a następnie połącz je przez ID
curl -X POST http://localhost:5000/api/warehouse-documents/109622/associate \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "targetDocumentId": 109623,
    "relationType": "related"
  }'
```

Odpowiedź:
```json
{
  "success": true,
  "data": {
    "sourceDocumentId": 109622,
    "targetDocumentId": 109623,
    "relationType": "related"
  },
  "message": "Documents RW MG/2026/01/1 and PW MG/2026/01/1 associated successfully"
}
```

### Utworzenie oferty (Gestor)

```bash
curl -X POST http://localhost:5000/api/offers \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "customerNIP": "1234567890",
    "validUntil": "2026-02-28",
    "notes": "Oferta handlowa",
    "items": [
      {
        "productId": 123,
        "productSymbol": "TOWAR001",
        "quantity": 100,
        "unitPriceNet": 50.00
      }
    ]
  }'
```

### Utworzenie operacji kasowej KP

```bash
curl -X POST http://localhost:5000/api/payments/cash/kp \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "amount": 1000.00,
    "description": "Wplata gotowki",
    "customerNIP": "1234567890",
    "cashRegisterSymbol": "KASA1"
  }'
```

### Pobranie listy produktów z filtrowaniem

```bash
curl "http://localhost:5000/api/products?search=laptop&activeOnly=true&page=1&pageSize=20" \
  -H "X-API-Key: your-api-key"
```

## 📁 Struktura projektu

```
nexo-sfera-api/
├── src/
│   ├── Controllers/         # Kontrolery REST (18+)
│   ├── Services/            # Serwisy (Sfera)
│   ├── Models/
│   │   ├── Dto/             # Data Transfer Objects
│   │   ├── Requests/        # Modele requestów
│   │   └── Responses/       # Modele odpowiedzi
│   ├── Configuration/       # Konfiguracja
│   ├── Authentication/      # API Key auth
│   ├── Helpers/             # Helpery
│   ├── Middleware/          # Middleware
│   ├── Program.cs           # Entry point
│   └── appsettings.json     # Konfiguracja
├── nexoSDK_58.0.2.8985/     # SDK Nexo Sfera v58
└── README.md
```

## 🔬 Ważne informacje techniczne

### Entity Framework 6 i .NET 8

SDK Nexo Sfera używa Entity Framework 6, który wymaga specjalnej obsługi w .NET 8:

1. **WindowsFormsSynchronizationContext** — wymagany do poprawnego działania EF6
2. **STA Thread** — operacje SDK muszą być wykonywane w wątku STA
3. **ExecuteWithLockAsync** — wszystkie operacje tworzenia dokumentów używają tego wrappera

### Wzorzec tworzenia dokumentów

```csharp
// 1. Ustaw magazyn na Dokument (nie na Dane!)
faktura.Dokument.Magazyn = magazyn;

// 2. Zarezerwuj numer PRZED dodaniem pozycji
faktura.ZarezerwujNumer();

// 3. Dodaj pozycje przez ID produktu
var pozycja = faktura.Pozycje.Dodaj(towarId);
pozycja.Ilosc = quantity;

// 4. Zapisz dokument
faktura.Zapisz();
```

## 📝 Uwagi techniczne

### Walidacja stanów magazynowych dla usług

API automatycznie pomija walidację stanów magazynowych dla produktów typu **usługa** (Rodzaj_Id = 1).

Oznacza to, że:
- ✅ Usługi mogą być dodawane do dokumentów sprzedaży bez sprawdzania stanów
- ✅ Nie pojawią się błędy typu "Insufficient stock for 'USLUGA001'"
- ✅ Usługi są rozpoznawane po właściwości `Rodzaj_Id = 1` w tabeli Asortymenty

**Przykład**:
```
Produkt: DMSUSGI00081 (Usługa)
Rodzaj_Id: 1
Stan magazynowy: 0 (nie ma znaczenia)
Walidacja: POMINIĘTA ✓
```

Walidacja stanów działa normalnie dla:
- Towarów (Rodzaj_Id = 0)
- Kompletów (Rodzaj_Id = 2)
- Materiałów (Rodzaj_Id = 3)

### Statusy dokumentów w odpowiedziach API

Wszystkie odpowiedzi API zawierają pełne informacje o statusie dokumentu:

```json
{
  "id": 12345,
  "number": "FS/2026/01/123",
  "statusId": 3,
  "status": "Zrealizowano",
  "statusSymbol": "ZREAL",
  ...
}
```

- `statusId` — ID numeryczne statusu
- `status` — Nazwa statusu (czytelna dla człowieka)
- `statusSymbol` — Symbol statusu (do porównań programistycznych)

### Pomijanie uwag importowych

API automatycznie pomija dodawanie uwag typu "Import ILUO: ..." dla:
- Faktur sprzedaży (FS)
- Faktur zakupu (FZ)
- Paragonów (PA)
- Korekt faktur
- Faktur zaliczkowych
- Faktur VAT marża

Jeśli przekażesz pole `notes` w żądaniu tworzenia tych dokumentów, zostanie ono **zignorowane** i zalogowane jako informacja.

Uwagi są dodawane normalnie dla:
- Dokumentów magazynowych (WZ, PZ, RW, PW, MM)
- Zamówień (ZK, ZD)
- Innych typów dokumentów

## 📄 Licencja

Projekt wymaga ważnej licencji InsERT Nexo z modułem Sfera.

---

⭐ Jeśli projekt jest przydatny, zostaw gwiazdkę!
