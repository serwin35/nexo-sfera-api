# Stan na dzień dla dokumentów z datą wsteczną

Notatka z 19.11.2026: przegląd SDK 62.0.0.9537 i kodu mostu, tylko do odczytu. Dotyczy migracji ILUO → Subiekt
(dokumenty z lat 2024–2025). Liczby z hosta: patrz „Do sprawdzenia na hoście”.

## Wniosek

Nexo udostępnia stan na dzień. Kontrola stanu przed zapisem w moście (`StockValidationHelper`) patrzy na stan
**dzisiejszy**. Dla dokumentu z datą wsteczną powinna pytać `IMagazynier.IloscWolna(data, …)`. Zmiana tej kontroli
czeka na liczby z hosta; ten PR dodaje tylko odczyt (`GET /api/inventory/stock-at-date`).

## 1. Stan na dzień w SDK

- `InsERT.Moria.EgzekutorMagazynowy.IMagazynier.IloscWolna(DateTime?, Asortyment, Magazyn)`: „maksymalna ilość
  asortymentu, jaką można wydać z konkretnego magazynu, w danym dniu”. Data to data, „do której mają być uwzględniane
  przychody”. `null` oznacza „zestawienie na dowolny dzień”, a magazyn `null` wszystkie magazyny.
- `IMagazynier.IloscDostepna(DateTime?, Asortyment, Magazyn)`: „remanent na dzień bieżący/dowolny”. Uwzględnia
  przychody i rozchody do daty.
- Warianty pomocnicze:
  - `IloscWolna(DateTime?, Asortyment, Magazyn, Boolean zawszeDoliczPrzeterminowane)`;
  - przeciążenia dla `Partia` i `Przyjecie`.

  Przez te przeciążenia wywołanie przez `dynamic` może się związać źle albo wcale.
- `IMagazynier` daje typowany `Uchwyt.PodajObiektTypu<IMagazynier>()`. Przykłady SDK pobierają usługi tak samo
  (`PodajObiektTypu<IAsortymenty>()`, `PodajObiektTypu<IMagazyny>()`).
- Most sumował dotąd `StanyMagazynowe.IloscDostepna`. Schemat bazy mówi o tej tabeli: zagregowane stany
  „uwzględniają cały istniejący ruch magazynowy”, czyli stan na teraz.

## 2. Co robi Nexo przy wydaniu z datą wsteczną

- Rozchód dostaje datę dokumentu (`Dokument.DataWprowadzenia`). Most ustawia `DataWydaniaWystawienia` i
  `DataWprowadzenia` na `issueDate` dla RW, PW, FS i PA.
- Egzekutor pilnuje chronologii: „Data rozchodu musi być późniejsza niż data jego przychodu źródłowego”
  (`EgzekutorResources.DataRozchoduPrzedPrzychoduZrodlowegoErrorMessage`). Rozchód z dnia D nie weźmie dostawy
  przyjętej po D.
- **Na dzień D brak, dziś jest.**
  - Kontrola mostu przepuszcza FS/PA, bo patrzy na dziś.
  - Nexo nie sięgnie po późniejsze dostawy, a brak ląduje w `Braki` (`IBraki.PosiadaBraki`,
    `IBrakujacaPozycja.IloscBrakujaca`).
  - Czy `Zapisz()` wtedy odmawia, czy zapisuje dokument z brakiem: nie znalazłem w dokumentacji SDK.
- **Na dzień D był, dziś brak** (np. rezerwacje ZK obniżają `IloscDostepna`). Kontrola mostu odrzuca FS/PA, choć
  Nexo mógłby je wydać.
- **RW jest już inaczej.** `POST /api/warehouse-documents/rw` pomija dzisiejszą kontrolę dla dokumentu starszego niż
  7 dni („SDK will validate historical stock”) i zdaje się na `Zapisz()`. FS i PA sprawdzają zawsze stan dzisiejszy.

## 3. Wybór dostaw

- Każdy magazyn ma własną funkcję wyboru dostaw: `Magazyny.FunkcjaWyboruPrzychodow` (Guid `IFunkcjaWyboruPrzychodow`,
  standardowo `FunkcjeWyboruPrzychodow.FunkcjaWyboruPrzychodowFIFO_ID` albo `…LIFO_ID`).
- Razem z regułą chronologii daje to najstarsze wolne dostawy sprzed daty dokumentu.
- Którą funkcję ma MG, trzeba odczytać na hoście.

## Rekomendacja (c)

1. Dla daty wstecznej kontrola mostu sprawdza `IloscWolna(issueDate, …)`. Zostaje ten sam kod 400 i ten sam format
   komunikatu („Insufficient stock for 'SYM': requested N, available M”) z dopiskiem „na dzień D”, żeby parser
   migratora działał bez zmian. Dla dokumentu z dziś bez zmian.
2. Odczyt stanu na dzień dla kontroli wstępnej migratora: `GET /api/inventory/stock-at-date`, ten PR.
3. Walidacja Nexo przy `Zapisz()` zostaje ostatnim sędzią. Po #64 nie ma zapisów awaryjnych, więc jej odmowa to 400.

Wariant (b), czyli pominięcie kontroli i zdanie się na Nexo, odpada z dwóch powodów:
- nie wiadomo, co Nexo robi z brakiem;
- jego komunikat nie zostałby rozpoznany jako brak stanu.

## Otwarte punkty

- Czy dzień liczy się włącznie: `IloscWolna(D 00:00)` czy `IloscWolna(D 23:59:59)`, czy dokładna `DataWprowadzenia`.
  `stock-at-date` zwraca obie wartości.
- Czy `IloscWolna` odejmuje rezerwacje.
- Znaczenie `zawszeDoliczPrzeterminowane` dla towarów z partiami.

## Do sprawdzenia na hoście (tylko odczyt)

1. Dla 2–3 kart z luki porównać cztery wartości z `GET /api/inventory/stock-at-date?symbol=…&warehouse=MG&date=…`
   (np. `HXRPROD00021_0010`, `DMSUDMW03789_01` na 2024-06-11):
   - `iloscWolnaD`;
   - `iloscWolnaEndOfDay`;
   - `iloscDostepnaD`;
   - `iloscDostepnaToday`.
2. `SELECT FunkcjaWyboruPrzychodow FROM ModelDanychContainer.Magazyny WHERE Symbol = 'MG'` i porównanie z
   `FunkcjaWyboruPrzychodowFIFO_ID`.
3. Czy `Zapisz()` odmawia RW z brakiem na dzień D. Tylko na kopii bazy albo w firmie testowej Nexo, nigdy na
   produkcji.

## Przy okazji naprawione

`GET /api/inventory/free-quantity` wołał `IloscWolna(produkt, magazyn)` z dwoma argumentami. W SDK 62 takiego
przeciążenia nie ma: wyjątek był połykany, a endpoint po cichu zwracał dzisiejszą sumę `StanyMagazynowe`. Teraz woła
typowane `IloscWolna(DateTime?, Asortyment, Magazyn)`, a błąd to 500 bez podstawionej wartości.
