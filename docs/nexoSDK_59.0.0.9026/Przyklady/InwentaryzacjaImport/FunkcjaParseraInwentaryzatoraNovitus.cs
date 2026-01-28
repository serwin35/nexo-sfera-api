using System;
using System.Collections.Generic;
using System.Text;
using InsERT.Moria.Inwentaryzacja;
using InsERT.Moria.Rozszerzanie;
using System.IO;
using System.Globalization;

namespace InwentaryzacjaImport
{
    internal class SparsowanySpisInwentaryzatora : ISparsowanySpisInwentaryzatora
    {
        public DateTime? DataWykonania
        {
            get;
            set;
        }

        private List<ISparsowanaPozycjaInwentaryzatora> _Pozycje = new List<ISparsowanaPozycjaInwentaryzatora>();
        public ICollection<ISparsowanaPozycjaInwentaryzatora> Pozycje
        {
            get { return _Pozycje; }
        }

        internal SparsowanySpisInwentaryzatora SetPozycjeNull()
        { _Pozycje = null; return this; }

        public string SymbolMagazynu
        {
            get;
            set;
        }

        public string Wykonujacy
        {
            get;
            set;
        }
    }
    internal class SparsowanaPozycjaInwentaryzatora : ISparsowanaPozycjaInwentaryzatora
    {
        public decimal? Cena
        {
            get;
            set;
        }

        public decimal Ilosc
        {
            get;
            set;
        }

        public string KodKreskowy
        {
            get;
            set;
        }

        public string Lokalizacja
        {
            get;
            set;
        }

        public string NumerPartii
        {
            get;
            set;
        }

        public string OpisPartii
        {
            get;
            set;
        }

        public string SymbolAsortymentu
        {
            get;
            set;
        }

        public string SymbolJednostkiMiary
        {
            get;
            set;
        }

        public DateTime? TerminWaznosci
        {
            get;
            set;
        }
    }

    public class FunkcjaParseraInwentaryzatoraNovitus : IFunkcjaParseraInwentaryzatora
    {
        private IDostawcaPluginow _dostawca = new DostawcaRozszerzenia();

        public string OczekiwaneRozszerzeniePlikuWejsciowego
        {
            get { return ""; }
        }

        public Guid Identyfikator
        {
            get { return Guid.Parse("375FC599-CB02-4131-AAE7-0D06243C3FAA"); }
        }

        public string Nazwa
        {
            get { return "Parser inwentaryzatora Novitus"; }
        }

        public string Opis
        {
            get { return "Parser plików z inwentaryzatora Novitus."; }
        }

        public IDostawcaPluginow Dostawca
        {
            get { return _dostawca; }
        }

        public ISparsowanySpisInwentaryzatora Parsuj(string plikWejsciowy)
        {
            IEnumerable<Tuple<ISparsowanaPozycjaInwentaryzatora, string>> niepoprawne;
            return Parsuj(plikWejsciowy, out niepoprawne);
        }

        private Dictionary<string, string> konfiguracja = null;

        private int GetParamInt(string klucz)
        {
            string wartosc = null;
            if (konfiguracja.TryGetValue(klucz, out wartosc))
                return int.Parse(wartosc);
            else
                return 0;
        }
        private char GetParamCharFromCode(string klucz)
        {
            return (char)int.Parse(konfiguracja[klucz]);
        }
        private bool GetParamBool(string klucz)
        {
            bool wartosc = false;
            switch (konfiguracja[klucz].ToLower())
            {
                case "tak":
                case "prawda":
                case "t":
                case "p":
                case "1":
                case "-1":
                case "y":
                case "yes":
                case "true":
                    wartosc = true;
                    break;
            }
            return wartosc;
        }

        public ISparsowanySpisInwentaryzatora Parsuj(string plikWejsciowy, out IEnumerable<Tuple<ISparsowanaPozycjaInwentaryzatora, string>> niepoprawnePozycje)
        {
            if (plikWejsciowy == null)
                throw new ArgumentNullException("plikWejsciowy", "Należy podać plik wejściowy.");
            if (string.IsNullOrEmpty(plikWejsciowy))
                throw new ArgumentException("plikWejsciowy", "Należy podać plik wejściowy.");
            if (!File.Exists(plikWejsciowy))
                throw new FileNotFoundException("Plik wejściowy nie istnieje.", plikWejsciowy);

            if (konfiguracja == null)
                WczytajDomyslnaKonfiguracje(plikWejsciowy);

            SparsowanySpisInwentaryzatora spis = null;
            niepoprawnePozycje = new List<Tuple<ISparsowanaPozycjaInwentaryzatora, string>>();

            var numFormat = (NumberFormatInfo)NumberFormatInfo.CurrentInfo.Clone();
            numFormat.NumberDecimalSeparator = ".";

            try
            {
                using (var plik = new StreamReader(plikWejsciowy, Encoding.GetEncoding(1250)))
                {
                    spis = new SparsowanySpisInwentaryzatora();
                    char[] sep = new char[] { GetParamCharFromCode("DOK_WY_SEPARATOR") };
                    while (!plik.EndOfStream)
                    {
                        var linia = plik.ReadLine();
                        var tokens = linia.Split(sep);
                        SparsowanaPozycjaInwentaryzatora pozycja = new SparsowanaPozycjaInwentaryzatora();

                        //data wykonania
                        if (spis.DataWykonania == null)
                        {
                            int idxDataWykonania = GetParamInt("DOK_WY_DATA");
                            if (idxDataWykonania > 0)
                                spis.DataWykonania = DateTime.ParseExact(tokens[idxDataWykonania - 1].Trim(), "yyyyMMdd", DateTimeFormatInfo.CurrentInfo);
                        }
                        //magazyn
                        if (spis.SymbolMagazynu == null)
                        {
                            int idxMagazyn = GetParamInt("DOK_WY_ID_DOKUMENTU");
                            if (idxMagazyn > 0)
                                spis.SymbolMagazynu = tokens[idxMagazyn - 1].Trim();
                        }

                        //cena
                        int idxCena = GetParamInt("DOK_WY_CENA");
                        if (idxCena > 0)
                        {
                            string sCena = tokens[idxCena - 1].Trim();
                            if (!string.IsNullOrWhiteSpace(sCena))
                                pozycja.Cena = decimal.Parse(sCena, numFormat) / (GetParamBool("CENA_W_GR") ? 100m : 1m);
                        }
                        //ilość
                        pozycja.Ilosc = decimal.Parse(tokens[GetParamInt("DOK_WY_ILOŚĆ") - 1].Trim(), numFormat);
                        //kod kreskowy
                        pozycja.KodKreskowy = tokens[GetParamInt("DOK_WY_KOD_KRESKOWY") - 1].Trim();
                        //lokalizacja
                        int idxLokalizacja = GetParamInt("DOK_WY_ID_DOKUMENTU");
                        if (idxLokalizacja > 0)
                            pozycja.Lokalizacja = tokens[idxLokalizacja - 1].Trim();
                        //opis partii
                        int idxOpisPartii = GetParamInt("DOK_WY_OPIS_POZ");
                        if (idxOpisPartii > 0)
                            pozycja.OpisPartii = tokens[idxOpisPartii - 1].Trim();
                        //asortyment
                        int idxTowar = GetParamInt("DOK_WY_NAZWA_TOWARU");
                        if (idxTowar > 0)
                            pozycja.SymbolAsortymentu = tokens[idxTowar - 1].Trim();
                        //jednostka miary
                        int idxJm = GetParamInt("DOK_WY_JM");
                        if (idxJm > 0)
                            pozycja.SymbolJednostkiMiary = tokens[idxJm - 1].Trim();
                        spis.Pozycje.Add(pozycja);
                    }
                }
            }
            catch (IOException ex)
            {
                throw ex;
            }
            return spis;
        }

        public static string NazwaDomyslnegoPlikuKonfiguracyjnego { get { return "ReadConfiguration.txt"; } } 

        private void WczytajDomyslnaKonfiguracje(string plikWejsciowy)
        {
            //próba odczytania z pliku leżącego obokwczytywanego pliku
            string sciezka = Path.GetDirectoryName(plikWejsciowy);
            if (sciezka != null)
            {
                string domyslnyPlikKonfiguracji = Path.Combine(sciezka, NazwaDomyslnegoPlikuKonfiguracyjnego);
                if (File.Exists(domyslnyPlikKonfiguracji))
                {
                    WczytajKonfiguracje(domyslnyPlikKonfiguracji);
                    if (konfiguracja != null && konfiguracja.Count > 2)
                        return;
                }
            }

            //ostatnia deska ratunku
            string konfig = @"
#INFO
TYP_LICENCJI=Podstawowa
#USTAWIENIA
CENA_W_GR=Tak
DOK_WY_ID_DOKUMENTU=0
DOK_WY_ID_RODZAJ_DOKUMENTU=0
DOK_WY_ID_KONTRAHENTA=0
DOK_WY_KOD_KRESKOWY=2
DOK_WY_ILOŚĆ=3
DOK_WY_OPIS_DOK=0
DOK_WY_OPIS_POZ=0
DOK_WY_DATA=0
DOK_WY_NAZWA_TOWARU=1
DOK_WY_CENA=4
DOK_WY_STAN=0
DOK_WY_JM=0
DOK_WY_OPIS_TOWARU=0
DOK_WY_OPIS_RODZAJ_DOKUMENTU=0
DOK_WY_OPIS_KONTRAHENTA=0
DOK_WY_SEPARATOR=44
";
            using (StringReader reader = new StringReader(konfig))
                WczytajKonfiguracje(reader);
        }

        public void WczytajKonfiguracje(string plik)
        {
            if (plik == null)
                throw new ArgumentNullException(nameof(plik));
            if (string.IsNullOrEmpty(plik))
                throw new ArgumentException("Należy podać plik z konfiguracją.");
            if (!File.Exists(plik))
                throw new FileNotFoundException("Podany plik nie istnieje.", plik);

            try
            {
                using (var reader = new StreamReader(plik, Encoding.GetEncoding(1250)))
                {
                    WczytajKonfiguracje(reader);
                }
            }
            catch (IOException ex)
            {
                throw ex;
            }
        }

        public void WczytajKonfiguracje(TextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            konfiguracja = new Dictionary<string, string>();

            char[] sep = new char[] { '=' };
            bool czytaj = false;
            string linia = null;
            while ((linia = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(linia))
                    continue;
                if (linia.StartsWith("#"))
                {
                    czytaj = linia.StartsWith("#USTAWIENIA");
                    continue;
                }
                if (czytaj)
                {
                    var tokens = linia.Split(sep);
                    konfiguracja[tokens[0].Trim()] = tokens[1].Trim();
                }
            }
        }
    }
}
