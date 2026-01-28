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
using System.Windows;
using System.Globalization;
using System.Text;

namespace exportExcel2XML
{
    public partial class XlsxExporter
    {
        static XNamespace nsHop = "http://schemas.insert.com.pl/2013/hop";
        static XNamespace nsRachunki = "http://schemas.insert.com.pl/2013/hop/rachunkibankowe";
        static XNamespace nsKlienci = "http://schemas.insert.com.pl/2018/hop/klienci";
        static XNamespace nsKlienciParametry = "http://schemas.insert.com.pl/2018/hop/klienci";

        public string[] ExportKlienci(string filename)
        {
            Slownik<string, int> kolumny = new Slownik<string, int>();

            XElement klienci =
                new XElement(nsKlienci + "Podmioty",
                    new XAttribute(XNamespace.Xmlns + "w", nsHop),
                    new XAttribute(XNamespace.Xmlns + "r", nsRachunki)
                        );

            XElement parametryKlientow = null;

            StringBuilder sbMsg = new StringBuilder();

            try
            {
                using (SpreadsheetDocument arkuszKalkulacyjny = OpenSpreadsheetDocument(filename))
                {
                    IEnumerable<Row> kolekcjaWierszy = GetSpreadsheetRows(arkuszKalkulacyjny);
                    if (kolekcjaWierszy.Count() == 0) //jeśli brak danych to plik nie będzie zapisywany
                        return null;

                    kolumny = GetColumns(arkuszKalkulacyjny, kolekcjaWierszy); // wszystkie wiersze z danymi z pliku excel są wrzucane do słownika

                    string pwPrefiks = "Pole własne - ";
                    List<string> polaWlasneKlientow = kolumny
                        .Where(k => k.Key.StartsWith(pwPrefiks))
                        .Select(k => k.Key.Substring(pwPrefiks.Length))
                        .ToList();

                    IEnumerator<Row> enumerator = kolekcjaWierszy.GetEnumerator(); //definicja wskaźnika kolejnych elementów słownika wierszy (klientów)
                    Row wiersz = null;

                    while (enumerator.MoveNext()) //wykonywane dopóki nie trafi się pusty wiersz
                    {
                        wiersz = enumerator.Current;
                        while (wiersz.RowIndex < 3) //pominięcie dwóch pierwszych wierszy nagłówkowych
                        {
                            if (!enumerator.MoveNext())
                                break;
                            wiersz = enumerator.Current;
                        }

                        var cells = GetCells(wiersz, kolumny, sbMsg);

                        if (cells != null)
                        {
                            CellValueGetter dane = new CellValueGetter(arkuszKalkulacyjny, cells, kolumny);

                            ElementHelper poz = GenerujKontrahenta(polaWlasneKlientow, dane);

                            if (!string.IsNullOrWhiteSpace(poz.GetXElement().Value))
                                klienci.Add(poz.GetXElement());
                        }
                    }

                    if (polaWlasneKlientow.Any())
                    {
                        parametryKlientow =
                            new XElement(nsKlienciParametry + "ParametryPodmiotow",
                                new XAttribute(XNamespace.Xmlns + "w", nsHop));

                        XElement wlasne = new XElement(nsKlienciParametry + "Wlasne");
                        parametryKlientow.Add(wlasne);
                        foreach (var pole in polaWlasneKlientow)
                        {
                            wlasne.Add(new XElement(nsKlienciParametry + "Pole", pole));
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

            if (klienci != null)
            {
                info.Add(ZapiszDoPliku(sciezka, "Kontrahenci.xml", klienci));
            }
            if (parametryKlientow != null)
            {
                info.Add(ZapiszDoPliku(sciezka, "ParametryKontrahentow.xml", parametryKlientow));
            }
            
            if (sbMsg.Length > 0)
                info.Add(sbMsg.ToString());

            return info.ToArray();
        }

        private static ElementHelper GenerujKontrahenta(List<string> polaWlasneKlientow, CellValueGetter dane)
        {
            var poz = new ElementHelper(nsKlienci.NamespaceName, "Podmiot", dane);
            poz.AddAttribute("Typ", "Typ");
            poz.AddAttributeValue("Dostawca", "1");
            poz.AddAttributeValue("Odbiorca", "1");
            poz.AddElement("Sygnatura", "Symbol");

            if (dane["Typ"] == "Firma")
            {
                var dane_firmy = poz.AddElement("DaneFirmy");
                dane_firmy.AddElement("Nazwa", "Dane firmy - Nazwa");
                dane_firmy.AddElement("NazwaSkrocona", "Dane firmy - Nazwa skrócona");
                dane_firmy.AddElement("REGON", "Dane firmy - REGON");
            }
            else if (dane["Typ"] == "Osoba")
            {
                var dane_osoba = poz.AddElement("DaneOsoby");
                dane_osoba.AddElement("Imie", "Dane osoby - Imię");
                dane_osoba.AddElement("Nazwisko", "Dane osoby - Nazwisko");
                dane_osoba.AddElement("PrzedstawicielFirmy")
                    .AddElement("Firma", "Dane osoby - Przedstawiciel firmy");
            }

            poz.AddElement("NIP", "NIP");
            var UE = poz.AddElement("UE");
            UE.AddElement(nsHop.NamespaceName, "NIPUE", "Podatnik UE - NIPUE");
            UE.AddElement(nsHop.NamespaceName, "PanstwoUE", "Podatnik UE - PanstwoUE");

            var wartoscEMail = dane["Kontakty - e-mail"];
            var wartoscTel = dane["Kontakty - telefon"];
            if (!string.IsNullOrWhiteSpace(wartoscEMail) && !string.IsNullOrWhiteSpace(wartoscTel))
            {
                var kontakty = poz.AddElement("Kontakty");
                if (!string.IsNullOrWhiteSpace(wartoscEMail))
                {
                    var kontaktEmail = kontakty.AddElementHelper(nsHop.NamespaceName, "Kontakt", dane);
                    kontaktEmail.AddAttributeValue("Podstawowy", "1");
                    kontaktEmail.AddAttributeValue("Typ", "Email");
                    kontaktEmail.AddElementValue("Wartosc", wartoscEMail);
                    kontaktEmail.AddElementValue("Komentarz", "Domyślny e-mail");
                }
                if (!string.IsNullOrWhiteSpace(wartoscTel))
                {
                    var kontaktTel = kontakty.AddElementHelper(nsHop.NamespaceName, "Kontakt", dane);
                    kontaktTel.AddAttributeValue("Podstawowy", "1");
                    kontaktTel.AddAttributeValue("Typ", "Telefon");
                    kontaktTel.AddElementValue("Wartosc", wartoscTel);
                    kontaktTel.AddElementValue("Komentarz", "Domyślny telefon");
                }
            }
            var adresy = poz.AddElement("Adresy");
            var adres = adresy.AddElementHelper(nsHop.NamespaceName, "Adres", dane);
            adres.AddAttributeValue("Rodzaj", "Glowny");

            var adres_szcz = adres.AddElement("AdresSzczegoly");
            adres_szcz.AddElement("Panstwo", "Adres - Państwo");
            adres_szcz.AddElement("Miasto", "Adres - Miejscowość");
            adres_szcz.AddElement("Ulica", "Adres - Ulica");
            adres_szcz.AddElement("NrDomu", "Adres - Nr domu");
            if (dane.HasColumn("Adres - Nr lokalu"))
                adres_szcz.AddElement("NrLokalu", "Adres - Nr lokalu", true);
            adres_szcz.AddElement("Wojewodztwo", "Adres - Województwo");
            adres_szcz.AddElement("Kod", "Adres - Kod pocztowy");

            poz.AddElement("Grupa", "Grupa");

            string[] cechy = dane["Cechy"].Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (cechy.Length > 0)
            {
                var cechy_el = poz.AddElement("Cechy");
                foreach (var cecha in cechy)
                    cechy_el.AddElementValue("Cecha", cecha.Trim());
            }

            if (!string.IsNullOrWhiteSpace(dane["Rachunek bankowy - Numer"]))
            {
                var rachunki_bank = poz.AddElement("RachunkiBankowe");
                var rachunek_bank = rachunki_bank.AddElement("Rachunek");
                rachunek_bank.AddElement(nsRachunki.NamespaceName, "Nazwa", "Rachunek bankowy - Nazwa");
                rachunek_bank.AddElement(nsRachunki.NamespaceName, "Numer", "Rachunek bankowy - Numer");
                rachunek_bank.AddElement(nsRachunki.NamespaceName, "Waluta", "Rachunek bankowy - Waluta");
            }

            if (polaWlasneKlientow != null && polaWlasneKlientow.Count > 0)
            {
                var wlasne = poz.AddElement("Wlasne");
                foreach (var nazwaPola in polaWlasneKlientow)
                {
                    string wartoscPola = dane["Pole własne - " + nazwaPola];
                    if (!string.IsNullOrWhiteSpace(wartoscPola))
                    {
                        var pole = wlasne.AddElementValue("Pole", wartoscPola);
                        pole.AddAttributeValue("Nazwa", nazwaPola);
                    }
                }
            }

            var zgodaMarketing = poz.AddElement("ZgodaMarketing"); //źle zrobione
            zgodaMarketing.AddAttribute<bool>("Jest", "Zgoda na informacje marketingowe");

            if (dane["Typ"] == "Firma")
            {
                var kredyt = poz.AddElement("Kredyt");
                kredyt.AddAttribute<bool>("Zezwalaj", "Kredyt - Zgoda na kredyt");
                kredyt.AddElement("Forma", "Kredyt - Forma kredytu kupieckiego");
                kredyt.AddElement("MaxLiczbaDok", "Kredyt - Liczba niespłaconych dokumentów");
                kredyt.AddElement("MaxLiczbaDniSp", "Kredyt - Liczba dni spóźnienia");
                kredyt.AddElement("Domyslny", "Kredyt - Domyślny kredyt kupiecki");
                kredyt.AddElement("Max", "Kredyt - Wartość całego kredytu");
            }
            return poz;
        }

        static XNamespace nsRozrachunki = "http://schemas.insert.com.pl/2014/hop/rozrachunki";

        public string[] ExportRozrachunki(string filename)
        {
            Slownik<string, int> kolumny = new Slownik<string, int>();

            XElement flagiWlasneXML = null;

            XElement rozrachunki =
                new XElement(nsRozrachunki + "Rozrachunki");

            StringBuilder sbMsg = new StringBuilder();

            try
            {
                using (SpreadsheetDocument arkuszKalkulacyjny = OpenSpreadsheetDocument(filename))
                {
                    var listaFlagWlasnych = new List<Tuple<string, string, string>>(); //flagi wlasnie

                    IEnumerable<Row> kolekcjaWierszy = GetSpreadsheetRows(arkuszKalkulacyjny);
                    if (kolekcjaWierszy.Count() == 0) //jeśli brak danych to plik nie będzie zapisywany
                        return null;

                    kolumny = GetColumns(arkuszKalkulacyjny, kolekcjaWierszy); // wszystkie wiersze z danymi z pliku excel są wrzucane do słownika

                    IEnumerator<Row> enumerator = kolekcjaWierszy.GetEnumerator(); //definicja wskaźnika kolejnych elementów słownika wierszy (rozrachunków)
                    Row wiersz = null;
                    while (enumerator.MoveNext())
                    {
                        wiersz = enumerator.Current;
                        while (wiersz.RowIndex < 3)
                        {
                            if (!enumerator.MoveNext())
                                break;
                            wiersz = enumerator.Current;
                        }

                        var cells = GetCells(wiersz, kolumny, sbMsg);

                        if (cells != null)
                        {
                            CellValueGetter dane = new CellValueGetter(arkuszKalkulacyjny, cells, kolumny);

                            ElementHelper poz = GenerujRozrachunek(listaFlagWlasnych, dane);

                            if (!string.IsNullOrWhiteSpace(poz.GetXElement().Value))
                                rozrachunki.Add(poz.GetXElement());
                        }
                    }

                    if (listaFlagWlasnych.Count != 0) //flagi wlasne
                    {
                        flagiWlasneXML = GenerujXmlFlagiWlasne(Path.GetDirectoryName(filename), listaFlagWlasnych);
                    }
                }
            }
            catch (IOException exception)
            {

                throw new IOException(exception.Message);
            }

            List<string> info = new List<string>();

            string sciezka = Path.GetDirectoryName(filename);
            if (rozrachunki != null)
            {
                info.Add(ZapiszDoPliku(sciezka, "Rozrachunki.xml", rozrachunki));
            }
            if (flagiWlasneXML != null) //flagi wlasne
            {
                info.Add(ZapiszDoPliku(sciezka, "FlagiWlasne.xml", flagiWlasneXML));
            }
            
            if (sbMsg.Length > 0)
                info.Add(sbMsg.ToString());

            return info.ToArray();
        }

        private static ElementHelper GenerujRozrachunek(List<Tuple<string, string, string>> listaFlagWlasnych, CellValueGetter dane)
        {
            ElementHelper poz = new ElementHelper(nsRozrachunki.NamespaceName, "Rozrachunek", dane);
            poz.AddAttribute("Rodzaj", "Rodzaj");
            poz.AddElement("Dokument", "Dokument");
            poz.AddElement("Podmiot", "Podmiot");
            poz.AddElement<DateTime>("DataDokumentu", "Data dokumentu");
            poz.AddElement<DateTime>("DataPowstania", "Data dokumentu");
            poz.AddElement<decimal>("Kwota", "Kwota");
            poz.AddElement<decimal>("KwotaPozostala", "Kwota pozostała");
            poz.AddElement("Waluta", "Waluta");
            poz.AddElement<DateTime>("TerminPlatnosci", "Termin płatności", true);
            var odsetki = poz.AddElement("Odsetki");
            odsetki.AddAttribute("RodzajOdsetek", "Odsetki - Rodzaj", true);
            odsetki.AddAttribute<decimal>("StopaOdsetek", "Odsetki - Stopa", true);

            var kwota = decimal.Parse(dane.GetValue<decimal>("Kwota"), NumberStyles.Number, NumberFormatInfo.InvariantInfo);
            var kwotaPozostala = decimal.Parse(dane.GetValue<decimal>("Kwota pozostała"), NumberStyles.Number, NumberFormatInfo.InvariantInfo);
            if (kwota != kwotaPozostala)
            {
                var historia = poz.AddElement("HistoriaRozliczen");
                var rozl = historia.AddElement("Rozliczenie");
                rozl.AddElementValue("Rodzaj", "S" + dane["Rodzaj"].Split('(')[0]);
                rozl.AddElement<DateTime>("DataDokumentu", "Data dokumentu");
                rozl.AddElement<DateTime>("DataPowstania", "Data dokumentu");
                rozl.AddElementValue("Dokument", "spłacona część");
                rozl.AddElementValue("Kwota", kwota - kwotaPozostala);
            }

            var flagaWlasnaKolor = dane["Flaga - Kolor"];
            if (!string.IsNullOrWhiteSpace(flagaWlasnaKolor))
            {
                var flaga = poz.AddElement("FlagaWlasna");
                flaga.AddElementValue("Kolor", flagaWlasnaKolor);
                listaFlagWlasnych.Add(new Tuple<string, string, string>(dane["Flaga - Nazwa"], dane["Flaga - Kolor"], "Rozrachunki")); //flagi wlasne
            }
            return poz;
        }

    }
}
