using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;

namespace KsefPluginy
{
    public class LinkDoSledzeniaPrzesylkiJakoDodatkowyOpis : IFunkcjaGenerowaniaDodatkowegoOpisuEFaktury
    {
        private Guid _id = new Guid("C64E8494-84CB-4A88-939E-C03ED43949EC");

        public string DomyslnyKlucz => "SLEDZENIEPRZESYLKI";

        public ZrodloFunkcjiGenerujacejDodatkowyOpis Zrodlo => ZrodloFunkcjiGenerujacejDodatkowyOpis.DlaNaglowkaDokumentu;

        public Guid Identyfikator => _id;

        public string Nazwa => "Link do śledzenia przesyłki";

        public string Opis => "Funkcja generująca link do śledzenia przesyłki jako dodatkowy opis";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public List<DodatkowyOpisEFaktury> Wygeneruj(string klucz
            , Dokument dokument
            , IEnumerable<IPozycjaDlaWierszaEFaktury> _) // można zignorować parametr wierszy gdyż plugin generuje dodatkowy opis dla całego dokumentu
        {
            List<DodatkowyOpisEFaktury> dane = new List<DodatkowyOpisEFaktury>();
            if (dokument.SposobDostawy != null
                && dokument.SposobDostawy.WidocznyNumerPrzesylki != (int)NumerPrzesylkiWidocznosc.Ukryj
                && !string.IsNullOrEmpty(dokument.NumerPrzesylki)
                && !string.IsNullOrEmpty(dokument.SposobDostawy.WzorAdresuSledzeniaPrzesylki))
            {
                dane.Add(new DodatkowyOpisEFaktury(klucz, dokument.SposobDostawy.WzorAdresuSledzeniaPrzesylki.Replace(DokumentExtensions.NrPrzesylkiPlaceholder, dokument.NumerPrzesylki)));
            }
            return dane;
        }
    }
}
