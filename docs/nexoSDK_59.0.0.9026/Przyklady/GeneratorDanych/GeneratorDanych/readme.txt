Pomiar czasu w tej aplikacji jest zrealizowany za pomocą klasy Stopwatch i może nie być dokładny. 
W celu uzyskania najsensowniejszych wyników zaleca się:
* kompilację w konfiguracji Release,
* uruchomienie aplikacji bez debuggera.
Kolejne uruchomienia aplikacji mogą dawać różne wyniki czasowe. 

Dokładniejszym sposobem pomiaru czasu jest użycie biblioteki BenchmarkDotNet, która pozwala na wielokrotne uruchomienie testów i uśrednienie wyników.