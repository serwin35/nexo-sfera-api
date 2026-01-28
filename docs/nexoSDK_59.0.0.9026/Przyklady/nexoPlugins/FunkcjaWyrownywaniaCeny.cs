using System;
using InsERT.Moria.CennikiICeny;
using InsERT.Moria.Rozszerzanie;

namespace NexoPlugins
{
    public class FunkcjaWyrownywaniaCenyDo99 : IFunkcjaWyrownywaniaCeny
    {
        public decimal WyrownajCene(decimal cena, int precyzja)
        {
            if (precyzja > 0)
            {
                decimal ulamek = (decimal)Math.Pow(10, -precyzja);
                decimal nowaCena = decimal.Ceiling(cena);
                if (nowaCena == cena)
                    nowaCena += 1m;
                nowaCena -= ulamek;
                return nowaCena;
            }
            return cena;
        }

        public InsERT.Moria.Finanse.Kwota WyrownajCene(InsERT.Moria.Finanse.Kwota kwota)
        {
            return new InsERT.Moria.Finanse.Kwota(WyrownajCene(kwota.Wartosc, (int)kwota.Waluta.Precyzja), kwota.Waluta);
        }

        public static readonly Guid Guid = new Guid("FD3AAB3F-E533-469C-910A-CC2053C4A5FF");

        public Guid Identyfikator
        {
            get { return Guid; }
        }

        public string Nazwa
        {
            get { return "Zakrąglanie w górę do \"99 groszy\"."; }
        }

        public string Opis
        {
            get { return "Zaokrągla w górę do niepełnej jednostki walutowej zgodnie z precyzją waluty."; }
        }

        public IDostawcaPluginow Dostawca
        {
            get { return new DostawcaPluginow(); }
        }
    }
}
