# Nexo Sfera REST API

REST API dla systemu InsERT Nexo, wykorzystujace Sfere jako warstwa komunikacji.

## Wymagania

- Windows 10/11 lub Windows Server
- .NET 8.0 SDK
- Zainstalowany InsERT Nexo z licencja na Sfere
- SQL Server z baza danych Nexo

## Konfiguracja

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
| `SqlPassword` | Haslo SQL (gdy UseWindowsAuth = false) |
| `NexoLogin` | Login operatora Nexo |
| `NexoPassword` | Haslo operatora Nexo |
| `Product` | Produkt: Subiekt, Rachmistrz, Rewizor, Gratyfikant, Gestor |

## Uruchomienie

```bash
cd src
dotnet run
```

API bedzie dostepne pod adresem: `http://localhost:5000`
Swagger UI: `http://localhost:5000/swagger`

## Srodowisko testowe

- **API URL:** `http://mssql-insert.sys.dmservice.pl:5000/`
- **Swagger:** `http://mssql-insert.sys.dmservice.pl:5000/swagger`
- **API Key:** Wymagany w headerze `X-API-Key`

## Endpointy API

### Health Check
- `GET /api/health` - Status API
- `GET /api/health/sfera` - Status polaczenia ze Sfera
- `POST /api/health/reconnect` - Ponowne polaczenie

### Kontrahenci (Customers)
- `GET /api/customers` - Lista kontrahentow
- `GET /api/customers/{id}` - Kontrahent po ID
- `GET /api/customers/by-nip/{nip}` - Kontrahent po NIP
- `POST /api/customers` - Utworz kontrahenta
- `PUT /api/customers/{id}` - Aktualizuj kontrahenta
- `DELETE /api/customers/{id}` - Usun kontrahenta

### Produkty (Products)
- `GET /api/products` - Lista produktow
- `GET /api/products/{id}` - Produkt po ID
- `GET /api/products/by-symbol/{symbol}` - Produkt po symbolu
- `GET /api/products/by-ean/{ean}` - Produkt po EAN
- `POST /api/products` - Utworz produkt
- `PUT /api/products/{id}` - Aktualizuj produkt
- `DELETE /api/products/{id}` - Usun produkt

### Dokumenty handlowe (Documents)
- `GET /api/documents` - Lista dokumentow
- `GET /api/documents/{id}` - Dokument po ID
- `GET /api/documents/by-number/{number}` - Dokument po numerze
- `POST /api/documents/sales-invoice` - Utworz fakture sprzedazy (FS)
- `POST /api/documents/receipt` - Utworz paragon
- `POST /api/documents/customer-order` - Utworz zamowienie od klienta (ZK)
- `POST /api/documents/purchase-invoice` - Utworz fakture zakupu (FZ)

### Dokumenty magazynowe (Warehouse Documents)
- `GET /api/warehouse-documents` - Lista dokumentow magazynowych
- `GET /api/warehouse-documents/{id}` - Dokument po ID
- `POST /api/warehouse-documents/wz` - Utworz wydanie zewnetrzne (WZ)
- `POST /api/warehouse-documents/pz` - Utworz przyjecie zewnetrzne (PZ)
- `POST /api/warehouse-documents/rw` - Utworz rozchod wewnetrzny (RW)
- `POST /api/warehouse-documents/pw` - Utworz przychod wewnetrzny (PW)
- `POST /api/warehouse-documents/mm` - Utworz przesuniecie miedzymagazynowe (MM)

### Platnosci (Payments)
- `GET /api/payments/cash` - Lista operacji kasowych
- `GET /api/payments/cash/{id}` - Operacja kasowa po ID
- `POST /api/payments/cash/kp` - Utworz KP (przyjecie gotowki)
- `POST /api/payments/cash/kw` - Utworz KW (wydanie gotowki)
- `GET /api/payments/bank` - Lista operacji bankowych
- `GET /api/payments/bank/{id}` - Operacja bankowa po ID
- `POST /api/payments/bank/bp` - Utworz BP (wplata bankowa)
- `POST /api/payments/bank/bw` - Utworz BW (wyplata bankowa)
- `GET /api/payments/settlements` - Lista rozrachunkow

### Oferty (Offers) - Gestor
- `GET /api/offers` - Lista ofert
- `GET /api/offers/{id}` - Oferta po ID
- `GET /api/offers/by-number/{number}` - Oferta po numerze
- `POST /api/offers` - Utworz oferte
- `POST /api/offers/{id}/accept` - Zaakceptuj oferte
- `POST /api/offers/{id}/close` - Zamknij oferte

### Magazyny (Warehouses)
- `GET /api/warehouses` - Lista magazynow
- `GET /api/warehouses/{symbol}` - Magazyn po symbolu
- `GET /api/warehouses/stock/{productSymbol}` - Stan magazynowy produktu

### Stany magazynowe (Inventory)
- `GET /api/inventory` - Lista stanow magazynowych
- `GET /api/inventory/product/{id}` - Stan dla produktu
- `GET /api/inventory/movements` - Historia ruchow magazynowych
- `GET /api/inventory/valuation` - Wycena magazynu

### Slowniki (Dictionary)
- `GET /api/dictionary/vat-rates` - Stawki VAT
- `GET /api/dictionary/units` - Jednostki miary
- `GET /api/dictionary/currencies` - Waluty
- `GET /api/dictionary/exchange-rates` - Kursy walut
- `GET /api/dictionary/payment-methods` - Formy platnosci
- `GET /api/dictionary/price-lists` - Cenniki
- `GET /api/dictionary/countries` - Kraje

### Raporty finansowe (Finance Reports)
- `GET /api/finance/cash-registers` - Stanowiska kasowe
- `GET /api/finance/cash-reports` - Raporty kasowe
- `GET /api/finance/bank-accounts` - Rachunki bankowe
- `GET /api/finance/bank-statements` - Wyciagi bankowe
- `GET /api/finance/aging-report` - Raport wiekowania

### System
- `GET /api/system/info` - Informacje o firmie
- `GET /api/system/operator` - Aktualny operator
- `GET /api/system/license` - Informacje o licencji

### Deklaracje (Declarations)
- `GET /api/declarations/vat` - Lista deklaracji VAT
- `GET /api/declarations/vat/{id}` - Szczegoly deklaracji
- `GET /api/declarations/types` - Typy deklaracji

### JPK
- `GET /api/jpk` - Lista plikow JPK
- `GET /api/jpk/{id}` - Szczegoly JPK
- `GET /api/jpk/types` - Typy JPK

### Konfiguracje
- `GET /api/configurations/categories` - Kategorie konfiguracji
- `GET /api/configurations/documents` - Konfiguracje dokumentow
- `GET /api/configurations/numbering` - Konfiguracje numeracji

### Raporty
- `GET /api/reports/types` - Typy raportow
- `GET /api/reports/sales/summary` - Podsumowanie sprzedazy
- `GET /api/reports/sales/by-product` - Sprzedaz wg produktow
- `GET /api/reports/sales/by-customer` - Sprzedaz wg klientow

### E-commerce
- `GET /api/ecommerce/integrations` - Integracje e-commerce
- `GET /api/ecommerce/offers` - Oferty internetowe
- `GET /api/ecommerce/packages` - Paczki wysylkowe

## Przyklady uzycia

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

### Utworzenie faktury sprzedazy

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

### Pobranie listy produktow z filtrowaniem

```bash
curl "http://localhost:5000/api/products?search=laptop&activeOnly=true&page=1&pageSize=20" \
  -H "X-API-Key: your-api-key"
```

## Struktura projektu

```
nexo-sfera-api/
├── src/
│   ├── Controllers/         # Kontrolery REST (18+)
│   ├── Services/            # Serwisy (Sfera)
│   ├── Models/
│   │   ├── Dto/             # Data Transfer Objects
│   │   ├── Requests/        # Modele requestow
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

## Wazne informacje techniczne

### Entity Framework 6 i .NET 8

SDK Nexo Sfera uzywa Entity Framework 6, ktory wymaga specjalnej obslugi w .NET 8:

1. **WindowsFormsSynchronizationContext** - wymagany do poprawnego dzialania EF6
2. **STA Thread** - operacje SDK musza byc wykonywane w watku STA
3. **ExecuteWithLockAsync** - wszystkie operacje tworzenia dokumentow uzywaja tego wrappera

### Wzorzec tworzenia dokumentow

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

## Licencja

Projekt wymaga waznej licencji InsERT Nexo z modulem Sfera.
