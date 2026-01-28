using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InsERT.Moria.Inwentaryzacja;
using InsERT.Moria.Rozszerzanie;
using System.IO;
using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace InwentaryzacjaImport
{
    public class FunkcjaParseraInwentaryzatoraXLSX : IFunkcjaParseraInwentaryzatora
    {
        private IDostawcaPluginow _dostawca = new DostawcaRozszerzenia();

        public string OczekiwaneRozszerzeniePlikuWejsciowego
        {
            get { return "xlsx"; }
        }

        public Guid Identyfikator
        {
            get { return Guid.Parse("BB188216-C38A-495D-B111-50A69F392160"); }
        }

        public string Nazwa
        {
            get { return "Parser plików Excela"; }
        }

        public string Opis
        {
            get { return "Funkcja importująca dane inwentaryzacyjne z arkusza Excela XLSX, z kolumn 'Kod kreskowy' oraz 'Stan'."; }
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

            Slownik<string, int> kolumny = new Slownik<string, int>();

            StringBuilder sbMsg = new StringBuilder();

            const string kolSymbol = "Symbol";
            const string kolIlosc = "Stan";
            const string kolKodKreskowy = "Kod kreskowy";

            try
            {
                using (SpreadsheetDocument spreadsheetDocument = SpreadsheetDocument.Open(plikWejsciowy, false))
                {
                    IEnumerable<Row> rowcollection = GetSpreadsheetRows(spreadsheetDocument);
                    if (rowcollection.Count() == 0) // jesli pusto to nic nie zwracamy
                        return null;

                    kolumny = GetColumns(spreadsheetDocument, rowcollection);
                    if (kolumny.Count == 3)
                    {
                        if (string.Concat(kolumny.Keys.ToArray()) == "POZYCJE=NULL")
                            return spis.SetPozycjeNull();
                        if (string.Concat(kolumny.Keys.ToArray()) == "SPIS=NULL")
                            return null;
                    }

                    if (kolumny.Where(k => k.Key == kolIlosc).Count() != 1 || kolumny.Where(k => k.Key == kolKodKreskowy).Count() != 1)
                    {
                        throw new InvalidDataException("Wczytywany plik Excela musi posiadać kolumnę z kodem kreskowym o nazwie 'Kod kreskowy' i kolumnę ze stanem towaru o nazwie 'Stan'.");
                    }

                    IEnumerator<Row> enumerator = rowcollection.GetEnumerator();
                    Row wiersz = null;
                    if (enumerator.MoveNext())
                        wiersz = enumerator.Current;
                    while (enumerator.MoveNext())
                    {
                        wiersz = enumerator.Current;
                        var cells = GetCells(wiersz, kolumny);
                        CellValueGetter dane = new CellValueGetter(spreadsheetDocument, cells, kolumny);
                        SparsowanaPozycjaInwentaryzatora pozycja = new SparsowanaPozycjaInwentaryzatora();
                        StringBuilder sbPozycja = new StringBuilder();
                        
                        //kod kreskowy
                        string kod = dane["Kod kreskowy"].Trim();
                        if (string.IsNullOrWhiteSpace(kod))
                            sbPozycja.Append("Pusty kod kreskowy.");
                         else
                            pozycja.KodKreskowy = dane["Kod kreskowy"].Trim();

                        //ilość
                        string strIlosc = dane["Stan"].Trim();
                        decimal ilosc = 0m;
                        try
                        {
                            ilosc = decimal.Parse(strIlosc, numFormat);
                            pozycja.Ilosc = ilosc;
                        }
                        catch (Exception)
                        {
                            sbPozycja.AppendFormat("Niepoprawnie określony stan: {0}. Komórka musi zawierać daną typu liczba.", strIlosc);
                        }

                        //symbol
                        if (dane.HasColumn(kolSymbol))
                        {
                            pozycja.SymbolAsortymentu = dane["Symbol"].Trim();
                        }

                        if (sbPozycja.Length > 1)
                        {
                            sbPozycja.Insert(0, string.Format("Wiersz {0}: ", wiersz.RowIndex.Value));
                            niepoprawne.Add(new Tuple<ISparsowanaPozycjaInwentaryzatora, string>(pozycja, sbPozycja.ToString()));
                        }
                        else
                            spis.Pozycje.Add(pozycja);
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

        /// <summary>
        /// Metoda pobierająca wiersze z danymi z arkusza kalkulacyjnego.
        /// </summary>
        /// <param name="spreadsheetDocument">Arkusz kalkulacyjny.</param>
        /// <returns>Zwraca wiersze z pierwszego arkusza podanego arkusza kalkulacyjnego.</returns>
        private static IEnumerable<Row> GetSpreadsheetRows(SpreadsheetDocument spreadsheetDocument)
        {
            WorkbookPart workbookPart = spreadsheetDocument.WorkbookPart;

            IEnumerable<Sheet> sheetcollection = spreadsheetDocument.WorkbookPart.Workbook.GetFirstChild<Sheets>().Elements<Sheet>();

            string relationshipId = sheetcollection.First().Id.Value;

            WorksheetPart worksheetPart = (WorksheetPart)spreadsheetDocument.WorkbookPart.GetPartById(relationshipId);

            SheetData sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();
            return sheetData.Descendants<Row>();
        }

        private static Slownik<string, int> _indeksyKolumn = new Slownik<string, int>();

        /// <summary>
        /// Metoda pobierająca mapę kolumn dla podanego arkusza i wierszy. Wiersze nagłówka muszą być w kolekcji wierszy.
        /// </summary>
        /// <param name="spreadsheetDocument">Arkusz kalkulacyjny.</param>
        /// <param name="rowCollection">Wiersze do przeanalizowania.</param>
        /// <returns>Zwraca mapę nazw kolumn na numer komórki w wierszu.</returns>
        /// <remarks>Metoda wypełnia też wewnętrzną, pomocniczą mapę indeksów kolumn, 
        /// mapującą nazwę kolumny (oznaczenie literowe: A, B, ..., AA, ... AAA, ...) na indeks w tablicy kolumn.</remarks>
        private static Slownik<string, int> GetColumns(SpreadsheetDocument spreadsheetDocument, IEnumerable<Row> rowCollection)
        {
            int count = rowCollection.ElementAt(0).Count();
            Slownik<string, int> columns = new Slownik<string, int>(count);
            string[] arColumns = new string[count];

            _indeksyKolumn.Clear();

            int i = 0;
            foreach (Cell cell in rowCollection.ElementAt(0))
            {
                string tekst = CellValueGetter.GetValueOfCell(spreadsheetDocument, cell);
                arColumns[i] = tekst;
                _indeksyKolumn[GetColumnName(cell.CellReference)] = i;
                i++;
                Debug.WriteLine("{0:d}, {1}", i, tekst);
            }

            for (i = 0; i < arColumns.Length; i++)
                if (!string.IsNullOrWhiteSpace(arColumns[i]))
                    columns.Add(arColumns[i], i);
            return columns;
        }

        /// <summary>
        /// Metoda zwracająca tablicę komórek wiersza w liczbie takiej jak nagłówki kolumn.
        /// </summary>
        /// <param name="row">Analizowany wiersz.</param>
        /// <param name="kolumny">Słownik z nagłówkami i indeksami kolumn.</param>
        /// <returns>Tablica z komórkami wiersza. Null w miejscach, gdzie nie ma komórek.</returns>
        private static Cell[] GetCells(Row row, Slownik<string, int> kolumny)
        {
            var cells = row.ChildElements.Cast<Cell>().ToArray();
            if (cells.Count() < kolumny.Count())
            {
                int count = kolumny.Count();
                Cell[] newCells = new Cell[count];
                foreach (var cell in cells)
                {
                    int idx = _indeksyKolumn[GetColumnName(cell.CellReference)];
                    newCells[idx] = cell;
                }
                cells = newCells;
            }
            return cells;
        }

        /// <summary>
        /// Metoda pobierająca nazwę kolumny na podstawie nazwy komórki np. "H32" -> "H"
        /// </summary>
        /// <param name="cellName">Nazwa komórki.</param>
        /// <returns></returns>
        private static string GetColumnName(string cellName)
        {
            Regex regex = new Regex("[A-Za-z]+");
            Match match = regex.Match(cellName);

            return match.Value;
        }

        /// <summary>
        /// Klasa pomocnicza do debugowania jakiej wartości klucza brakuje i leci wyjątek. Funkcjonalnie identyczna z <c>System.Collections.Generic.Dictionary</c>.
        /// </summary>
        /// <typeparam name="TKey">Typ klucza.</typeparam>
        /// <typeparam name="TValue">Typ wartości.</typeparam>
        class Slownik<TKey, TValue> : Dictionary<TKey, TValue>
        {
            public Slownik() { }
            public Slownik(int capacity) : base(capacity) { }

            public new TValue this[TKey key]
            {
                get
                {
                    return base[key];
                }
                set
                {
                    base[key] = value;
                }
            }
        }

        /// <summary>
        /// Klasa wspomagająca pobieranie wartości z komórek wiersza arkusza kalkulacyjnego po podaniu nazwy kolumny.
        /// Wymaga przygotowania tablicy komórek w wierszu oraz słownika mapującego nazwy kolumn na index w tablicy komórek.
        /// </summary>
        class CellValueGetter
        {
            private SpreadsheetDocument _spreadsheetDocument;
            private Cell[] _cells;
            IDictionary<string, int> _columnsIndexes;
            /// <summary>
            /// Konstruktor klasy dla podanego arkusza i wiersza.
            /// </summary>
            /// <param name="spreadsheetDocument">Arkusz kalkulacyjny.</param>
            /// <param name="cells">Tablica komórek wiersza.</param>
            /// <param name="columnsIndexes">Słownik mapujący nazwy kolumn na indeks w tablicy komórek.</param>
            public CellValueGetter(SpreadsheetDocument spreadsheetDocument, Cell[] cells, IDictionary<string, int> columnsIndexes)
            {
                _spreadsheetDocument = spreadsheetDocument;
                _cells = cells;
                _columnsIndexes = columnsIndexes;
            }
            /// <summary>
            /// Operator pobierania wartości tekstowej z komórki arkusza.
            /// </summary>
            /// <param name="name">Nazwa kolumny</param>
            /// <returns>Zwraca wartość komórki z kolumny o podanej danej nazwie.</returns>
            public string this[string name]
            {
                get { return GetValue(name); }
            }
            /// <summary>
            /// Metoda pobierająca wartość tekstową z komórki arkusza.
            /// </summary>
            /// <param name="name">Nazwa kolumny</param>
            /// <returns>Zwraca wartość komórki z kolumny o podanej danej nazwie.</returns>
            public string GetValue(string name)
            {
                try
                {
                    return GetValueOfCell(_spreadsheetDocument, _cells[_columnsIndexes[name]]);
                }
                catch (KeyNotFoundException ex)
                {
                    var message = FormatKeyNotFoundExceptionMessage(name, ex);
                    throw new InvalidOperationException(message, ex);
                }
            }

            public bool HasColumn(string name)
            {
                int idx;
                return _columnsIndexes.TryGetValue(name, out idx);
            }

            private string FormatKeyNotFoundExceptionMessage(string keyName, KeyNotFoundException ex)
            {
                var similar = string.Join("'\n'", _columnsIndexes.Where(ci => ci.Key.StartsWith(keyName.Split(' ')[0], StringComparison.CurrentCultureIgnoreCase)).Select(i => i.Key).ToArray());
                var message = string.Format("Nie udało się pobranie wartości z kolumny '{0}'.\n{1}\nKolumny o podobnych nazwach:\n'{2}'", keyName, ex.Message, similar);
                return message;
            }

            public static string GetValueOfCell(SpreadsheetDocument spreadsheetdocument, Cell cell)
            {
                // Get value in Cell
                SharedStringTablePart sharedString = spreadsheetdocument.WorkbookPart.SharedStringTablePart;
                if (cell == null || cell.CellValue == null)
                {
                    return string.Empty;
                }

                string cellValue = cell.CellValue.InnerText;

                // The condition that the Cell DataType is SharedString
                if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
                    cellValue = sharedString.SharedStringTable.ChildElements[int.Parse(cellValue)].InnerText;

                return cellValue;
            }
        }
    }
}
