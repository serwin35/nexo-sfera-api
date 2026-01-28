# Przykładowy plugin integracji Sello NX z kurierem

Copyright © InsERT S.A. 2024

## Laboratorium

Uwaga! Aktualnie pluginy integracji Sello NX dla kurierów są dostępne w laboratorium nexo, co oznacza, że implementacja przedstawiona tutaj może różnić się od wersji finalnej. Wszelkie zmiany wprowadzane w kolejnych aktualizacja Sello NX mogą wymagać przebudowania pluginu, w tym konieczności dostosowania kodu do nowszej wersji.

## Opis pluginu

Plugin kurierski umożliwia stworzenie integracji Sello NX z dowolnym serwisem kurierskim posiadającym własne API. Dzięki podejściu pluginowemu, użytkownik programu otrzymuje dostęp do możliwości pracy z kurierem (nadawanie przesyłek) w jednolity sposób, ze spójnym interfejsem, obsługą błędów, konfiguracją konta integracji (połączenia) w programie i zarządzania nim oraz wysyłkami.

Twórca rozwiązania jest dzięki temu zwolniony z konieczności tworzenia własnego interfejsu graficznego programu, obsługi błędów, kolejkowania zadań synchronizacji. Sello NX dostarcza tutaj wiele gotowych mechanizmów a implementacja plugina integracji sprowadza się do zadeklarowania kilkunastu właściwości i implementacji metod komunikujących się z serwisem kurierskim i wymianie tych danych z Sello NX.


## Opis przykładu

Przykład przedstawia podstawową implementację pluginu integracji z serwisem kurierskim dla Sello NX.

 **Możliwości**

- Komunikacja z testowym środowiskiem serwisu InPost API ShipX
- Tworzenie przesyłki w systemie kuriera
- Pobranie etykiety z systemu kuriera
- Pobieranie statusu przesyłki z systemu kuriera
- Tworzenie konta integracji, stempla identyfikującego kuriera, walidacja danych dostępowych do API kuriera
- Udostępnianie usług kurierskich, usług dodatkowych w sposobie dostawy paczki w Sello NX



## Wymagania

- Sello NX 48.2

## Budowanie

Opis budowania pluginów dla systemu InsERT nexo opisany został [tutaj](../../../README.md).