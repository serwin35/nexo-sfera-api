using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Urzadzenia.Core;

namespace NexoPlugins
{
    public class EwaluatorWlasnychAutotekstowWydrukowNiefiskalnych_Pozycja : IEwaluatorAutotekstowWlasnychWydrukowNiefiskalnych
    {
        private readonly Guid _id;

        public EwaluatorWlasnychAutotekstowWydrukowNiefiskalnych_Pozycja()
        {
            _id = new Guid("F4039277-A816-46B6-BD79-3B0255128A4C");
        }

        public ISet<string> ObslugiwaneAutoteksty => new HashSet<string>() { "wartość rabatu netto", "wartość rabatu brutto", "wartość rabatu netto lub brutto" };

        public Guid Identyfikator => _id;

        public string Nazwa => "Własne pozycji";

        public string Opis => "Obsługuje dodatkowe autoteksty dla pozycji dokumentu.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string Ewaluuj(string wzorzec, IZawartoscWydrukuNiefiskalnego dane, out bool zakonczonaPowodzeniem)
        {
            if (dane.Dane is PozycjaDokumentu pozycja)
            {
                zakonczonaPowodzeniem = true;
                switch(wzorzec)
                {
                    case "wartosc rabatu netto":
                        return string.Format("{0:N" + pozycja.Dokument.Waluta.Precyzja + "}", WyliczWartoscRabatu(pozycja, true));
                    case "wartosc rabatu brutto":
                        return string.Format("{0:N" + pozycja.Dokument.Waluta.Precyzja + "}", WyliczWartoscRabatu(pozycja, false));
                    case "wartosc rabatu netto lub brutto":
                        return string.Format("{0:N" + pozycja.Dokument.Waluta.Precyzja + "}", WyliczWartoscRabatu(pozycja, pozycja.Dokument.WyliczenieVAT == (int)WyliczeniaVAT.Netto));
                }
            }
            zakonczonaPowodzeniem = false;
            return string.Empty;
        }

        private decimal WyliczWartoscRabatu(PozycjaDokumentu pozycja, bool netto)
        {
            decimal przedRabatem = netto ? pozycja.Wartosc.NettoPrzedRabatem : pozycja.Wartosc.BruttoPrzedRabatem;
            decimal poRabacie = netto ? pozycja.Wartosc.NettoPoRabacie : pozycja.Wartosc.BruttoPoRabacie;
            return (poRabacie - przedRabatem) * -1;
        }
    }
}
