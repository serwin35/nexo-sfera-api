using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KsefPluginy
{
    public class SkladnikiKompletuJakoDodatkowyOpis : IFunkcjaGenerowaniaDodatkowegoOpisuEFaktury
    {
        private Guid _id = new Guid("17EE675E-5E8D-4405-A86D-0B6E4045DEAC");

        public string DomyslnyKlucz => "SKLADNIKKOMPLETU";

        public ZrodloFunkcjiGenerujacejDodatkowyOpis Zrodlo => ZrodloFunkcjiGenerujacejDodatkowyOpis.DlaWierszyFaktury;

        public Guid Identyfikator => _id;

        public string Nazwa => "Składniki kompletu";

        public string Opis => "Funkcja generująca składniki kompletu jako dodatkowy opis";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public List<DodatkowyOpisEFaktury> Wygeneruj(string klucz, Dokument dokument, IEnumerable<IPozycjaDlaWierszaEFaktury> pozycje)
        {
            List<DodatkowyOpisEFaktury> dane = new List<DodatkowyOpisEFaktury>();
            foreach (IPozycjaDlaWierszaEFaktury pozycja in pozycje
                .Where(p => p.RodzajWiersza != RodzajWierszaEFaktury.PrzedKorekta // nie generujemy dla wierszy "przed korektą"
                            && p.Pozycja.AsortymentAktualny != null // tylko dla pozycji asortymentów kartotekowych
                            && p.Pozycja.AsortymentAktualny.Rodzaj.CzyKomplet())) // generujemy tylko dla pozycji asortymentów będących kompletem
            {
                foreach (SkladnikKompletu skladnik in pozycja.Pozycja.AsortymentAktualny.SkladnikiKompletu)
                {
                    string format = "0." + new string(new char[skladnik.JednostkaMiaryAsortymentu.PrecyzjaWlasciwa() ?? 3]).Replace('\0', '0');
                    dane.Add(new DodatkowyOpisEFaktury(klucz, $"{skladnik.Skladnik.Symbol};{skladnik.Ilosc.ToString(format)};{skladnik.JednostkaMiaryAsortymentu.Symbol()}", pozycja.NumerWiersza));
                }
            }
            return dane;
        }

    }
}
