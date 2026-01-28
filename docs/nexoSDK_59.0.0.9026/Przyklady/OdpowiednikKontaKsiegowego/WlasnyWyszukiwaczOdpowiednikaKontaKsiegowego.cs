// Przykład pokazuje jak znajdować odpowiedniki dla kont nie-kartotekowych oraz kont z podłączonymi kartotekami.
// Przykład oparty jest o *autentyczny* problem zgłoszony przez Klienta.
// Wymaga co najmniej nexo 41.0.0 (nie będzie się kompilować na wersjach wcześniejszych)

// Problem 1: Mapowanie kont z parametrów różnic kursowych (konta bez podłączonej kartoteki)
// rok 2021	   rok 2022
// 750-03-01   750-02
// 750-03-02   750-94
// 750-04-01   750-04-01
// 751-03-01   751-02
// 751-03-02   751-94
// 751-04-01   751-04-01

// Problem 2: Mapowanie kont z kartoteki klientów			
// rok 2021          rok 2022
// 203-02-01-00019   203-00444
// 201-02-01-00719   201-01903
// 201-02-01-01832   201-00539
// 201-02-01-01092   201-00550

using InsERT.Moria.KsiegowoscPelna;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Linq;

namespace OdpowiednikKontaKsiegowego
{
    public class WlasnyWyszukiwaczOdpowiednikaKontaKsiegowego : IFunkcjaZnajdowaniaOdpowiednikaKontaKsiegowego
    {
        private readonly Func<IKontaKsiegowe> kontaKsiegowe;
        private readonly Func<IDefinicjeKontaKsiegowego> definicjeKontaKsiegowego;
        private readonly Func<IElementyKartotekiKsiegowejZwykle> elementyKartotekiKsiegowejZwykle;
        private readonly Func<IFabrykaFunkcjiZnajdowaniaOdpowiednikaKontaKsiegowego> fabrykaZnajdowaczy;

        public Guid Identyfikator => new Guid("0760A0B2-F13F-48F6-B3DA-E054BDD39303");

        public string Nazwa => "Przykładowa wyszukiwarka";

        public string Opis => "Przykładowa wyszukiwarka odpowiednika konta księgowego";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public WlasnyWyszukiwaczOdpowiednikaKontaKsiegowego(
            Func<IKontaKsiegowe> kontaKsiegowe,
            Func<IDefinicjeKontaKsiegowego> definicjeKontaKsiegowego,
            Func<IElementyKartotekiKsiegowejZwykle> elementyKartotekiKsiegowejZwykle,
            Func<IFabrykaFunkcjiZnajdowaniaOdpowiednikaKontaKsiegowego> fabrykaZnajdowaczy // Musi być Func<>, bo inaczej mamy StackOverflowException
            )
        {
            this.kontaKsiegowe = kontaKsiegowe;
            this.definicjeKontaKsiegowego = definicjeKontaKsiegowego;
            this.elementyKartotekiKsiegowejZwykle = elementyKartotekiKsiegowejZwykle;
            this.fabrykaZnajdowaczy = fabrykaZnajdowaczy;
        }

        public KontoKsiegowe ZnajdzLubUtworzOdpowiednik(KontoKsiegowe zrodloweKontoKsiegowe, OkresObrachunkowy docelowyOkresObrachunkowy, bool przenoszenieKontMiedzyOkresami)
        {
            // Jeśli czegoś brakuje to trudno, nic na to nie poradzimy.
            if (zrodloweKontoKsiegowe == null || docelowyOkresObrachunkowy == null)
                return null;

            // Docelowy okres obrachunkowy: musi być 2022
            if (docelowyOkresObrachunkowy.DataPoczatkowa().Year == 2021)
            {
                var zrodlowyOkresObrachunkowy = zrodloweKontoKsiegowe.Definicja?.OkresObrachunkowy;
                if (zrodlowyOkresObrachunkowy == null)
                    return null;

                // Źródłowy okres obrachunkowy: musi być 2021
                if (zrodlowyOkresObrachunkowy.DataPoczatkowa().Year == 2021)
                {
                    // Tutaj rozwiązuję problem 1:
                    {
                        string docelowyNumerKonta = null;
                        switch (zrodloweKontoKsiegowe.Numer)
                        {
                            // Sztywna "mapa" numerów
                            case "750-03-01": docelowyNumerKonta = "750-02";  break;
                            case "750-03-02": docelowyNumerKonta = "750-94"; break;
                            case "750-04-01": docelowyNumerKonta = "750-04-01"; break;
                            case "751-03-01": docelowyNumerKonta = "751-02"; break;
                            case "751-03-02": docelowyNumerKonta = "751-94"; break;
                            case "751-04-01": docelowyNumerKonta = "751-04-01"; break;
                        }

                        if (!string.IsNullOrEmpty(docelowyNumerKonta))
                        {
                            // Znajdowanie konta nie-kartotekowego po numerze w docelowym okresie obrachunkowym
                            var konto = kontaKsiegowe().ZnajdzLubUtworz(docelowyOkresObrachunkowy, docelowyNumerKonta);

                            if (konto.Zmodyfikowany) // Jeśli zostało utworzone, to trzeba je zapisać przez przekazaniem "dalej"
                                konto.Zapisz();
                            return konto.Dane;
                        }
                    }

                    // Tutaj rozwiązuję problem 2:
                    {
                        // Jeśli źródłowe konto zaczyna się od "201-02-01" lub od "203-02-01"
                        if (zrodloweKontoKsiegowe.Numer.StartsWith("201-02-01") || zrodloweKontoKsiegowe.Numer.StartsWith("203-02-01"))
                        {
                            // Znajduję numer syntetyki (201 lub 203)
                            var numerSyntetyki = zrodloweKontoKsiegowe.Numer.Substring(0, 3);

                            // Znajduję definicję "liścia", czyli 201-Klienci lub 203-Klienci w docelowym okresie obrachunkowym
                            var definicjaKontaKsiegowegoWOkresieDocelowym = definicjeKontaKsiegowego().Dane
                                .PobierzZOkresuObrachunkowego(docelowyOkresObrachunkowy.Id)
                                .Where(dkk => dkk.KontoSyntetyczne.Numer == numerSyntetyki) 
                                .Where(dkk => dkk.KontoSyntetyczne_Id == dkk.DefinicjaNadrzedna_Id) // sztuczka: szukamy definicji 2-go poziomu
                                .Where(dkk => dkk.Kartoteka_Id != null)
                                .FirstOrDefault();

                            // Obsługa błędu na wypadek innego planu kont niż spodziewany
                            if (definicjaKontaKsiegowegoWOkresieDocelowym == null)
                                throw new InvalidOperationException($"Nie można było znaleźć definicji konta {numerSyntetyki}-... w okresie {docelowyOkresObrachunkowy.Nazwa} dla którego podpięta byłaby 1-poziomowa kartoteka.");

                            // Znajdowanie elementu kartoteki wyznaczającego konto w okresie źródłowym.
                            var ostatniElementKartotekiKontaZrodlowego = zrodloweKontoKsiegowe.ElementyKartotek.OrderByDescending(e => e.Kolejnosc).FirstOrDefault()?.ElementKartoteki;

                            // Obsługa błędu na wypadek innego planu kont niż spodziewany
                            if (ostatniElementKartotekiKontaZrodlowego == null)
                                throw new InvalidOperationException($"Nie udało się ustalić kontrahenta podłączonego do konta {zrodloweKontoKsiegowe.Numer} w okresie {zrodlowyOkresObrachunkowy.Nazwa}.");

                            IKontoKsiegowe doceloweKontoKsiegowe = null;

                            // Obsługa ścieżki, gdy na wejściu jest konto dla elementu "pozostałe" (....-99999)
                            if (ostatniElementKartotekiKontaZrodlowego is ElementKartotekiKsiegowejPozostale)
                            {
                                doceloweKontoKsiegowe = kontaKsiegowe().ZnajdzLubUtworz(definicjaKontaKsiegowegoWOkresieDocelowym, definicjaKontaKsiegowegoWOkresieDocelowym.Kartoteka.ElementPozostale);
                            }
                            // Obsługa ścieżki, gdy na wejściu jest konto dla elementu innego niż "pozostałe" (nie: ....-99999)
                            else if (ostatniElementKartotekiKontaZrodlowego is ElementKartotekiKsiegowejZwykly zwyklyElementKartotekiWZrodlowym)
                            {
                                // Znajdowanie klienta dla którego znaleźć w nowym okresie konto
                                var podmiot = zwyklyElementKartotekiWZrodlowym.ElementKartotekiZrodlowej?.Podmiot;

                                // Obsługa błędu na wypadek innego planu kont niż spodziewany
                                if (podmiot == null)
                                    throw new InvalidOperationException($"Nie udało się ustalić kontrahenta podłączonego do konta {zrodloweKontoKsiegowe.Numer} w okresie {zrodlowyOkresObrachunkowy.Nazwa}.");

                                // Znajdowanie (lub nadanie jeśli nie istnieje) numeru analityki w karotece w docelowym okresie obrachunkowym dla znalezionego wcześniej podmiotu
                                var elementKartotekiWOkresieDocelowym = elementyKartotekiKsiegowejZwykle().Utworz(
                                    definicjaKontaKsiegowegoWOkresieDocelowym.Kartoteka,
                                    podmiot,
                                    przenoszenieKontMiedzyOkresami);

                                // Obsługa błędu
                                if (elementKartotekiWOkresieDocelowym == null)
                                    throw new InvalidOperationException($"Nie udało się stworzyć elementu kartoteki dla kontrahenta {podmiot.NazwaSkrocona} w kartotece {definicjaKontaKsiegowegoWOkresieDocelowym.Kartoteka.Nazwa} w okresie {zrodlowyOkresObrachunkowy.Nazwa}.");

                                // Zapisanie numeru analityki jeśli został nadany
                                if (elementKartotekiWOkresieDocelowym.Zmodyfikowany)
                                    elementKartotekiWOkresieDocelowym.Zapisz();

                                // Utworzenie konta księgowego w docelowym okresie obrachunkowym
                                doceloweKontoKsiegowe = kontaKsiegowe().ZnajdzLubUtworz(definicjaKontaKsiegowegoWOkresieDocelowym, elementKartotekiWOkresieDocelowym.Dane);
                            }

                            // Jeśli docelowe konto zostało znalezione lub utworzone 
                            if (doceloweKontoKsiegowe != null)
                            {
                                if (doceloweKontoKsiegowe.Zmodyfikowany) // Jeśli zostało utworzone, to trzeba je zapisać przez przekazaniem "dalej"
                                    doceloweKontoKsiegowe.Zapisz();
                                return doceloweKontoKsiegowe.Dane;
                            }
                        }
                    }
                }
            }
            
            // Obsługa znajdowania odpowiednika w innych przypadkach niż założone (uruchamiamy wtedy domyślny nexowy mechanizm).
            return fabrykaZnajdowaczy().Domyslna.ZnajdzLubUtworzOdpowiednik(zrodloweKontoKsiegowe, docelowyOkresObrachunkowy, przenoszenieKontMiedzyOkresami);
        }
    }
}
