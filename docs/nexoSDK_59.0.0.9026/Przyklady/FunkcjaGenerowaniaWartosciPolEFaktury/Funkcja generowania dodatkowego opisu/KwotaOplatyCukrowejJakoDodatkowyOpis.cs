using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InsERT.Moria.Asortymenty;

namespace KsefPluginy
{
    public class KwotaOplatyCukrowejJakoDodatkowyOpis : IFunkcjaGenerowaniaDodatkowegoOpisuEFaktury
    {
        private readonly IKalkulatorOplatyCukrowej _kalkulatorOplatyCukrowej;

        public KwotaOplatyCukrowejJakoDodatkowyOpis(
            IKalkulatorOplatyCukrowej kalkulatorOplatyCukrowej)
        {
            _kalkulatorOplatyCukrowej = kalkulatorOplatyCukrowej;
        }

        private Guid _id = new Guid("B0339BC4-95C6-4472-B13F-83C45BEA35DA");

        public string DomyslnyKlucz => "OPLATACUKROWA";

        public ZrodloFunkcjiGenerujacejDodatkowyOpis Zrodlo => ZrodloFunkcjiGenerujacejDodatkowyOpis.DlaNaglowkaDokumentu;

        public Guid Identyfikator => _id;

        public string Nazwa => "Kwota opłaty cukrowej i kofeinowej";

        public string Opis => "Funkcja generująca kwotę opłaty cukrowej i kofeinowej jako dodatkowy opis";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public List<DodatkowyOpisEFaktury> Wygeneruj(string klucz
            , Dokument dokument
            , IEnumerable<IPozycjaDlaWierszaEFaktury> _) // można zignorować parametr wierszy gdyż plugin generuje dodatkowy opis dla całego dokumentu
        {
            List<DodatkowyOpisEFaktury> dane = new List<DodatkowyOpisEFaktury>();
            decimal kwota = dokument.WyliczKwoteOplatyCukrowej(_kalkulatorOplatyCukrowej) + dokument.WyliczKwoteOplatyKofeinowej(_kalkulatorOplatyCukrowej);
            if (kwota != 0m)
            {
                dane.Add(new DodatkowyOpisEFaktury(klucz, kwota.ToString("0.00")));
            }
            return dane;
        }
    }
}
