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
| `Product` | Produkt: Subiekt, Rachmistrz, Rewizor, Gratyfikant |

## Uruchomienie

```bash
cd src
dotnet run
```

API bedzie dostepne pod adresem: `http://localhost:5000`
Swagger UI: `http://localhost:5000/swagger`

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

### Dokumenty (Documents)
- `GET /api/documents` - Lista dokumentow
- `GET /api/documents/{id}` - Dokument po ID
- `GET /api/documents/by-number/{number}` - Dokument po numerze
- `POST /api/documents/sales-invoice` - Utworz fakture sprzedazy
- `POST /api/documents/customer-order` - Utworz zamowienie od klienta
- `POST /api/documents/purchase-invoice` - Utworz fakture zakupu

### Magazyny (Warehouses)
- `GET /api/warehouses` - Lista magazynow
- `GET /api/warehouses/{symbol}` - Magazyn po symbolu
- `GET /api/warehouses/stock/{productSymbol}` - Stan magazynowy produktu

## Przyklady uzycia

### Utworzenie kontrahenta

```bash
curl -X POST http://localhost:5000/api/customers \
  -H "Content-Type: application/json" \
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
  -d '{
    "type": 1,
    "customerNIP": "1234567890",
    "warehouseSymbol": "MAG",
    "notes": "Faktura testowa",
    "items": [
      {
        "productSymbol": "TOWAR001",
        "quantity": 5,
        "priceNet": 100.00
      }
    ]
  }'
```

### Pobranie listy produktow z filtrowaniem

```bash
curl "http://localhost:5000/api/products?search=laptop&activeOnly=true&page=1&pageSize=20"
```

## Struktura projektu

```
nexo-sfera-api/
├── src/
│   ├── Controllers/         # Kontrolery REST
│   ├── Services/            # Serwisy (Sfera)
│   ├── Models/
│   │   ├── Dto/             # Data Transfer Objects
│   │   ├── Requests/        # Modele requestow
│   │   └── Responses/       # Modele odpowiedzi
│   ├── Configuration/       # Konfiguracja
│   ├── Program.cs           # Entry point
│   └── appsettings.json     # Konfiguracja
├── nexoSDK_58.0.2.8985/     # SDK Nexo Sfera
└── README.md
```

## Licencja

Projekt wymaga waznej licencji InsERT Nexo z modulem Sfera.
