# Nexo Sfera API - MCP Server

Serwer MCP (Model Context Protocol) wbudowany w Nexo Sfera API umożliwia agentom AI (Claude, Cursor, Windsurf, itp.) bezpośrednie zarządzanie systemem ERP InsERT Nexo.

## Szybki start

1. Uruchom Nexo Sfera API na serwerze Windows z zainstalowanym InsERT Nexo
2. Skonfiguruj swojego klienta AI (instrukcje poniżej)
3. Gotowe - agent AI ma dostęp do 18 narzędzi ERP

## Endpoint

```
http://<adres-serwera>:5000/sse
```

Domyślnie API nasłuchuje na porcie `5000` (konfigurowalne w `appsettings.json`).

Transport: **HTTP + Server-Sent Events (SSE)** - standard MCP.

## Konfiguracja klientów AI

### Claude Desktop

Plik: `%APPDATA%\Claude\claude_desktop_config.json` (Windows) lub `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS)

```json
{
  "mcpServers": {
    "nexo-erp": {
      "url": "http://localhost:5000/sse"
    }
  }
}
```

### Claude Code (CLI)

Plik `.mcp.json` w katalogu projektu (już dołączony w repo):

```json
{
  "mcpServers": {
    "nexo-erp": {
      "type": "sse",
      "url": "http://localhost:5000/sse"
    }
  }
}
```

### Cursor

Settings > MCP Servers > Add:
- Name: `nexo-erp`
- Transport: `sse`
- URL: `http://localhost:5000/sse`

### Windsurf / Continue.dev / inne

Większość klientów MCP wspiera transport SSE. Podaj URL: `http://localhost:5000/sse`

### MCP Inspector (testowanie)

```bash
npx @modelcontextprotocol/inspector
```

Wpisz URL: `http://localhost:5000/sse` - zobaczysz listę 18 dostępnych narzędzi.

## Dostępne narzędzia (18)

### System (2 tools)

| Narzędzie | Opis | Parametry |
|-----------|------|-----------|
| `GetSystemInfo` | Status połączenia, operator, kontekst (magazyn, oddział) | brak |
| `ListManagers` | Lista 27 dostępnych managerów SDK z opisami | brak |

### Kontrahenci (3 tools)

| Narzędzie | Opis | Parametry |
|-----------|------|-----------|
| `SearchCustomers` | Szukaj kontrahentów po nazwie, NIP lub symbolu | `query` (wymagany), `limit` (domyślnie 10) |
| `GetCustomerDetails` | Pełne dane kontrahenta z adresem i kontaktami | `customerId` lub `nip` |
| `CreateCustomer` | Utwórz nowego kontrahenta | `shortName` (wymagany), `nip?`, `type?` (Firma/Osoba), `email?`, `phone?`, `city?`, `street?`, `postalCode?` |

### Produkty (3 tools)

| Narzędzie | Opis | Parametry |
|-----------|------|-----------|
| `SearchProducts` | Szukaj produktów po nazwie, symbolu lub EAN | `query` (wymagany), `activeOnly` (domyślnie true), `limit` (domyślnie 10) |
| `GetProductDetails` | Pełne dane produktu z cenami i stanami magazynowymi | `productId` lub `symbol` |
| `CheckStock` | Stan magazynowy produktu we wszystkich magazynach | `productSymbol` lub `productId`, `warehouseSymbol?` |

### Dokumenty (5 tools)

| Narzędzie | Opis | Parametry |
|-----------|------|-----------|
| `SearchDocuments` | Szukaj dokumentów z filtrami | `type?` (FS/FZ/PA/ZK/ZD), `customerName?`, `dateFrom?`, `dateTo?`, `unpaidOnly?`, `limit` (domyślnie 20) |
| `GetDocumentDetails` | Pełne dane dokumentu z pozycjami i płatnościami | `documentId` (wymagany), `type?` |
| `CreateSalesInvoice` | Wystaw fakturę sprzedaży (FS) | `customerId` lub `customerNip`, `items` (JSON), `paymentMethod?`, `notes?` |
| `CreateReceipt` | Wystaw paragon (PA) | `items` (JSON), `paymentMethod?` |
| `CreateCustomerOrder` | Utwórz zamówienie od klienta (ZK) | `customerId` lub `customerNip`, `items` (JSON), `notes?` |

**Format pozycji (`items`):**
```json
[
  {"productSymbol": "LAPTOP-01", "quantity": 2, "priceNet": 3500.00},
  {"productSymbol": "MYSZ-USB",  "quantity": 5}
]
```
- `productSymbol` - symbol produktu w Nexo (wymagany)
- `quantity` - ilość (domyślnie 1)
- `priceNet` - cena netto (opcjonalnie, jeśli pominięte - użyje ceny z cennika)

### Magazyn (2 tools)

| Narzędzie | Opis | Parametry |
|-----------|------|-----------|
| `GetStockLevels` | Stany magazynowe z filtrami | `warehouseSymbol?`, `productSymbol?`, `lowStockOnly?`, `limit` (domyślnie 50) |
| `GetWarehouseDocuments` | Dokumenty magazynowe | `type?` (WZ/PZ/MM/RW/PW), `dateFrom?`, `limit` (domyślnie 20) |

### Finanse (3 tools)

| Narzędzie | Opis | Parametry |
|-----------|------|-----------|
| `GetUnpaidDocuments` | Nieopłacone/przeterminowane faktury | `customerName?`, `customerNip?`, `overdueOnly?`, `limit` (domyślnie 20) |
| `GetSettlements` | Rozrachunki (należności/zobowiązania) | `customerName?`, `type?` (receivable/payable), `limit` (domyślnie 20) |
| `GetFinanceSummary` | Podsumowanie: należności, zobowiązania, przeterminowane | brak |

## Przykłady użycia z agentem AI

Po podłączeniu MCP możesz pisać do agenta naturalnym językiem:

```
"Pokaż mi 5 ostatnich faktur sprzedaży"
→ Agent wywoła SearchDocuments(type="FS", limit=5)

"Jaki jest stan magazynowy laptopów?"
→ Agent wywoła SearchProducts(query="laptop") + CheckStock(productSymbol="...")

"Wystaw fakturę dla klienta NIP 5261234567 na 10 szt. LAPTOP-01 po 3500 zł netto"
→ Agent wywoła CreateSalesInvoice(customerNip="5261234567", items=[...])

"Kto ma przeterminowane płatności?"
→ Agent wywoła GetUnpaidDocuments(overdueOnly=true)

"Pokaż podsumowanie finansowe firmy"
→ Agent wywoła GetFinanceSummary()

"Znajdź kontrahenta Jan Kowalski i pokaż jego dane"
→ Agent wywoła SearchCustomers(query="Jan Kowalski") + GetCustomerDetails(customerId=...)
```

## Format odpowiedzi

Wszystkie narzędzia zwracają JSON w formacie:

```json
{
  "success": true,
  "message": "Found 5 customers matching 'kowalski'",
  "data": [ ... ]
}
```

W przypadku błędu:
```json
{
  "success": false,
  "error": "Customer not found",
  "detail": "optional error details"
}
```

## Wymagania

- Windows Server z zainstalowanym InsERT Nexo (Subiekt/Rachmistrz/Rewizor)
- .NET 8 Runtime
- Dostęp sieciowy do serwera SQL z bazą Nexo
- Nexo Sfera API uruchomione i połączone z bazą danych

## Rozwiązywanie problemów

**Agent nie widzi narzędzi:**
- Sprawdź czy API jest uruchomione: `curl http://localhost:5000/api/health`
- Sprawdź logi API - MCP server loguje się przy starcie

**Narzędzia zwracają błędy "manager unavailable":**
- Sprawdź status Sfery: `curl http://localhost:5000/api/health/deep`
- Możliwe że Sfera nie jest połączona z bazą - sprawdź `appsettings.json`

**Timeout przy operacjach:**
- SDK Nexo działa na dedykowanym wątku STA - tylko jedna operacja na raz
- Długie operacje (np. skanowanie wszystkich dokumentów) mogą trwać kilka sekund

## REST API

Oprócz MCP, API udostępnia tradycyjne endpointy REST na porcie 5000:
- Swagger UI: `http://localhost:5000/swagger`
- Health check: `http://localhost:5000/api/health`
- 250+ endpointów REST w 32 kontrolerach
