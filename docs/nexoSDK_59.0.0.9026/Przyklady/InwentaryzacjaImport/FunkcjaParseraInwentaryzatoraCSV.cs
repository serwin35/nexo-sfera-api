using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InsERT.Moria.Inwentaryzacja;
using InsERT.Moria.Rozszerzanie;
using System.IO;
using System.Globalization;

namespace InwentaryzacjaImport
{
    public class FunkcjaParseraInwentaryzatoraCSV : IFunkcjaParseraInwentaryzatora
    {
        private IDostawcaPluginow _dostawca = new DostawcaRozszerzenia();

        public string OczekiwaneRozszerzeniePlikuWejsciowego
        {
            get { return ""; }
        }

        public Guid Identyfikator
        {
            get { return Guid.Parse("A396E3CB-874A-497E-A2C6-AF0392C18616"); }
        }

        public string Nazwa
        {
            get { return "Parser plików CSV"; }
        }

        public string Opis
        {
            get { return "Parser plików CSV o stałej strukturze (symbol;kod;ilość)."; }
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

        public ISparsowanySpisInwentaryzatora Parsuj(string plikWejsciowy, out IEnumerable<Tuple<ISparsowanaPozycjaInwentaryzatora, string>> niepoprawnePozycje)
        {
            if (plikWejsciowy == null)
                throw new ArgumentNullException("plikWejsciowy", "Należy podać plik wejściowy.");
            if (string.IsNullOrEmpty(plikWejsciowy))
                throw new ArgumentException("plikWejsciowy", "Należy podać plik wejściowy.");
            if (!File.Exists(plikWejsciowy))
                throw new FileNotFoundException("Plik wejściowy nie istnieje.", plikWejsciowy);

            SparsowanySpisInwentaryzatora spis = new SparsowanySpisInwentaryzatora();

            var niepoprawne = new List<Tuple<ISparsowanaPozycjaInwentaryzatora, string>>();
            niepoprawnePozycje = null;

            var numFormat = (NumberFormatInfo)NumberFormatInfo.CurrentInfo.Clone();
            numFormat.NumberDecimalSeparator = ".";

            try
            {
                using (var plik = new StreamReader(plikWejsciowy, Encoding.GetEncoding(1250)))
                {
                    char[] sep = new char[] { ';' };
                    for (int idx = 1; !plik.EndOfStream; idx++)
                    {
                        var linia = plik.ReadLine();
                        var tokens = linia.Split(sep);
                        SparsowanaPozycjaInwentaryzatora pozycja = new SparsowanaPozycjaInwentaryzatora();
                        StringBuilder sbPozycja = new StringBuilder();
                        if (tokens.Length == 3)
                        {
                            if (tokens[2].ToLower() == "ilość")
                                continue;

                            //asortyment
                            pozycja.SymbolAsortymentu = tokens[0].Trim();
                            try
                            {
                                //ilość
                                pozycja.Ilosc = decimal.Parse(tokens[2].Trim(), numFormat);
                                if (pozycja.Ilosc < 0)
                                    sbPozycja.Append("Ilość jest mniejsza od 0.");

                                //kod kreskowy
                                pozycja.KodKreskowy = tokens[1].Trim();
                                if (string.IsNullOrEmpty(pozycja.KodKreskowy))
                                    sbPozycja.Append("Pusty kod kreskowy.");

                                if (sbPozycja.Length == 0)
                                    spis.Pozycje.Add(pozycja);
                            }
                            catch (Exception ex)
                            {
                                sbPozycja.Append(ex.Message);
                            }
                        }
                        else
                            sbPozycja.Append("Niepoprawna liczba elementów w wierszu. Oczekiwano 3 (symbol;kod;ilość).");

                        if (sbPozycja.Length > 0)
                        {
                            sbPozycja.Insert(0, string.Format("Linia {0}: ", idx));
                            niepoprawne.Add(new Tuple<ISparsowanaPozycjaInwentaryzatora, string>(pozycja, sbPozycja.ToString()));
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                throw ex;
            }
            if (niepoprawne.Any())
                niepoprawnePozycje = niepoprawne;
            return spis;
        }

    }
}
