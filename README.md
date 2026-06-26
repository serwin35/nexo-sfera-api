<div align="center">

# 🔗 Nexo Sfera REST API

**Profesjonalne REST API dla systemu InsERT Nexo**
Subiekt · Rachmistrz · Rewizor · Gratyfikant · Gestor

[![.NET](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Swagger](https://img.shields.io/badge/Swagger-85EA2D?style=for-the-badge&logo=swagger&logoColor=black)](http://localhost:5000/swagger)
[![Windows](https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)](https://www.microsoft.com/windows)

Rozwijane przez **[DMservice](https://dmservice.pl)** | 📧 mateusz.serwinowski@dmservice.pl

</div>

---

> 🚀 **Swagger UI (pełny katalog endpointów):** http://localhost:5000/swagger
> 🔑 Autoryzacja kluczem API — patrz [Autentykacja](#-autentykacja)
> 📦 Wersja SDK: **nexo 61.0.0.9362** (`docs/nexoSDK_61.0.0.9362/`)

---

## 📋 Spis treści

- [Wymagania](#%EF%B8%8F-wymagania)
- [Konfiguracja](#-konfiguracja)
- [Uruchomienie](#-uruchomienie)
- [Autentykacja](#-autentykacja)
- [Multi-tenant (wiele firm)](#-multi-tenant-wiele-firm)
- [Konwencje API](#-konwencje-api)
- [Moduły API](#-moduły-api)
- [Integracja z Laravel](#-integracja-z-laravel)
- [Przykłady użycia](#-przykłady-użycia)
- [Architektura i uwagi techniczne](#-architektura-i-uwagi-techniczne)

---

## ⚙️ Wymagania

- Windows 10/11 lub Windows Server
- .NET 8.0 SDK
- Zainstalowany InsERT Nexo z licencją na **Sferę**
- SQL Server z bazą danych Nexo

## 🔧 Konfiguracja

Skopiuj `src/appsettings.template.json` do `src/appsettings.json` i uzupełnij:

```json
{
  "Sfera": {
    "Server": "(local)\\INSERTNEXO",
    "Database": "NazwaTwojejBazy",
    "UseWindowsAuth": true,
    "SqlLogin": null,
    "SqlPassword": null,
    "NexoLogin": "Szef",
    "NexoPassword": "",
    "Product": "Subiekt"
  }
}
```

| Parametr | Opis |
|----------|------|
| `Server` | Nazwa instancji SQL Server |
| `Database` | Nazwa bazy danych Nexo |
| `UseWindowsAuth` | `true` dla autentykacji Windows, `false` dla SQL |
| `SqlLogin` / `SqlPassword` | Dane logowania SQL (gdy `UseWindowsAuth = false`) |
| `NexoLogin` / `NexoPassword` | Operator Nexo (domyślny) |
| `Product` | Subiekt, Rachmistrz, Rewizor, Gratyfikant |
| `AutoSyncSdk` | Automatyczna synchronizacja DLL-i SDK przy starcie (domyślnie `true`) |
| `NexoInstallPath` | Ścieżka instalacji nexo (domyślnie wykrywana) |
| `PreferDeploymentBinaries` | Binarki z deploymentu nexo klienta (`%LocalAppData%\InsERT\Deployments\Nexo\*\Binaries`) nadpisują DLL-e z builda przy różnicy wersji — spójność z wersją bazy (domyślnie `true`) |
| `SdkFallbackUrl` / `SdkFallbackToken` | Zip z DLL-ami SDK pobierany przy starcie, gdy na maszynie nie ma nexo, a build nie zawierał DLL-i |

Kolejność źródeł DLL-i SDK przy starcie: **deployment nexo na maszynie klienta** (zawsze zgodny z wersją bazy) → instalacja w Program Files → zip z `SdkFallbackUrl`.

Wszystkie parametry można nadpisać zmiennymi środowiskowymi `SFERA_*` (np. `SFERA_SERVER`, `SFERA_NEXO_LOGIN`); klucz API — `API_KEY`, port — `API_PORT`.

> ⚠️ **Nie commituj `appsettings.json` z realnymi danymi.** Używaj zmiennych środowiskowych w produkcji.

## 🚀 Build i uruchomienie

> Build wymaga **Windows** (target `net8.0-windows`, WinForms, DLL-e SDK InsERT).

### Pierwsze uruchomienie (świeży klon)

```powershell
# 1. Wypełnij src\lib\nexo-sdk DLL-ami SDK (nie są śledzone w git)
#    Domyślnie: docs\nexoSDK_*\Bin, fallback: zainstalowane nexo
.\scripts\setup-sdk.ps1
#    własne źródło: .\scripts\setup-sdk.ps1 -SdkSourcePath "C:\Program Files (x86)\InsERT\nexo"

# 2. Konfiguracja
copy src\appsettings.template.json src\appsettings.json   # i uzupełnij dane
```

### Build i start

```powershell
dotnet build src\NexoSferaApi.csproj          # sam build
dotnet run --project src\NexoSferaApi.csproj  # build + start
```

- API: `http://localhost:5000` (port: env `API_PORT` albo `Kestrel:Endpoints:Http:Url`)
- Swagger UI: `http://localhost:5000/swagger`
- Specyfikacja OpenAPI: `http://localhost:5000/swagger/v1/swagger.json`

### Release / publish

```powershell
dotnet publish src\NexoSferaApi.csproj -c Release -o publish
.\publish\NexoSferaApi.exe
# albo pełny pakiet release:
.\scripts\build-release.ps1
```

### Przydatne warianty

```powershell
# DLL-e SDK z innej ścieżki (bez setup-sdk.ps1)
dotnet build src\NexoSferaApi.csproj -p:NexoSdkPath="C:\Program Files (x86)\InsERT\nexo"

# Tryb Development - aktywne endpointy debug/* i diagnostics
$env:ASPNETCORE_ENVIRONMENT="Development"; dotnet run --project src\NexoSferaApi.csproj
```

### Build w CI (GitHub Actions)

`appsettings.json` **nie jest potrzebny do builda** — konfiguracja jest wyłącznie runtime'owa (env vars `SFERA_*`, `API_KEY`, `ApiKeys__Keys__0__Key`… albo `appsettings.Production.json` obok exe). Gotowe workflow: `.github/workflows/build.yml` (windows-latest, SDK z prywatnego zipa przez secret `NEXO_SDK_URL`) oraz `build-selfhosted.yml` (runner z zainstalowanym nexo). Szczegóły: `docs/CI-CD-SETUP.md`.

## 🔐 Autentykacja

Każde żądanie (poza `GET /api/health` i `GET /api/settings/info`) wymaga klucza API. Obsługiwane formy (w kolejności preferencji):

```
Authorization: Bearer {klucz-api}     # preferowana
X-API-Key: {klucz-api}                # legacy
?api_key={klucz-api}                  # tylko do testów — klucz ląduje w logach!
```

Endpoint MCP (`/mcp`, integracja z agentami AI) podlega tej samej autoryzacji.

## 🏢 Multi-tenant (wiele firm)

Różne klucze API mogą pracować jako różni operatorzy Nexo — to umożliwia obsługę wielu firm/działów jedną instancją API:

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
        "Key": "klucz-api-default",
        "Name": "Klucz domyślny",
        "IsActive": true,
        "CompanyDescription": "Używa domyślnego operatora z sekcji Sfera"
      }
    ]
  }
}
```

| Parametr | Wymagany | Opis |
|----------|----------|------|
| `Key` | **Tak** | Unikalny klucz API |
| `Name` | **Tak** | Nazwa opisowa klucza |
| `IsActive` | **Tak** | Czy klucz jest aktywny |
| `NexoLogin` / `NexoPassword` | Nie | Operator Nexo dla tego klucza; brak = operator domyślny z sekcji `Sfera` |
| `Database` | Nie | **Osobna baza Nexo (= osobna firma) dla tego klucza**; brak = baza domyślna |
| `DefaultWarehouse` / `DefaultBranch` | Nie | Magazyn/oddział ustawiany w kontekście każdego żądania tym kluczem |
| `CompanyDescription` | Nie | Opis do celów logowania |

### Jak to działa

1. **Połączenie per baza** — `SferaConnectionPool` utrzymuje osobne połączenie SDK (z własnym wątkiem STA) dla każdej bazy z konfiguracji kluczy; połączenia powstają leniwie przy pierwszym żądaniu danym kluczem.
2. **Routing per żądanie** — `SferaServiceRouter` czyta claims klucza i kieruje **każdą** operację (wszystkie 61 kontrolerów, bez wyjątków) do właściwego połączenia.
3. **Operator i kontekst** — przed każdą operacją router wymusza operatora i magazyn/oddział klucza; klucz **bez** własnego operatora zawsze wraca do operatora domyślnego, więc kontekst jednej firmy nie przecieka do drugiej.
4. **Izolacja danych** — każdy operator widzi w Nexo tylko to, na co pozwalają jego uprawnienia; osobne bazy są odizolowane całkowicie.

Uwaga wydajnościowa: operacje **w ramach jednej bazy** wykonują się sekwencyjnie (jeden wątek STA na bazę), ale różne firmy/bazy pracują na osobnych wątkach — równolegle. Pierwsze żądanie do nowej bazy płaci koszt nawiązania połączenia (kilka sekund).

### Bezpieczeństwo

- Przechowuj klucze API w zmiennych środowiskowych / sejfie, rotuj regularnie
- Używaj HTTPS w produkcji
- Nadawaj operatorom Nexo minimalne wymagane uprawnienia

## 📐 Konwencje API

### Koperta odpowiedzi

Odpowiedzi pojedyncze (`ApiResponse<T>`):

```json
{ "success": true, "data": { ... }, "message": null, "errors": null }
```

Odpowiedzi listowe (`PagedResponse<T>`):

```json
{
  "success": true,
  "data": [ ... ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 1234,
  "totalPages": 25,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

Błędy: `success: false` + `message`/`errors`, status HTTP 400/404/500. (Część starszych endpointów zwraca jeszcze surowe obiekty — trwa ujednolicanie.)

### Paginacja i filtrowanie

Endpointy listowe przyjmują `?page=1&pageSize=50` oraz filtry specyficzne dla zasobu (np. `search`, `activeOnly`, zakresy dat) — szczegóły w Swaggerze.

### Współbieżność — ważne dla integratora

SDK Sfery działa na **jednym dedykowanym wątku STA** — wszystkie operacje SDK wykonują się **sekwencyjnie**. Konsekwencje:

- Równoległe żądania są kolejkowane, nie wykonują się jednocześnie
- Długa operacja (np. generowanie JPK) opóźnia kolejne żądania
- Po stronie klienta ustaw rozsądne timeouty i unikaj agresywnego fan-outu

### Diagnostyka połączenia

- `GET /api/health` — status API (anonimowy)
- `GET /api/health/sfera` — status połączenia ze Sferą
- `POST /api/health/reconnect` — pełna rekonekcja (dispose + connect na wątku STA)

## 📡 Moduły API

Pełny, zawsze aktualny katalog **354 endpointów** znajdziesz w **Swagger UI** (`/swagger`). Przegląd modułów:

| Domena | Moduły (route prefix) |
|--------|----------------------|
| **Sprzedaż i zakupy** | `documents` (FS/FZ/PA/korekty/zaliczki/marża), `customer-orders` (ZK), `orders` (ZD), `offers` (oferty Gestor), `promotions`, `discounts` |
| **Magazyn** | `warehouse-documents` (WZ/PZ/RW/PW/MM), `warehouses`, `inventory` (stany, ruchy, wycena), `assembly` (kompletacja ZM), `remainders` (remanenty) |
| **Kartoteki** | `customers` + `contractor-groups`, `products` + `product-groups/-attributes/-templates/-symbols`, `photo-gallery`, `custom-fields` |
| **Finanse** | `payments` (KP/KW/BP/BW, rozrachunki), `finance-reports` (kasy, rachunki, wyciągi, wiekowanie), `vat-registry`, `accruals` (RMK) |
| **Księgowość** | `booking-documents` (dekretacja), `accounting-import`, `internal-documents` (DW), `financial-statements`, `archives` |
| **Podatki i deklaracje** | `jpk`, `ksef` (e-faktury), `declarations` (VAT/ZUS), `intrastat` |
| **Kadry i płace** | `employees`, `contracts`, `payroll` (listy płac), `absences`, `ppk` |
| **Majątek** | `fixed-assets` (środki trwałe), `fleet` (pojazdy) |
| **CRM i serwis** | `activities` (działania CRM), `service-orders` (zlecenia serwisowe), `comments`, `calendars`, `attachments` |
| **E-commerce i logistyka** | `ecommerce` (integracje, oferty internetowe, paczki), `couriers` |
| **Słowniki i konfiguracja** | `dictionary` (VAT, jm, waluty, kursy, formy płatności, kraje), `configurations`, `system-parameters`, `settings`, `organization`, `permissions`, `devices`, `print` |
| **System** | `health`, `system` (firma, operator, licencja), `audit-trail`, `reports`, `office-clients`, `diagnostics` (tylko do debugowania) |

## 🐘 Integracja z Laravel

```php
// config/services.php
'nexo' => [
    'base_url' => env('NEXO_API_URL', 'http://nexo-host:5000'),
    'api_key'  => env('NEXO_API_KEY'),
],
```

```php
use Illuminate\Support\Facades\Http;

$nexo = Http::baseUrl(config('services.nexo.base_url'))
    ->withToken(config('services.nexo.api_key'))   // Authorization: Bearer
    ->timeout(120)                                  // operacje SDK są sekwencyjne!
    ->acceptJson();

// Lista produktów
$products = $nexo->get('/api/products', ['search' => 'laptop', 'page' => 1, 'pageSize' => 20])
    ->json('data');

// Faktura sprzedaży
$invoice = $nexo->post('/api/documents/sales-invoice', [
    'customerNIP' => '1234567890',
    'warehouseSymbol' => 'MAG',
    'items' => [
        ['productSymbol' => 'TOWAR001', 'quantity' => 5, 'priceNet' => 100.00],
    ],
])->json();

if (! $invoice['success']) {
    Log::error('Nexo API error', $invoice['errors'] ?? []);
}
```

## 💡 Przykłady użycia

### Utworzenie kontrahenta

```bash
curl -X POST http://localhost:5000/api/customers \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer your-api-key" \
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
  -H "Authorization: Bearer your-api-key" \
  -d '{
    "type": 1,
    "customerNIP": "1234567890",
    "warehouseSymbol": "MAG",
    "items": [
      { "productSymbol": "TOWAR001", "quantity": 5, "priceNet": 100.00 }
    ]
  }'
```

### Utworzenie paragonu

```bash
curl -X POST http://localhost:5000/api/documents/receipt \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer your-api-key" \
  -d '{
    "warehouseSymbol": "MAG",
    "items": [
      { "productSymbol": "TOWAR001", "quantity": 2, "priceGross": 123.00 }
    ]
  }'
```

### Dokumenty magazynowe (WZ / PZ / RW / PW / MM)

```bash
curl -X POST http://localhost:5000/api/warehouse-documents/pw \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer your-api-key" \
  -d '{
    "warehouseSymbol": "MG",
    "issueDate": "2026-05-06T00:00:00",
    "notes": "Import z systemu zewnetrznego",
    "items": [
      { "productSymbol": "TOWAR001", "quantity": 4.0, "unit": "szt.", "priceNet": 16.05 },
      { "productEan": "5901234123457", "quantity": 10.0, "priceNet": 25.00 }
    ]
  }'
```

Dla MM dodaj `targetWarehouseSymbol`. Dla RW ceny mogą być ignorowane — system wycenia rozchód wg metody FIFO/LIFO/średniej ważonej skonfigurowanej w Nexo.

### Powiązanie dokumentów (np. RW z PW)

```bash
curl -X POST http://localhost:5000/api/warehouse-documents/109622/associate \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer your-api-key" \
  -d '{ "targetDocumentId": 109623, "relationType": "related" }'
```

### Operacja kasowa KP

```bash
curl -X POST http://localhost:5000/api/payments/cash/kp \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer your-api-key" \
  -d '{
    "amount": 1000.00,
    "description": "Wplata gotowki",
    "customerNIP": "1234567890",
    "cashRegisterSymbol": "KASA1"
  }'
```

## 🔬 Architektura i uwagi techniczne

### Struktura projektu

```
nexo-sfera-api/
├── src/
│   ├── Controllers/         # 61 kontrolerów REST
│   ├── Services/            # SferaService (wątek STA), synchronizator SDK
│   ├── Models/              # Dto / Requests / Responses
│   ├── Helpers/             # DynamicPropertyHelper, walidacja stanów
│   ├── Authentication/      # API Key auth (Bearer / X-API-Key)
│   ├── Configuration/       # SferaSettings, ApiKeySettings
│   ├── Middleware/          # Obsługa błędów
│   ├── Mcp/                 # Narzędzia MCP dla agentów AI (patrz MCP.md)
│   └── Program.cs
├── lib/nexo-sdk/            # DLL-e SDK Nexo
├── docs/
│   ├── nexoSDK_61.0.0.9362/ # Oficjalna dokumentacja SDK (nieśledzona w git)
│   └── AUDIT-2026-06-12.md  # Audyt jakości i mapa pokrycia SDK
└── scripts/                 # Skrypty deploy/diagnostyka
```

### Entity Framework 6, STA i .NET 8

SDK Nexo Sfera używa EF6 i był projektowany pod aplikacje desktopowe:

1. **Dedykowany wątek STA** — wszystkie operacje SDK wykonuje jeden wątek z `WindowsFormsSynchronizationContext` (wymóg EF6)
2. **`ExecuteWithLockAsync`** — każdy dostęp do SDK z kontrolera musi przejść przez ten wrapper (serializacja pracy na wątku STA); obiekty `dynamic` SDK **nie mogą opuścić** lambdy
3. **Rekonekcja** — `POST /api/health/reconnect` bezpiecznie ubija i odtwarza połączenie na wątku STA

### Wzorzec tworzenia dokumentów (SDK)

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

### Walidacja stanów magazynowych dla usług

API pomija walidację stanów dla produktów typu **usługa** (`Rodzaj_Id = 1`) — usługi można dodawać do dokumentów sprzedaży bez sprawdzania stanów. Walidacja działa normalnie dla towarów (0), kompletów (2) i materiałów (3).

### Statusy dokumentów w odpowiedziach

```json
{ "id": 12345, "number": "FS/2026/01/123", "statusId": 3, "status": "Zrealizowano", "statusSymbol": "ZREAL" }
```

### Pomijanie uwag importowych

Pole `notes` jest **ignorowane** (i logowane) przy tworzeniu FS, FZ, PA, korekt, faktur zaliczkowych i VAT marża; działa normalnie dla dokumentów magazynowych i zamówień.

## 📄 Licencja

Projekt wymaga ważnej licencji InsERT Nexo z modułem Sfera. Licencja komercyjna DMservice.

---

⭐ Jeśli projekt jest przydatny, zostaw gwiazdkę!
