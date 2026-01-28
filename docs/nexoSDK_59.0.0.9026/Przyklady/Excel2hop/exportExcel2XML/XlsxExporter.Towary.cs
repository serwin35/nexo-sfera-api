using System;
using System.Xml.Linq;
using System.Xml;
using System.Diagnostics;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;

namespace exportExcel2XML
{
    public partial class XlsxExporter
    {
        static XNamespace nsTowary = "http://schemas.insert.com.pl/2018/hop/towary";
        static XNamespace nsTowaryParam = "http://schemas.insert.com.pl/2015/hop/towary";

        public string[] ExportujTowary(string filename)
        {
            Slownik<string, int> kolumny = new Slownik<string, int>();

            XElement towary =
                new XElement(nsTowary + "Asortyment",
                    new XAttribute(XNamespace.Xmlns + "w", nsHop)
                );

            XElement parametry = null;
            XElement flagiWlasneXML = null;
            XElement mojaFirmaXML = null;

            StringBuilder sbMsg = new StringBuilder();

            try
            {
                using (SpreadsheetDocument spreadsheetDocument = SpreadsheetDocument.Open(filename, false))
                {
                    IEnumerable<Row> rowcollection = GetSpreadsheetRows(spreadsheetDocument);
                    if (rowcollection.Count() == 0) // jesli pusto to nic nie zapisujemy do plików
                        return null;

                    kolumny = GetColumns(spreadsheetDocument, rowcollection);

                    Slownik<string, bool> polaWlasne = new Slownik<string, bool>();
                    var listaFlagWlasnych = new List<Tuple<string, string, string>>(); //flagi wlasnie

                    List<string> listaMagazynow = new List<string>();
                    listaMagazynow = kolumny.Where(k => k.Key.StartsWith("stan min ")).Select(k => k.Key.Substring(9)).ToList();

                    IEnumerator<Row> enumerator = rowcollection.GetEnumerator();
                    Row wiersz = null;
                    if (enumerator.MoveNext())
                        wiersz = enumerator.Current;
                    while (wiersz != null)
                    {
                        while (wiersz.RowIndex.Value < 3)
                        {
                            if (!enumerator.MoveNext())
                                break;
                            wiersz = enumerator.Current;
                        }

                        var cells = GetCells(wiersz, kolumny, sbMsg);

                        CellValueGetter dane = new CellValueGetter(spreadsheetDocument, cells, kolumny);

                        ElementHelper poz = null;

                        if (cells != null)
                            poz = GenerujPozycjeAsortymentu(polaWlasne, listaFlagWlasnych, listaMagazynow, dane);

                        ElementHelper skladniki = null;
                        if (cells != null && dane["Rodzaj"] == "KT")
                        {
                            do
                            {
                                GenerujSkladniki(dane, ref skladniki);

                                if (enumerator.MoveNext())
                                {
                                    wiersz = enumerator.Current;
                                    cells = wiersz.Descendants<Cell>().ToArray();
                                    dane = new CellValueGetter(spreadsheetDocument, cells, kolumny);
                                }
                                else
                                {
                                    wiersz = null;
                                    break;
                                }
                            } while (!string.IsNullOrWhiteSpace(dane["Składniki kompletu - Symbol składnika"]) &&
                                string.IsNullOrWhiteSpace(dane["Symbol"]));
                        }
                        else if (enumerator.MoveNext())
                            wiersz = enumerator.Current;
                        else
                            wiersz = null;

                        if (poz != null)
                        {
                            if (skladniki != null)
                                poz.Append(skladniki);

                            if (!string.IsNullOrWhiteSpace(poz.GetXElement().Value))
                                towary.Add(poz.GetXElement());
                        }
                    }

                    if (polaWlasne.Any())
                    {
                        parametry =
                            new XElement(nsTowaryParam + "ParametryTowarow",
                                new XAttribute(XNamespace.Xmlns + "w", nsHop));

                        XElement wlasne = new XElement(nsTowaryParam + "Wlasne");
                        parametry.Add(wlasne);
                        foreach (var pole in polaWlasne)
                        {
                            wlasne.Add(new XElement(nsTowaryParam + "Pole", pole.Key));
                        }
                    }

                    if (listaFlagWlasnych.Any()) //flagi wlasne
                    {
                        flagiWlasneXML = GenerujXmlFlagiWlasne(Path.GetDirectoryName(filename), listaFlagWlasnych);
                    }

                    if (listaMagazynow.Any())
                        mojaFirmaXML = GenerujXmlMojaFirma(Path.GetDirectoryName(filename), listaMagazynow);
                }
            }
            catch (IOException ex)
            {
                throw new IOException(ex.Message);
            }

            List<string> info = new List<string>();

            string sciezka = Path.GetDirectoryName(filename);
            if (towary != null)
            {
                info.Add(ZapiszDoPliku(sciezka, "Towary.xml", towary));
            }
            if (parametry != null)
            {
                info.Add(ZapiszDoPliku(sciezka, "ParametryTowarow.xml", parametry));
            }
            if (flagiWlasneXML != null) //flagi wlasne
            {
                info.Add(ZapiszDoPliku(sciezka, "FlagiWlasne.xml", flagiWlasneXML));
            }
            if (mojaFirmaXML != null)
            {
                info.Add(ZapiszDoPliku(sciezka, "MojaFirma.xml", mojaFirmaXML));
            }

            if (sbMsg.Length > 0)
                info.Add(sbMsg.ToString());

            return info.ToArray();
        }

        private static void GenerujSkladniki(CellValueGetter dane, ref ElementHelper skladniki)
        {
            string symbolSkladnika = dane["Składniki kompletu - Symbol składnika"];
            string jednostkaMiarySkladnika = dane["Składniki kompletu - Jednostka miary"];
            string iloscSkladnika = dane["Składniki kompletu - Ilość"];

            if (!string.IsNullOrWhiteSpace(symbolSkladnika))
            {
                if (skladniki == null)
                    skladniki = new ElementHelper(nsTowary.NamespaceName, "Skladniki", dane);
                var skladnik = skladniki.AddElement("Skladnik");
                skladnik.AddElementValue("Symbol", symbolSkladnika);
                skladnik.AddElementValue("Liczba", iloscSkladnika);
                skladnik.AddElementValue("Jm", jednostkaMiarySkladnika);
            }
        }

        private static ElementHelper GenerujPozycjeAsortymentu(Slownik<string, bool> polaWlasne, List<Tuple<string, string, string>> listaFlagWlasnych, List<string> magazyny, CellValueGetter dane)
        {
            var poz = new ElementHelper(nsTowary.NamespaceName, "Pozycja", dane);
            poz.AddAttribute("Rodzaj", "Rodzaj");
            poz.AddElement("Sygnatura", "Symbol");
            poz.AddElement("Nazwa", "Nazwa");
            poz.AddElement("JmSprzedazy", "jedn przy sprz");
            poz.AddElement("JmZakupu", "jedn przy zak");
            poz.AddElement<bool>("OtwartaCena", "Otwarta cena w kasie");

            bool mamPKWIU2008 = dane.HasColumn("PKWiU 2008");
            bool mamPKWIU2015 = dane.HasColumn("PKWiU 2015");
            if (mamPKWIU2008)
            {
                var pkwiu = poz.AddElement("PKWIU", "PKWiU 2008");
                pkwiu.AddAttributeValue("Klasyfikacja", "PKWIU2008");
            }
            else if (mamPKWIU2015)
            {
                var pkwiu = poz.AddElement("PKWIU", "PKWiU 2015");
                pkwiu.AddAttributeValue("Klasyfikacja", "PKWIU2015");
            }
            poz.AddElement<bool>("Wazony", "Ważony na wadze");
            poz.AddElement<bool>("ObrotMarza", "Czy VAT marża");
            poz.AddElement("Opis", "Opis");
            poz.AddElement<bool>("SklepInternet", "Sklep internetowy");
            poz.AddElement<bool>("SerwisAukcyjny", "Serwis aukcyjny");
            poz.AddElement<bool>("SprzedazMobilna", "Sprzedaż mobilna");

            var elemVatSprzedaz = poz.AddElement("StawkaVatSprzedaz");
            elemVatSprzedaz.AddElement("Symbol", "Stawka VAT sprzedaż");
            elemVatSprzedaz.AddElement("Nazwa", "Stawka VAT sprzedaż");
            elemVatSprzedaz.AddElement("Wartosc", "Stawka VAT sprzedaż");

            var elemVatZakup = poz.AddElement("StawkaVatZakup");
            elemVatZakup.AddElement("Symbol", "Stawka VAT zakup");
            elemVatZakup.AddElement("Nazwa", "Stawka VAT zakup");
            elemVatZakup.AddElement("Wartosc", "Stawka VAT zakup");

            var elemJednostki = poz.AddElement("JednostkiMiar");
            var elemPodstJm = elemJednostki.AddElement("PodstJm");
            elemPodstJm.AddElement("Symbol", "Podstawowa jednostka miary - symbol");
            elemPodstJm.AddElement("PodstKodKreskowy", "Podstawowa jednostka miary - kod", true);
            elemPodstJm.AddElement("NazwaDlaUf", "Podstawowa jednostka miary - nazwa dla uf");
            elemPodstJm.AddElement("PLU", "Podstawowa jednostka miary - kod plu");
            elemPodstJm.AddElement<bool>("WysylajNaUrz", "Podstawowa jednostka miary - wysyłaj na uf");
            var dodJm = dane["Dodatkowa jednostka miary - symbol"];
            if (!string.IsNullOrWhiteSpace(dodJm))
            {
                var elemDodJm = elemJednostki.AddElement("Jm");
                elemDodJm.AddElement("Symbol", "Dodatkowa jednostka miary - symbol");
                elemDodJm.AddElement("PodstKodKreskowy", "Dodatkowa jednostka miary - kod", true);
                elemDodJm.AddElement("NazwaDlaUf", "Dodatkowa jednostka miary - nazwa dla uf", true);
                elemDodJm.AddElement("PLU", "Dodatkowa jednostka miary - kod plu");
                elemDodJm.AddElement<bool>("WysylajNaUrz", "Dodatkowa jednostka miary - wysyłaj na uf");
                elemDodJm.AddElementValue("Ilosc", "1");
                elemDodJm.AddElement<decimal>("IloscNadrzJm", "Dodatkowa jednostka miary - Ilość nadrz jm");
                elemDodJm.AddElement("NadrzednaJm", "Dodatkowa jednostka miary - jednostka nadrz");
            }
            poz.AddElement("PorownawczaJm", "Porównawcza jednostka miary", true);
            poz.AddElement<decimal>("CenaEwidencyjna", "cena ewidencyjna", true);

            if (dane["Rodzaj"] != "US")
            {
                var stany = poz.AddElement("Stany");
                foreach (string symbol in magazyny)
                {
                    var mag = stany.AddElement("Magazyn");
                    mag.AddElementValue("Symbol", symbol);
                    var stan = mag.AddElement("Stan");
                    stan.AddAttributeValue("Rodzaj", "min");
                    stan.AddElement<decimal>("Ilosc", "stan min " + symbol);
                }
            }

            var producent = dane["Producent - Symbol"];
            if (!string.IsNullOrWhiteSpace(producent))
            {
                var prod = poz.AddElement("Producent");
                prod.AddElementValue("Sygnatura", producent);
                prod.AddElement("SymbolAsortymentu", "Producent - Symbol u producenta", true);
            }

            var dostawca = dane["Podst. Dostawca - Symbol"];
            if (!string.IsNullOrWhiteSpace(dostawca))
            {
                var dosts = poz.AddElement("Dostawcy");
                var dost = dosts.AddElement("Dostawca");
                dost.AddElementValue("Sygnatura", dostawca);
                dost.AddElement("SymbolAsortymentu", "Podst. Dostawca - Symbol u dostawcy", true);
            }

            poz.AddElement("Powiazany", "asort. powiązany", true);
            poz.AddElement("Uwagi", "uwagi", true);
            poz.AddElement("Charakterystyka", "charakterystyka pełna", true);
            poz.AddElement("Grupa", "grupa");
            var cechy = poz.AddElement("Cechy");
            cechy.AddElement("Cecha", "cecha 1", true);
            cechy.AddElement("Cecha", "cecha 2", true);
            var zdjecie = dane["zdjęcie"];
            if (!string.IsNullOrWhiteSpace(zdjecie))
            {
                var zdj = poz.AddElement("Zdjecia");
                zdj.AddElementValue("Zdjecie", zdjecie);
            }

            var flagaWlasnaKolor = dane["Flaga - Kolor"];
            if (!string.IsNullOrWhiteSpace(flagaWlasnaKolor))
            {
                var flaga = poz.AddElement("FlagaWlasna");
                flaga.AddElementValue("Kolor", flagaWlasnaKolor);
                listaFlagWlasnych.Add(new Tuple<string, string, string>(dane["Flaga - Nazwa"], dane["Flaga - Kolor"], "Towary i uslugi")); //flagi wlasne
            }

            var dok = dane["Biblioteka dokumentów - Nazwa"];
            if (!string.IsNullOrWhiteSpace(dok))
            {
                var biblioteka = poz.AddElement("BibliotekaDokumentow");
                ElementHelper element = biblioteka.AddElementHelper(nsHop.NamespaceName, "ElementBibliotekiDokumentow", dane);
                element.AddElementValue("Nazwa", dok);
                element.AddElementValue("Typ", Path.GetExtension(dane["Biblioteka dokumentów - Plik"]));
                element.AddElement("Plik", "Biblioteka dokumentów - Plik");
            }

            string kontrolaTW = dane.GetValue<bool>("Kontrola terminu ważności - czy kontrolowany");
            if (kontrolaTW == "1")
                poz.AddElement("KontrolaTW", "Kontrola terminu ważności - ilość dni");

            poz.AddElement("StronaWww", "Strona WWW");

            string poleWlasneNazwa = dane["Pola własne - nazwa"];
            if (!string.IsNullOrWhiteSpace(poleWlasneNazwa) && poleWlasneNazwa != "-")
            {
                var wlasne = poz.AddElement("Wlasne");
                var pole = wlasne.AddElement("Pole", "Pola własne - wartość");
                pole.AddAttribute("Nazwa", "Pola własne - nazwa");
                polaWlasne[poleWlasneNazwa] = false;
            }
            return poz;
        }

        static XNamespace nsStany = "http://schemas.insert.com.pl/2013/hop/stanymagazynowe";
        
        internal string[] ExportStanyTowarow(string filename)
        {
            Slownik<string, int> kolumny = new Slownik<string, int>();

            XElement stany = new XElement(nsStany + "StanyMagazynowe");

            XElement mojaFirma = null;

            StringBuilder sbMsg = new StringBuilder();

            try
            {
                IEnumerable<string> magazyny = null;
                using (SpreadsheetDocument spreadsheetDocument = SpreadsheetDocument.Open(filename, false))
                {
                    IEnumerable<Row> rowcollection = GetSpreadsheetRows(spreadsheetDocument);
                    if (rowcollection.Count() == 0) // jesli pusto to nic nie zapisujemy do plików
                        return null;

                    kolumny = GetColumns(spreadsheetDocument, rowcollection);

                    Slownik<string, bool> polaWlasne = new Slownik<string, bool>();
                    var listaFlagWlasnych = new List<Tuple<string, string, string>>(); //flagi wlasnie

                    IEnumerator<Row> enumerator = rowcollection.GetEnumerator();
                    Row row = null;

                    Dictionary<string, ElementHelper> stanyMagazynow = new Dictionary<string, ElementHelper>();
                    while (enumerator.MoveNext())
                    {
                        row = enumerator.Current;
                        while (row.RowIndex < 3)
                        {
                            if (!enumerator.MoveNext())
                                break;
                            row = enumerator.Current;
                        }

                        var cells = GetCells(row, kolumny, sbMsg);
                        if (cells == null)
                            continue;

                        CellValueGetter dane = new CellValueGetter(spreadsheetDocument, cells, kolumny);

                        DogenerujStanMagazynowy(stany, stanyMagazynow, dane);
                    }
                    magazyny = stanyMagazynow.Keys;
                }
                
                if (magazyny != null && magazyny.Any()) // musimy wyeksportowac informacje o magazynach
                {
                    mojaFirma = GenerujXmlMojaFirma(Path.GetDirectoryName(filename), magazyny);
                }
            }
            catch (IOException ex)
            {
                throw new IOException(ex.Message);
            }

            List<string> pliki = new List<string>();
            string sciezka = Path.GetDirectoryName(filename);

            if (stany != null)
            {
                pliki.Add(ZapiszDoPliku(sciezka, "StanyMagazynowe.xml", stany));
            }
            if (mojaFirma != null)
            {
                pliki.Add(ZapiszDoPliku(sciezka, "MojaFirma.xml", mojaFirma));
            }

            if (sbMsg.Length > 0)
                pliki.Add(sbMsg.ToString());

            return pliki.ToArray();
        }

        //private static ElementHelper UtworzMojaFirma(IEnumerable<string> magazyny)
        //{
        //    ElementHelper mf =
        //        new ElementHelper(nsMojaFirma.NamespaceName, "MojaFirma", null);
        //    mf.GetXElement().Add(new XAttribute(XNamespace.Xmlns + "w", nsHop));
        //    mf.AddElementValue("NIP", "1111111111");
        //    var adresy = mf.AddElement("Adresy");
        //    var adres = adresy.AddElementHelper(nsHop.NamespaceName, "Adres", null);
        //    adres.AddAttributeValue("Rodzaj", "Glowny");
        //    var szczegoly = adres.AddElement("AdresSzczegoly");
        //    szczegoly.AddElementValue("Miasto", "Wrocław");
        //    szczegoly.AddElementValue("Ulica", "Jerzmanowska");
        //    szczegoly.AddElementValue("Panstwo", "Polska");
        //    mf.AddElement("Cenniki");
        //    var dane = mf.AddElement("DaneFirmy");
        //    dane.AddElementValue("Nazwa", "Moja firma");
        //    dane.AddElementValue("NazwaSkrocona", "Moja firma");

        //    ElementHelper magi = mf.AddElement("Magazyny");
        //    foreach (var mag in magazyny)
        //    {
        //        ElementHelper magazyn = magi.AddElement("Magazyn");
        //        magazyn.AddElementValue("Symbol", mag);
        //        magazyn.AddElementValue("Nazwa", mag);
        //        magazyn.AddElementValue("Opis", mag);
        //        magazyn.AddElement("Adresy");
        //    }
        //    var model = mf.AddElement("ModelOrganizacyjny");
        //    var centrala = model.AddElement("Centrala");
        //    centrala.AddElementValue("Symbol", "MOJA FIRMA");
        //    return mf;
        //}

        private static void DogenerujStanMagazynowy(XElement stany, Dictionary<string, ElementHelper> stanyMagazynow, CellValueGetter dane)
        {
            string symbolMagazynu = dane["Magazyn"];
            ElementHelper stanMagazynu;
            if (!stanyMagazynow.TryGetValue(symbolMagazynu, out stanMagazynu))
            {
                stanMagazynu = new ElementHelper(nsStany.NamespaceName, "StanMagazynu", dane);
                ElementHelper mag = stanMagazynu.AddElement("Magazyn");
                mag.AddAttributeValue("Symbol", symbolMagazynu);
                stany.Add(stanMagazynu.GetXElement());

                stanyMagazynow[symbolMagazynu] = stanMagazynu;
            }
            string symbolTowaru = dane["Symbol towaru"];
            ElementHelper aso = null;
            XElement asortymentElement = stanMagazynu.GetXElement().Elements(XName.Get("Asortyment", nsStany.NamespaceName)).Where(e => e.Attribute("Symbol").Value == symbolTowaru).FirstOrDefault();
            if (asortymentElement == null)
            {
                aso = new ElementHelper(nsStany.NamespaceName, "Asortyment", dane);
                aso.AddAttributeValue("Symbol", symbolTowaru);
                stanMagazynu.Append(aso);
            }
            else
            {
                aso = new ElementHelper(asortymentElement, nsStany.NamespaceName, dane);
            }
            ElementHelper dostawa = aso.AddElement("Dostawa");
            dostawa.AddAttribute("Ilosc", "Ilość");
            dostawa.AddAttribute<decimal>("CenaNetto", "Cena netto", true);
            dostawa.AddAttribute("Numer", "Kod dostawy", true);
            dostawa.AddAttribute("Opis", "Opis dostawy", true);
            dostawa.AddAttribute<DateTime>("TerminWaznosci", "Termin ważności", true);
        }

    }
}