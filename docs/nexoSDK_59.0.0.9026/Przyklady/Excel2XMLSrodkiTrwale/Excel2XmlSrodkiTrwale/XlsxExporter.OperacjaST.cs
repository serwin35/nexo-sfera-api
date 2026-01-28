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
        static XNamespace nsOperacjeST = "http://schemas.insert.com.pl/2013/hop/operacjest";

        public string[] ExportOperacjeST(string filename)
        {
            int lp = 0, nr = 1;
            Slownik<string, int> kolumny = new Slownik<string, int>();

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

                                ElementHelper poz = GenerujOperacjeST(dane, ++lp, ref nr);

                                if (!string.IsNullOrWhiteSpace(poz.GetXElement().Value))
                                    operacjeST.Add(poz.GetXElement());
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

            if (operacjeST != null)
            {
                info.Add(ZapiszDoPliku(sciezka, "OperacjeST.xml", operacjeST));
            }

            if (sbMsg.Length > 0)
                info.Add(sbMsg.ToString());

            return info.ToArray();
        }

        private static ElementHelper GenerujOperacjeST(CellValueGetter dane, int lp, ref int nr)
        {
            var poz = new ElementHelper(nsOperacjeST.NamespaceName, "Przyjecie", dane);
            poz.AddAttributeValue("Umarzany", 1);
            var srodekTrwaly = poz.AddElement("SrodekTrwaly");
            srodekTrwaly.AddAttributeValue("Ident", lp);
            srodekTrwaly.AddElementValue(nsSrodkiTrwale.NamespaceName, "Numer", lp);
            srodekTrwaly.AddElementValue(nsSrodkiTrwale.NamespaceName, "Nazwa", dane["Nazwa"]);
            srodekTrwaly.AddElementValue(nsSrodkiTrwale.NamespaceName, "Typ", "SrodekTrwaly");

            if (!dane["Dokument OT"].Equals(""))
            {
                poz.AddElementValue("Numer", dane["Dokument OT"]);
            }
            else
            {
                string generowanyNumer = "OT " + nr++ + "/" + DateTime.Parse(dane.GetValue<DateTime>("Data przyjęcia do użytkowania")).ToString(@"MM\/yyyy") + "[wł]";
                poz.AddElementValue("Numer", generowanyNumer);
            }
            poz.AddElementValue("Data", DateTime.Parse(dane.GetValue<DateTime>("Data przyjęcia do użytkowania")).ToString("yyyy-MM-dd"));

            var typAmortyzacji = poz.AddElement("TypAmortyzacji");
            typAmortyzacji.AddElementValue("Symbol", "POD");
            typAmortyzacji.AddElementValue("Nazwa", "Podatkowy");
            typAmortyzacji.AddElementValue("Typ", "Podatkowy");

            var uzytkowanie = poz.AddElement("Uzytkowanie");
            var miejsce = uzytkowanie.AddElement("Miejsce");
            miejsce.AddElementValue(nsMiejscaUzytkowania.NamespaceName, "Symbol", "IN001");
            miejsce.AddElementValue(nsMiejscaUzytkowania.NamespaceName, "Nazwa", "Firma Przykładowa");
            uzytkowanie.AddElementValue("Uzytkownik", "Jan Kowalski");      // według założeń ale w przyszłości należy pobierać tą dane

            if (dane.HasColumn("Wartość początkowa"))
            {
                if (!dane["Wartość początkowa"].Equals(""))
                {
                    var umarzanie = poz.AddElement("Umarzanie");
                    var wartoscPoczatkowa = string.Format("{0:0.00}", Math.Round(Double.Parse(dane["Wartość początkowa"].ToString().Replace('.', ',')), 2)).Replace(',', '.');
                    umarzanie.AddElementValue("WartoscPoczatkowa", wartoscPoczatkowa);
                    umarzanie.AddElementValue("WartoscPoczatkowaStanowiacaKoszty", wartoscPoczatkowa);
                    umarzanie.AddElementValue("MetodaUmarzania", "Liniowa");
                    umarzanie.AddElementValue("UmorzenieRoczne", (Double.Parse(dane["Stawka amortyzacji"], CultureInfo.InvariantCulture)) * 100);
                    umarzanie.AddElementValue("RozpoczecieUmarzania", "MiesiacPrzyjecia");
                    var wzorzecPlanuAmortyzacji = umarzanie.AddElement("WzorzecPlanuAmortyzacji");
                    wzorzecPlanuAmortyzacji.AddElementValue("TypPodzialu", "Miesieczny");

                    if (DateTime.Parse(dane.GetValue<DateTime>("Data przyjęcia do użytkowania")).CompareTo(new DateTime(2018, 1, 1)) < 0)
                    //(Double.Parse(dane["Dotychczasowe umożenie"], CultureInfo.InvariantCulture) - Double.Parse(dane["Wartość początkowa"], CultureInfo.InvariantCulture) >= 0
                    {
                        var historiaAmortyzacji = umarzanie.AddElement("HistoriaAmortyzacji");
                        historiaAmortyzacji.AddAttributeValue("PrzyjetyWczesniej", 1);
                        historiaAmortyzacji.AddElementValue("DataPierwszejAmortyzacjiWProgramie", "2018-01-01");
                        var amortyzacjaHistoryczna = historiaAmortyzacji.AddElement("AmortyzacjaHistoryczna");
                        amortyzacjaHistoryczna.AddAttributeValue("Miesiac", 0);
                        amortyzacjaHistoryczna.AddAttributeValue("Wartosc", string.Format("{0:0.00}", Math.Round(Double.Parse(dane["Dotychczasowe umożenie"].ToString().Replace('.', ',')), 2)).Replace(',', '.'));
                    }
                }
            }

            poz.AddElement("WlasneRozszerzone");
            return poz;
        }
    }
}
