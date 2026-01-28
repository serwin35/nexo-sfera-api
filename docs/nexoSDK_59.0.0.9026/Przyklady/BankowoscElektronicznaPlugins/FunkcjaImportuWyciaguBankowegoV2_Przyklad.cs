using InsERT.Moria.Bank;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace BankowoscElektronicznaPlugins
{
    public class FunkcjaImportuWyciaguBankowegoV2_Przyklad : IFunkcjaImportuWyciaguBankowegoV2
    {
        public Guid Identyfikator { get; set; } = Guid.Parse("F197B0EC-1731-4024-A736-D6503729B3A7");
        
        public string Nazwa { get { return "Przykładowy import"; } }

        public string Opis { get { return "Przykładowa funkcja importu wyciągu bankowego"; } }

        public IDostawcaPluginow Dostawca { get { return new DostawcaPluginow(); } }

        public Encoding Kodowanie { get; set; } = Encoding.GetEncoding(1250);

        public string[] NumeryInstytucji => new string[] { "8811", "8882" };

        public Encoding DomyslneKodowanie => Kodowanie;

        public IEnumerable<IElektronicznyWyciagBankowy> Parsuj(string sciezkaDoPliku)
        {
            if (String.IsNullOrEmpty(sciezkaDoPliku))
                throw new ArgumentNullException(nameof(sciezkaDoPliku));

            var linie = File.ReadLines(sciezkaDoPliku);

            var wyciagi = new List<IElektronicznyWyciagBankowy>();
            int nrWyciagu = 1;

            foreach (var linia in linie)
            {
                wyciagi.Add(StworzWyciag(linia, nrWyciagu++));
            }
            
            return wyciagi;
        }

        private IElektronicznyWyciagBankowy StworzWyciag(string linia, int nrWyciagu)
        {
            return new ElektronicznyWyciagBankowy()
            {
                Data = DateTime.Today.AddDays(nrWyciagu - 1),
                Okres = new OkresWymagany() { DataPoczatkowa = DateTime.Today.AddDays(nrWyciagu - 1), DataKoncowa = DateTime.Today.AddDays(nrWyciagu - 1) },
                NumerRachunku = "91114000004372307491667848",
                Numer = string.Format("Wyciag {0}/{1}/{2}", nrWyciagu, DateTime.Today.AddDays(nrWyciagu - 1).Month, DateTime.Today.AddDays(nrWyciagu - 1).Year),
                SaldoPoczatkowe = 1000000 * nrWyciagu,
                SaldoKoncowe = 1000000 * (nrWyciagu + 1),
                SymbolWaluty = "PLN",
                Operacje = StworzOperacje(linia, nrWyciagu)
            };
        }

        private IEnumerable<IElektronicznaOperacjaBankowa> StworzOperacje(string linia, int nrWyciagu)
        {
            return new IElektronicznaOperacjaBankowa[]
            {
                new ElektronicznaOperacjaBankowa()
                {
                    DataKsiegowania = DateTime.Today.AddDays(nrWyciagu - 1),
                    DataOperacji = DateTime.Today.AddDays(nrWyciagu - 1),
                    Kontrahent = "ABC",
                    Kwota = 1000000,
                    NumerRachunkuKontrahenta = "46105000028209862762977957",
                    TNR = DateTime.Now.Ticks.ToString(),
                    TrescOperacji = linia,
                    Tytul = "przelew",
                    Typ = TypElektronicznejOperacjiBankowej.Przelew,
                    Waluta = "PLN",
                    Wplyw = true
                }
            };
        }
    }

    public class ElektronicznyWyciagBankowy : IElektronicznyWyciagBankowy
    {
        public DateTime? Data { get; set; }

        public OkresWymagany Okres { get; set; }

        public string NumerRachunku { get; set; }

        public string Numer { get; set; }

        public IEnumerable<IElektronicznaOperacjaBankowa> Operacje { get; set; }

        public decimal SaldoPoczatkowe { get; set; }

        public decimal SaldoKoncowe { get; set; }

        public string SymbolWaluty { get; set; }
    }

    public class ElektronicznaOperacjaBankowa : IElektronicznaOperacjaBankowa
    {
        public string Waluta { get; set; }

        public DateTime DataKsiegowania { get; set; }

        public DateTime DataOperacji { get; set; }

        public decimal Kwota { get; set; }

        public string Tytul { get; set; }

        public string NumerRachunkuKontrahenta { get; set; }

        public string Kontrahent { get; set; }

        public bool Wplyw { get; set; }

        public string TrescOperacji { get; set; }

        public string TNR { get; set; }

        public string ReferencjeKlienta { get; set; }

        public TypElektronicznejOperacjiBankowej Typ { get; set; }

        public Tuple<double, int, string> DopasowanyKontrahent { get; set; }

        public IList<Tuple<double, int, string>> DopasowanieNajlepszych { get; set; }
    }
}
