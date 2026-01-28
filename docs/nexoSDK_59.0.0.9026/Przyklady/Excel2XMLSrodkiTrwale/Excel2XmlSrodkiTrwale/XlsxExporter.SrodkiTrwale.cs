using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Excel2XmlSrodkiTrwale
{
    public partial class XlsxExporter
    {
        static XNamespace nsSrodkiTrwale = "http://schemas.insert.com.pl/2018/hop/srodkitrwale";

        public string[] ExportSrodkiTrwale(string filename)
        {
            int lp = 0;
            Slownik<string, int> kolumny = new Slownik<string, int>();

            XElement srodkiTrwale = new XElement(nsSrodkiTrwale + "SrodkiTrwale");

            StringBuilder sbMsg = new StringBuilder();

            try
            {
                using (SpreadsheetDocument arkuszKalkulacyjny = OpenSpreadsheetDocument(filename))
                {
                    int liczbaZakladek = GetNumberWorkbookPart(arkuszKalkulacyjny);
                    
                    for(int i = 1; i <= liczbaZakladek; i++)
                    {
                        IEnumerable<Row> kolekcjaWierszy = GetSpreadsheetRows(arkuszKalkulacyjny, i);
                        if (kolekcjaWierszy.Count() == 0)
                            return null;
                        Debug.WriteLine("{0:d}", kolekcjaWierszy.Count());

                        kolumny = GetColumnsWhereOneTitleRow(arkuszKalkulacyjny, kolekcjaWierszy);

                        IEnumerator<Row> enumerator = kolekcjaWierszy.GetEnumerator(); 
                        Row wiersz = null;

                        while (enumerator.MoveNext()) 
                        {
                            wiersz = enumerator.Current;
                            while (wiersz.RowIndex < 2)
                            {
                                if (!enumerator.MoveNext())
                                    break;
                                wiersz = enumerator.Current;
                            }

                            var cells = GetCells(wiersz, kolumny, sbMsg, GetNumberWorkbookPart(arkuszKalkulacyjny, i));

                            if (cells != null)
                            {
                                CellValueGetter dane = new CellValueGetter(arkuszKalkulacyjny, cells, kolumny);

                                ElementHelper poz = GenerujSrodekTrwaly(dane, ++lp);

                                if (!string.IsNullOrWhiteSpace(poz.GetXElement().Value))
                                    srodkiTrwale.Add(poz.GetXElement());
                            }
                        }
                    }
                }
            }
            catch (IOException exception)
            {

                throw new IOException(exception.Message);
            }


            List<string> info = new List<string>();
            string sciezka = Path.GetDirectoryName(filename);

            if (srodkiTrwale != null)
            {
                info.Add(ZapiszDoPliku(sciezka, "SrodkiTrwale.xml", srodkiTrwale));
            }

            if (sbMsg.Length > 0)
                info.Add(sbMsg.ToString());

            return info.ToArray();
        }

        public string[] ExportSrodkiTrwaleIOperacjeST(string filename)
        {
            int lp = 0, nr = 1;
            Slownik<string, int> kolumny = new Slownik<string, int>();

            XElement srodkiTrwale = new XElement(nsSrodkiTrwale + "SrodkiTrwale");
            XElement operacjeST =
                new XElement(nsOperacjeST + "OperacjeST",
                            new XAttribute(XNamespace.Xmlns + "MU", nsMiejscaUzytkowania),
                            new XAttribute(XNamespace.Xmlns + "ST", nsSrodkiTrwale)
                            );

            StringBuilder sbMsg = new StringBuilder();

            try
            {
                using (SpreadsheetDocument arkuszKalkulacyjny = OpenSpreadsheetDocument(filename))
                {
                    int liczbaZakladek = GetNumberWorkbookPart(arkuszKalkulacyjny);

                    for (int i = 1; i <= liczbaZakladek; i++)
                    {
                        IEnumerable<Row> kolekcjaWierszy = GetSpreadsheetRows(arkuszKalkulacyjny, i);
                        if (kolekcjaWierszy.Count() == 0)
                            return null;
                        Debug.WriteLine("{0:d}", kolekcjaWierszy.Count());

                        kolumny = GetColumnsWhereOneTitleRow(arkuszKalkulacyjny, kolekcjaWierszy);

                        IEnumerator<Row> enumerator = kolekcjaWierszy.GetEnumerator();
                        Row wiersz = null;

                        while (enumerator.MoveNext()) 
                        {
                            wiersz = enumerator.Current;
                            while (wiersz.RowIndex < 2)
                            {
                                if (!enumerator.MoveNext())
                                    break;
                                wiersz = enumerator.Current;
                            }

                            var cells = GetCells(wiersz, kolumny, sbMsg, GetNumberWorkbookPart(arkuszKalkulacyjny, i));

                            if (cells != null)
                            {
                                CellValueGetter dane = new CellValueGetter(arkuszKalkulacyjny, cells, kolumny);

                                ElementHelper poz = GenerujSrodekTrwaly(dane, ++lp);
                                ElementHelper elem = GenerujOperacjeST(dane, lp, ref nr);

                                if (!string.IsNullOrWhiteSpace(poz.GetXElement().Value))
                                    srodkiTrwale.Add(poz.GetXElement());
                                if (!string.IsNullOrWhiteSpace(elem.GetXElement().Value))
                                    operacjeST.Add(elem.GetXElement());
                            }
                        }
                    }
                }
            }
            catch (IOException exception)
            {

                throw new IOException(exception.Message);
            }


            List<string> info = new List<string>();
            string sciezka = Path.GetDirectoryName(filename);

            if (srodkiTrwale != null)
            {
                info.Add(ZapiszDoPliku(sciezka, "SrodkiTrwale.xml", srodkiTrwale));
                info.Add(ZapiszDoPliku(sciezka, "OperacjeST.xml", operacjeST));
            }

            if (sbMsg.Length > 0)
                info.Add(sbMsg.ToString());

            return info.ToArray();
        }

        private static ElementHelper GenerujSrodekTrwaly(CellValueGetter dane, int lp)
        {
            var poz = new ElementHelper(nsSrodkiTrwale.NamespaceName, "SrodekTrwaly", dane);
            poz.AddAttributeValue("Ident", lp);
            poz.AddElementValue("Numer", lp);
            poz.AddElementValue("Nazwa", dane["Nazwa"]);
            poz.AddElementValue("Typ", "SrodekTrwaly");

            DateTime dt;
            string dataNabycia;
            if (DateTime.TryParseExact(dane.GetValue<DateTime>("Data nabycia"), "dd-mm-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                dataNabycia = dt.ToString("yyyy-MM-dd");
            }
            else
            {
                dataNabycia = DateTime.Parse(dane.GetValue<DateTime>("Data nabycia")).ToString("yyyy-MM-dd");
            }

            if (dane.HasColumn("Grupa KŚT"))
            {
                if (!dane["Grupa KŚT"].Equals(""))
                {
                    ElementHelper kst;
                    if (DateTime.Parse(dane.GetValue<DateTime>("Data nabycia")).CompareTo(new DateTime(2018, 1, 1)) < 0)
                    {
                        kst = poz.AddElement("KST");
                        kst.AddAttributeValue("Ident", 9);
                    }
                    else
                    {
                        kst = poz.AddElement("KST2016");
                        kst.AddAttributeValue("Ident", 9);
                    }
                    kst.AddElementValue("Nr", dane["Grupa KŚT"]);
                    kst.AddElementValue("Nazwa", " ");
                }
            }

            if (dane.HasColumn("Typ WNiP"))
            {
                if(!dane["Typ WNiP"].Equals(""))
                {
                    poz.AddElementValue("TypWNiP", dane["Typ WNiP"]);
                } 
            }

            poz.AddElementValue("NrInwentarzowy", lp + "/2018");

            List<string> polaDodatkowe = new List<string>() { "GUS", "Sprzedawca"};
            foreach (var poleDodatkowe in polaDodatkowe)
            {
                if (dane.HasColumn(poleDodatkowe))
                {
                    if (!dane[poleDodatkowe].Equals(""))
                    {
                        poz.AddElementValue(poleDodatkowe, dane[poleDodatkowe]);
                    }
                }
            }

            string nrDokumentuOT = "";
            if (dane.HasColumn("Dokument OT"))
            {
                if (!dane["Dokument OT"].Equals(""))
                {
                    nrDokumentuOT = dane["Dokument OT"];
                }
            }

            if (dane.HasColumn("Charakterystyka"))
            {
                if (!dane["Charakterystyka"].Equals(""))
                {
                    poz.AddElementValue("Charakterystyka", dane["Charakterystyka"] + ( !nrDokumentuOT.Equals("") ? ", " + nrDokumentuOT : String.Empty));
                }
            }
            

            if (dane.HasColumn("Producent") || dane.HasColumn("Rok produkcji"))
            {
                var produkcja = poz.AddElement("Produkcja");
                if (dane.HasColumn("Producent"))
                {
                    if (!dane["Producent"].Equals(""))
                    {
                        produkcja.AddElementValue("Producent", dane["Producent"]);
                    }
                }
                if (dane.HasColumn("Rok produkcji"))
                {
                    if (!dane["Rok"].Equals(""))
                    {
                        produkcja.AddElementValue("Rok produkcji", dane["Rok produkcji"]);
                    }
                }
            }

            if ((dane.HasColumn("Dane techniczne") || dane.HasColumn("Stan techniczny")))
            {
                var techniczne = poz.AddElement("Techniczne");
                if (dane.HasColumn("Dane techniczne"))
                {
                    if(!dane["Dane techniczne"].Equals(""))
                    {
                        techniczne.AddElementValue("Dane techniczne", dane["Dane techniczne"]);
                    }
                }
                if (dane.HasColumn("Stan techniczny"))
                {
                    if (!dane["Stan techniczny"].Equals(""))
                    {
                        techniczne.AddElementValue("Stan techniczny", dane["Stan techniczny"]);
                    }
                }
            }
           
            var nabycie = poz.AddElement("Nabycie");
            nabycie.AddElementValue("Data", dataNabycia);
            if (dane.HasColumn("Wartość początkowa"))
            {
                if (!dane["Wartość początkowa"].Equals(""))
                {
                    nabycie.AddElementValue("Wartosc", dane["Wartość początkowa"]);
                }
            }else if (dane.HasColumn("Wartość nabycia"))
            {
                if (!dane["Wartość nabycia"].Equals(""))
                {
                    nabycie.AddElementValue("Wartosc", dane["Wartość nabycia"]);
                }
            }
            if (dane.HasColumn("Dokument nabycia"))
            {
                if (!dane["Dokument nabycia"].Equals(""))
                {
                    nabycie.AddElementValue("Dokument", dane["Dokument nabycia"]);
                }
            }
            if (dane.HasColumn("Sposób nabycia"))
            {
                if (!dane["Sposób nabycia"].Equals(""))
                {
                    nabycie.AddElementValue("Sposob", dane["Sposób nabycia"]);
                }
            }

            poz.AddElement("WlasneRozszerzone");
            return poz;
        }
    }
}
