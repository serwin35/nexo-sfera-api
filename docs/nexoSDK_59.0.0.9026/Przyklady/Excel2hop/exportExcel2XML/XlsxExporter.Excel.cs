using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;


namespace exportExcel2XML
{
    public partial class XlsxExporter
    {
        private static string GetValueOfCell<T>(SpreadsheetDocument spreadsheetdocument, Cell cell)
        {
            string value = GetValueOfCell(spreadsheetdocument, cell);
            Type type = typeof(T);
            if (type.FullName == "System.Boolean")
                value = ((new string[] { "tak", "yes", "Y", "1", "true", "prawda", "T" })
                    .Any(p => p.Equals(value, StringComparison.CurrentCultureIgnoreCase)) ? "1" : "0");
            else if (type.FullName == "System.Decimal")
            {
                decimal decValue;
                if (!decimal.TryParse(value, out decValue))
                    decValue = 0;
                value = decValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return value;
        }

        private static string GetValueOfCell(SpreadsheetDocument spreadsheetdocument, Cell cell)
        {
            // Get value in Cell
            SharedStringTablePart sharedString = spreadsheetdocument.WorkbookPart.SharedStringTablePart;
            if (cell.CellValue == null)
            {
                return string.Empty;
            }

            string cellValue = cell.CellValue.InnerText;

            // The condition that the Cell DataType is SharedString
            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
                cellValue = sharedString.SharedStringTable.ChildElements[int.Parse(cellValue)].InnerText;
            if (cellValue == "-")
                cellValue = string.Empty;
            return cellValue;
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

        /// <summary>
        /// Metoda pobierająca mapę kolumn dla podanego arkusza i wierszy. Wiersze nagłówka muszą być w kolekcji wierszy.
        /// </summary>
        /// <param name="spreadsheetDocument">Arkusz kalkulacyjny.</param>
        /// <param name="rowCollection">Wiersze do przeanalizowania.</param>
        /// <returns>Zwraca mapę nazw kolumn na numer komórki w wierszu.</returns>
        private static Slownik<string, int> GetColumns(SpreadsheetDocument spreadsheetDocument, IEnumerable<Row> rowCollection)
        {
            int count = Math.Max(rowCollection.ElementAt(0).Count(), rowCollection.ElementAt(1).Count());
            Slownik<string, int> columns = new Slownik<string, int>(count);
            string[] arColumns = new string[count];

            int i = 0;
            foreach (Cell cell in rowCollection.ElementAt(0))
            {
                string tekst = GetValueOfCell(spreadsheetDocument, cell);
                arColumns[i++] = tekst;
                Debug.WriteLine("{0:d}, {1}", i, tekst);
            }

            i = 0;
            string prefiks = null;
            foreach (Cell cell in rowCollection.ElementAt(1))
            {
                string tekst = GetValueOfCell(spreadsheetDocument, cell);
                if (!string.IsNullOrWhiteSpace(tekst))
                {
                    if (string.IsNullOrWhiteSpace(prefiks))
                        prefiks = arColumns[i];
                    if (!string.IsNullOrWhiteSpace(arColumns[i]))
                        prefiks = arColumns[i];
                    if (!string.IsNullOrWhiteSpace(prefiks))
                    {
                        arColumns[i] = prefiks + " - " + tekst;
                    }
                }
                else
                    prefiks = null;
                Debug.WriteLine("{0:d}, {1}", i, arColumns[i]);
                i++;
            }

            for (i = 0; i < arColumns.Length; i++)
                if (!string.IsNullOrWhiteSpace(arColumns[i]))
                    columns.Add(arColumns[i], i);
            return columns;
        }
        [Obsolete]
        private static Slownik<string, int> GetColumns_Old(SpreadsheetDocument spreadsheetDocument, IEnumerable<Row> rowCollection)
        {
            int count = rowCollection.ElementAt(0).Count();
            Slownik<string, int> columns = new Slownik<string, int>(count);
            string[] arColumns = null;

            int i = 0;
            List<string> listColumns = new List<string>();
            foreach (Cell cell in rowCollection.ElementAt(0))
            {
                string tekst = GetValueOfCell(spreadsheetDocument, cell);
                if (!string.IsNullOrWhiteSpace(tekst))
                {
                    listColumns.Add(tekst);
                    Debug.WriteLine("{0:d}, {1}", ++i, tekst);
                }
            }
            arColumns = listColumns.ToArray();

            i = 0;
            string prefiks = null;
            foreach (Cell cell in rowCollection.ElementAt(1))
            {
                if (i >= arColumns.Length)
                    break;
                string tekst = GetValueOfCell(spreadsheetDocument, cell);
                if (!string.IsNullOrWhiteSpace(tekst))
                {
                    if (string.IsNullOrWhiteSpace(prefiks))
                        prefiks = arColumns[i];
                    if (!string.IsNullOrWhiteSpace(arColumns[i]))
                        prefiks = arColumns[i];
                    if (!string.IsNullOrWhiteSpace(prefiks))
                    {
                        arColumns[i] = prefiks + " - " + tekst;
                    }
                }
                else
                    prefiks = null;
                Debug.WriteLine("{0:d}, {1}", i, arColumns[i]);
                i++;
            }

            for (i = 0; i < arColumns.Length; i++)
                columns.Add(arColumns[i], i);
            return columns;
        }

        private static Cell[] GetCells(Row row, Slownik<string, int> kolumny, System.Text.StringBuilder sb)
        {
            var cells = row.Descendants<Cell>().Where(c => c.CellValue != null).ToArray();
            if (cells.Count() == 0)
                return null;
            else if (cells.Count() < kolumny.Count())
            {
                if (sb.Length == 0)
                    sb.AppendLine("\r\n\r\nUwagi do przetwarzanych danych:");
                sb.AppendFormat("Wiersz {0} ma wypełnionych komórek {1} zamiast {2}. Puste powinny zawierać \"-\".\r\n",
                    row.RowIndex, cells.Count(), kolumny.Count());
                return null;
            }
            return cells.ToArray();
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
            /// <summary>
            /// Metoda pobierająca wartość z komórki arkusza i konwertująca ją na tekst wg wymagań XML dla określonego typu. 
            /// </summary>
            /// <typeparam name="T">Typ wartości XML.</typeparam>
            /// <param name="name">Nazwa kolumny.</param>
            /// <returns>Zwraca tekst reprezentujący wartość z komórki arkusza zgodnie z wymaganiami XML.</returns>
            public string GetValue<T>(string name)
            {
                try
                {
                    return GetValueOfCell<T>(_spreadsheetDocument, _cells[_columnsIndexes[name]]);
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

            private static string GetValueOfCell(SpreadsheetDocument spreadsheetdocument, Cell cell)
            {
                // Get value in Cell
                SharedStringTablePart sharedString = spreadsheetdocument.WorkbookPart.SharedStringTablePart;
                if (cell.CellValue == null)
                {
                    return string.Empty;
                }

                string cellValue = cell.CellValue.InnerText;

                // The condition that the Cell DataType is SharedString
                if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
                    cellValue = sharedString.SharedStringTable.ChildElements[int.Parse(cellValue)].InnerText;
                if (cellValue == "-")
                    cellValue = string.Empty;
                return cellValue;
            }

            private static string GetValueOfCell<T>(SpreadsheetDocument spreadsheetdocument, Cell cell)
            {
                string value = GetValueOfCell(spreadsheetdocument, cell);
                Type type = typeof(T);
                if (type.FullName == "System.Boolean")
                    value = ((new string[] { "tak", "yes", "Y", "1", "true", "prawda", "T" })
                        .Any(p => p.Equals(value, StringComparison.CurrentCultureIgnoreCase)) ? "1" : "0");
                else if (type.FullName == "System.Decimal")
                {
                    decimal decValue;
                    if (!decimal.TryParse(value, NumberStyles.Number, NumberFormatInfo.InvariantInfo, out decValue))
                        decValue = 0;
                    value = decValue.ToString("0.##", NumberFormatInfo.InvariantInfo);
                }
                else if (type.FullName == "System.DateTime")
                {
                    double nDays;
                    if (double.TryParse(value, out nDays))
                    {
                        DateTime dateValue = DateTime.FromOADate(nDays);
                        value = dateValue.ToString("yyy-MM-dd");
                    }
                }
                return value;
            }
        }

        /// <summary>
        /// Klasa ułatwiająca generowanie XML-a i upraszczająca kod.
        /// </summary>
        class ElementHelper
        {
            private XNamespace _ns;
            private XElement _elem;
            private CellValueGetter _data;
            /// <summary>
            /// Konstruktor elementu XML pozwalający na określenie przestrzeni nazw, nazwy oraz źródła danych dla elementu i jego podelementów.
            /// </summary>
            /// <param name="ns">Przestrzeń nazw dla elementu i jego podelementów.</param>
            /// <param name="name">Nazwa elementu.</param>
            /// <param name="data">Źródło danych dla elementu i jego podelementów.</param>
            public ElementHelper(string ns, string name, CellValueGetter data)
            {
                _ns = ns;
                Init(_ns, name, data, null);
            }
            /// <summary>
            /// Konstruktor elementu pomocniczego dla istniejącego już elementu XML.
            /// </summary>
            /// <param name="elem">Isteniejący element XML.</param>
            /// <param name="ns">Przestrzeń nazw dla elementu i jego podelementów.</param>
            /// <param name="data">Źródło danych dla elementu i jego podelementów.</param>
            public ElementHelper(XElement elem, string ns, CellValueGetter data)
            {
                _ns = ns;
                _data = data;
                _elem = elem;
            }

            private ElementHelper() { }
            private void Init(XNamespace ns, string name, CellValueGetter data, string idx)
            {
                _ns = ns;
                _elem = new XElement(_ns + name, data == null || idx == null ? null : data[idx]);
                _data = data;
            }
            private void Init<T>(XNamespace ns, string name, CellValueGetter data, string idx)
            {
                _ns = ns;
                _elem = new XElement(_ns + name, data == null || idx == null ? null : data.GetValue<T>(idx));
                _data = data;
            }
            private void Init(XNamespace ns, string name, object content)
            {
                _ns = ns;
                _elem = new XElement(_ns + name, content);
            }
            /// <summary>
            /// Metoda dodająca nowy podelement.
            /// </summary>
            /// <param name="name">Nazwa dodawanego elementu.</param>
            /// <param name="idx">Nazwa kolumny, z której dane mają zostać umieszczone jako zawartość dodawanego elementu.</param>
            /// <param name="omitIfDefault"><c>true</c> jeśli element jest opcjonalny. W przypadku wartości pustej w podanej kolumnie, element nie będzie utworzony.</param>
            /// <returns>Zwraca nowoutworzony element.</returns>
            public ElementHelper AddElement(string name, string idx = null, bool omitIfDefault = false)
            {
                if (omitIfDefault)
                {
                    if (_data == null || idx == null || string.IsNullOrWhiteSpace(_data[idx]))
                        return null;
                }
                var ret = new ElementHelper();
                ret.Init(_ns, name, _data, idx);
                _elem.Add(ret._elem);
                return ret;
            }
            /// <summary>
            /// Metoda dodająca nowy podelement z bezpośrednim podaniem jego zawartości.
            /// </summary>
            /// <param name="name">Nazwa dodawanego elementu.</param>
            /// <param name="content">Zawartość dodawanego elementu. Np. tekst.</param>
            /// <returns>Zwraca nowoutworzony element.</returns>
            public ElementHelper AddElementValue(string name, object content)
            {
                var ret = new ElementHelper();
                ret.Init(_ns, name, content);
                _elem.Add(ret._elem);
                return ret;
            }
            /// <summary>
            /// Metoda dodająca nowy podelement, pozwalająca na ustawienie przestrzeni nazw oraz źródła danych dla podelementów.
            /// </summary>
            /// <param name="ns">Przestrzeń nazw dla elementu i jego podelementów.</param>
            /// <param name="name">Nazwa elementu.</param>
            /// <param name="data">Źródło danych dla elementu i jego podelmentów.</param>
            /// <returns>Zwraca nowoutworzony element.</returns>
            public ElementHelper AddElementHelper(string ns, string name, CellValueGetter data)
            {
                var ret = new ElementHelper(ns, name, data);
                _elem.Add(ret._elem);
                return ret;
            }
            public ElementHelper AddElement(string ns, string name, string idx)
            {
                var ret = new ElementHelper();
                ret.Init(ns, name, _data, idx);
                _elem.Add(ret._elem);
                return ret;
            }
            /// <summary>
            /// Metoda dołączająca podany element jako kolejny podelement.
            /// </summary>
            /// <param name="elem">Element do dołączenia.</param>
            public void Append(ElementHelper elem)
            {
                _elem.Add(elem._elem);
            }
            /// <summary>
            /// Metoda dodająca atrybut do elementu.
            /// </summary>
            /// <param name="name">Nazwa dodawanego atrybutu.</param>
            /// <param name="idx">Nazwa kolumny, z której dane mają zostać umieszczone jako zawartość dodawanego atrybutu.</param>
            public void AddAttribute(string name, string idx)
            {
                _elem.Add(new XAttribute(name, _data == null || idx == null ? null : _data[idx]));
            }
            /// <summary>
            /// Metoda dodająca atrybut do elementu, wpisująca wartość zgodnie z wymaganiami XML dla określonego typu.
            /// </summary>
            /// <typeparam name="T">Typ wartości w XML.</typeparam>
            /// <param name="name">Nazwa dodawanego atrybutu.</param>
            /// <param name="idx">Nazwa kolumny, z której dane mają zostać umieszczone jako wartość dodawanego atrybutu.</param>
            public void AddAttribute<T>(string name, string idx, bool omitIfDefault = false)
            {
                if (omitIfDefault)
                {
                    if (_data == null || idx == null || string.IsNullOrWhiteSpace(_data[idx]))
                        return;
                }

                _elem.Add(new XAttribute(name, _data == null || idx == null ? null : _data.GetValue<T>(idx)));
            }
            /// <summary>
            /// Metoda dodająca atrybut do elementu.
            /// </summary>
            /// <param name="name">Nazwa dodawanego atrybutu.</param>
            /// <param name="idx">Nazwa kolumny, z której dane mają zostać umieszczone jako zawartość dodawanego atrybutu.</param>
            /// <param name="omitIfDefault"><c>true</c> jeśli atrybut jest opcjonalny. W przypadku wartości pustej w podanej kolumnie, atrybut nie będzie utworzony.</param>
            public void AddAttribute(string name, string idx, bool omitIfDefault = false)
            {
                if (omitIfDefault)
                {
                    if (_data == null || idx == null || string.IsNullOrWhiteSpace(_data[idx]))
                        return;
                }

                _elem.Add(new XAttribute(name, _data == null || idx == null ? null : _data[idx]));
            }
            /// <summary>
            /// Metoda dodająca atrybut z bezpośrednim podaniem jego wartości.
            /// </summary>
            /// <param name="name">Nazwa dodawanego atrubutu.</param>
            /// <param name="content">Wartość dodawanego atrybutu. Np. tekst.</param>
            public void AddAttributeValue(string name, object content)
            {
                _elem.Add(new XAttribute(name, content));
            }
            /// <summary>
            /// Metoda dodająca nowy podelement, wpisująca zawartość zgodnie z wymaganiami XML dla określonego typu.
            /// </summary>
            /// <typeparam name="T">Typ wartości w XML.</typeparam>
            /// <param name="name">Nazwa dodawanego elementu.</param>
            /// <param name="idx">Nazwa kolumny, z której dane mają zostać umieszczone jako zawartość dodawanego elementu.</param>
            /// <returns>Zwraca nowoutworzony element.</returns>
            public ElementHelper AddElement<T>(string name, string idx, bool omitIfDefault = false)
            {
                if (omitIfDefault)
                {
                    if (_data == null || idx == null || string.IsNullOrWhiteSpace(_data[idx]))
                        return null;
                }

                var ret = new ElementHelper();
                ret.Init<T>(_ns, name, _data, idx);
                _elem.Add(ret._elem);
                return ret;
            }
            /// <summary>
            /// Metoda zwracająca wewnętrzny element XML
            /// </summary>
            /// <returns>Zwraca wewnętrzny element XML.</returns>
            public XElement GetXElement() { return _elem; }
        }

        ~XlsxExporter()
        {
            foreach (string file in tempFilenames)
            {
                try
                {
                    File.Delete(file);
                }
                catch(Exception) {}
            }
        }
        private List<string> tempFilenames = new List<string>();
        private SpreadsheetDocument OpenSpreadsheetDocument(string filename)
        {
            try
            {
                return SpreadsheetDocument.Open(filename, false);
            }
            catch (OpenXmlPackageException e)
            {
                if (e.ToString().Contains("Invalid Hyperlink"))
                {
                    string newFileName = Path.GetTempFileName();
                    tempFilenames.Add(newFileName);
                    using (FileStream fs = new FileStream(newFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                    {
                        fs.SetLength(0L);
                        using (FileStream fsInput = new FileStream(filename, FileMode.Open, FileAccess.Read))
                            fsInput.CopyTo(fs);
                        UriFixer.FixInvalidUri(fs, brokenUri => FixUri(brokenUri));
                    }
                    return SpreadsheetDocument.Open(newFileName, false);
                }
            }
            return null;
        }

        private static Uri FixUri(string brokenUri)
        {
            return new Uri("http://broken-link/");
        }
    }

    public static class UriFixer
    {
        public static void FixInvalidUri(Stream fs, Func<string, Uri> invalidUriHandler)
        {
            XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
            using (ZipArchive za = new ZipArchive(fs, ZipArchiveMode.Update))
            {
                foreach (var entry in za.Entries.ToList())
                {
                    if (!entry.Name.EndsWith(".rels"))
                        continue;
                    bool replaceEntry = false;
                    XDocument entryXDoc = null;
                    using (var entryStream = entry.Open())
                    {
                        try
                        {
                            entryXDoc = XDocument.Load(entryStream);
                            if (entryXDoc.Root != null && entryXDoc.Root.Name.Namespace == relNs)
                            {
                                var urisToCheck = entryXDoc
                                    .Descendants(relNs + "Relationship")
                                    .Where(r => r.Attribute("TargetMode") != null && (string)r.Attribute("TargetMode") == "External");
                                foreach (var rel in urisToCheck)
                                {
                                    var target = (string)rel.Attribute("Target");
                                    if (target != null)
                                    {
                                        try
                                        {
                                            Uri uri = new Uri(target);
                                        }
                                        catch (UriFormatException)
                                        {
                                            Uri newUri = invalidUriHandler(target);
                                            rel.Attribute("Target").Value = newUri.ToString();
                                            replaceEntry = true;
                                        }
                                    }
                                }
                            }
                        }
                        catch (XmlException)
                        {
                            continue;
                        }
                    }
                    if (replaceEntry)
                    {
                        var fullName = entry.FullName;
                        entry.Delete();
                        var newEntry = za.CreateEntry(fullName);
                        using (StreamWriter writer = new StreamWriter(newEntry.Open()))
                        using (XmlWriter xmlWriter = XmlWriter.Create(writer))
                        {
                            entryXDoc.WriteTo(xmlWriter);
                        }
                    }
                }
            }
        }
    }
}
