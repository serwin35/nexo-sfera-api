using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KsefPluginy
{
    public class MasaPozycjiJakoDodatkowyOpis : IFunkcjaGenerowaniaDodatkowegoOpisuEFaktury
    {
        private Guid _id = new Guid("56082C4D-832D-4E6E-AA78-CEA75FF16F4A");

        public string DomyslnyKlucz => "MASA";

        public ZrodloFunkcjiGenerujacejDodatkowyOpis Zrodlo => ZrodloFunkcjiGenerujacejDodatkowyOpis.DlaWierszyFaktury;

        public Guid Identyfikator => _id;

        public string Nazwa => "Masa pozycji";

        public string Opis => "Funkcja generująca masę pozycji dodatkowy opis";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public List<DodatkowyOpisEFaktury> Wygeneruj(string klucz
            , Dokument dokument
            , IEnumerable<IPozycjaDlaWierszaEFaktury> pozycje)
        {
            List<DodatkowyOpisEFaktury> dane = new List<DodatkowyOpisEFaktury>();
            foreach (IPozycjaDlaWierszaEFaktury pozycja in pozycje.Where(p => p.RodzajWiersza != RodzajWierszaEFaktury.PrzedKorekta))
            {
                if (pozycja.Pozycja.JednostkaMasy != null && pozycja.Pozycja.Masa.HasValue)
                {
                    dane.Add(new DodatkowyOpisEFaktury(klucz, pozycja.Pozycja.Masa.Value.ToString($"0.{new string(new char[pozycja.Pozycja.JednostkaMasy.Precyzja ?? 3]).Replace('\0', '0')}"), pozycja.NumerWiersza));
                }
            }
            return dane;
        }
    }
}
