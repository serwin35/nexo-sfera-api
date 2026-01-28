using InsERT.Moria.Bank;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BankowoscElektronicznaPlugins
{
    public class FunkcjaEksportuDyspozycjiBankowychZKodowaniemV2_Przyklad : IFunkcjaEksportuDyspozycjiBankowychZKodowaniemV2
    {
        public Guid Identyfikator { get; set; } = Guid.Parse("BFBDD4B1-4B6B-4714-B463-975ACED557B0");

        public string Nazwa { get { return "Przykładowy eksport"; } }

        public string Opis { get { return "Przykładowa funkcja eksportu dyspozycji bankowych"; } }

        public IDostawcaPluginow Dostawca { get { return new DostawcaPluginow(); } }

        public Encoding Kodowanie { get; set; } = Encoding.GetEncoding(1250);

        public string[] NumeryInstytucji => new string[] { "8811", "8882" };

        public Encoding DomyslneKodowanie => Kodowanie;

        public void Generuj(IEnumerable<DyspozycjaBankowa> dyspozycje, string sciezkaDoPliku, OpcjeEksportuDyspozycjiBankowych opcjaZapisu)
        {
            if (dyspozycje == null)
                throw new ArgumentNullException(nameof(dyspozycje));
            if (String.IsNullOrEmpty(sciezkaDoPliku))
                throw new ArgumentNullException(nameof(sciezkaDoPliku));

            var linie = dyspozycje.Select(d => string.Concat(string.Join(",",
                    "120",
                    d.DataZlozenia.ToString("yyyyMMdd"),
                    d.Kwota.ToString().Replace(",", string.Empty),
                    string.Concat("\"", d.RachunekNadawcy.NumerBezSeparatorow, "\""),
                    string.Concat("\"", d.RachunekOdbiorcy.NumerBezSeparatorow, "\""),
                    string.Concat("\"", d.Nadawca.NazwaSkrocona ?? string.Empty, "\""),
                    string.Concat("\"", d.Odbiorca.NazwaSkrocona ?? string.Empty, "\""),
                    string.Concat("\"", d.Tytulem ?? string.Empty, "\""),
                    "", "", "51"
               ), Environment.NewLine));

            switch (opcjaZapisu)
            {
                case OpcjeEksportuDyspozycjiBankowych.DolaczDoPliku:
                    File.AppendAllLines(sciezkaDoPliku, linie);
                    break;
                case OpcjeEksportuDyspozycjiBankowych.UtworzPlik:
                    File.WriteAllLines(sciezkaDoPliku, linie);
                    break;
            }
        }
    }

    class DostawcaPluginow : IDostawcaPluginow
    {
        public string Adres { get { return "ul. Jerzmanowska 2, 54-519 Wrocław"; } }
        public string AdresWWW { get { return "www.insert.com.pl"; } }
        public IEnumerable<string> Kontakty { get { yield return "tel. +48 71 78 76 100"; yield return "email: office@insert.com.pl"; } }
        public string KRS { get { return "0000306888"; } }
        public string Nazwa { get { return "InsERT S.A."; } }
        public string NIP { get { return "898-19-45-134"; } }
        public string REGON { get { return "932283479"; } }
    }
}
