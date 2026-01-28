using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KsefPluginy
{
    /// <summary>
    /// Funkcja dla każdej pozycji dokumentu powiązanej z asortymentem będącym wariantem modelu wpisuje w sekcji DodatkowyOpis cechy wybranego wariantu
    /// w postaci NAZWAWŁAŚCIWOŚCI1:CECHA1;NAZWAWŁAŚCIWOŚCI2:CECHA2...
    /// </summary>
    public class CechyModeluJakoDodatkowyOpis : IFunkcjaGenerowaniaDodatkowegoOpisuEFaktury
    {
        private Guid _id = new Guid("2F47355F-DE07-4293-BBE2-B048F14B33A9");

        public string DomyslnyKlucz => "MODEL";

        public ZrodloFunkcjiGenerujacejDodatkowyOpis Zrodlo => ZrodloFunkcjiGenerujacejDodatkowyOpis.DlaWierszyFaktury;

        public Guid Identyfikator => _id;

        public string Nazwa => "Cechy modelu";

        public string Opis => "Funkcja generująca cechy modelu jako dodatkowy opis";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public List<DodatkowyOpisEFaktury> Wygeneruj(string klucz, Dokument dokument, IEnumerable<IPozycjaDlaWierszaEFaktury> pozycje)
        {
            List<DodatkowyOpisEFaktury> dane = new List<DodatkowyOpisEFaktury>();
            foreach (IPozycjaDlaWierszaEFaktury pozycja in pozycje
                .Where(p => p.RodzajWiersza != RodzajWierszaEFaktury.PrzedKorekta // nie generujemy dla wierszy "przed korektą"
                            && p.Pozycja.AsortymentAktualny != null // tylko dla pozycji asortymentów kartotekowych
                            && p.Pozycja.AsortymentAktualny?.Model != null)) // generujemy tylko dla pozycji asortymentów będących wariantem modelu
            {
                string wlasciwosci = string.Join(";", pozycja.Pozycja.AsortymentAktualny.WlasciwosciAsortymentu.Select(w => $"{w.Wlasciwosc.Nazwa}:{string.Join(",", w.CechyAsortymentu.Select(c => c.Nazwa))}"));
                if (string.IsNullOrEmpty(wlasciwosci)) // tekst w polu Wartosc nie może być pusty
                    wlasciwosci = "BRAK";
                dane.Add(new DodatkowyOpisEFaktury(klucz, wlasciwosci, pozycja.NumerWiersza));
            }
            return dane;
        }
    }
}
