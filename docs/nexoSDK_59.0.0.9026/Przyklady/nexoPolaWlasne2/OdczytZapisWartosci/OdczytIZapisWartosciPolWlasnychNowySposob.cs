using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Narzedzia.PolaWlasne2;

namespace RozwiazanieWlasne
{
    public class OdczytIZapisWartosciPolWlasnychNowySposob : IOdczytIZapisWartosciPolWlasnychTester
    {
        private readonly IPolaWlasneAdv2Accessor _asoPW2Accessor;

        public OdczytIZapisWartosciPolWlasnychNowySposob(IPolaWlasneAdv2Accessor asoPW2Accessor)
        {
            if (asoPW2Accessor == null)
            {
                throw new ArgumentNullException("asoPW2Accessor");
            }

            _asoPW2Accessor = asoPW2Accessor;
        }

        #region Obsluga zdarzenia PropertyChanged

        private void PolaWlasneAccessor_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //Nazwa wlasciwosci przekazywana jako e.PropertyName to nazwa pola wlasnego
            string nazwaPola = e.PropertyName;
        }

        public void PodpiecieDoZdarzeniaPropertyChanged()
        {
            _asoPW2Accessor.PropertyChanged += PolaWlasneAccessor_PropertyChanged;
        }

        public void OdpiecieOdZdarzeniaPropertyChanged()
        {
            _asoPW2Accessor.PropertyChanged -= PolaWlasneAccessor_PropertyChanged;
        }

        #endregion

        public void OdczytIZapisWartosciPolaTypuTekst()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuTekst;

            string biezacaWartosc = _asoPW2Accessor.PobierzWartoscTypuTekst(nazwaPola); //odczyt
            _asoPW2Accessor.UstawWartoscTypuTekst(nazwaPola, "abcd"); //zapis
        }

        public void OdczytIZapisWartosciPolaTypuDlugiTekst()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuDlugiTekst;

            string biezacaWartosc = _asoPW2Accessor.PobierzWartoscTypuTekst(nazwaPola); //odczyt

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("Pierwsza linijka tekstu przykładowego.");
            stringBuilder.AppendLine();
            stringBuilder.Append("Druga linijka tekstu przykładowego.");
            _asoPW2Accessor.UstawWartoscTypuTekst(nazwaPola, stringBuilder.ToString()); //zapis
        }

        public void OdczytIZapisWartosciPolaTypuLiczbaCalkowita()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuLiczbaCalkowita;

            int? biezacaWartosc = _asoPW2Accessor.PobierzWartoscTypuLiczbaCalkowita(nazwaPola); //odczyt
            _asoPW2Accessor.UstawWartoscTypuLiczbaCalkowita(nazwaPola, 5); //zapis
        }

        public void OdczytIZapisWartosciPolaTypuLiczbaRzeczywista()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuLiczbaRzeczywista;

            decimal? biezacaWartosc = _asoPW2Accessor.PobierzWartoscTypuLiczbaRzeczywista(nazwaPola); //odczyt
            _asoPW2Accessor.UstawWartoscTypuLiczbaRzeczywista(nazwaPola, 1.23m); //zapis
        }

        public void OdczytIZapisWartosciPolaTypuWartoscLogiczna()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuWartoscLogiczna;

            bool? biezacaWartosc = _asoPW2Accessor.PobierzWartoscTypuLogicznego(nazwaPola); //odczyt
            _asoPW2Accessor.UstawWartoscTypuLogicznego(nazwaPola, true); //zapis
        }

        public void OdczytIZapisWartosciPolaTypuData()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuData;

            DateTime? biezacaWartosc = _asoPW2Accessor.PobierzWartoscTypuData(nazwaPola); //odczyt
            _asoPW2Accessor.UstawWartoscTypuData(nazwaPola, DateTime.Now); //zapis
        }

        public void OdczytIZapisWartosciPolaTypuSlownikWlasny()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuSlownikWlasny;

            //odczyt biezacej wartosci pola
            ElementSlownikowegoZrodlaDanych<int> biezacaPozycja = _asoPW2Accessor.PobierzWartoscTypuSlownikWlasny(nazwaPola);
            if (biezacaPozycja != null)
            {
                int biezacyKlucz = biezacaPozycja.Klucz;
                string biezacaWartosc = biezacaPozycja.Wartosc;
            }
            //pobranie wszystkich pozycji slownika
            IEnumerable<ElementSlownikowegoZrodlaDanych<int>> pozycjeSlownika = _asoPW2Accessor.PobierzWartosciTypuSlownikWlasny(nazwaPola);
            foreach (ElementSlownikowegoZrodlaDanych<int> pozycja in pozycjeSlownika)
            {
                int klucz = pozycja.Klucz;
                string wartosc = pozycja.Wartosc;
            }
            //zapis nowej wartosci pola
            ElementSlownikowegoZrodlaDanych<int> nowaPozycja = pozycjeSlownika.Last(); //dla przykładu wybieram ostatnią pozycję słownika
            _asoPW2Accessor.UstawWartoscTypuSlownikWlasny(nazwaPola, nowaPozycja.Klucz);
        }

        public void OdczytIZapisWartosciPolaTypuSlownikWlasnySqlByInt()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuSlownikWlasnySqlByInt;

            //odczyt biezacej wartosci pola
            ElementSlownikowegoZrodlaDanych<int> biezacaPozycja = _asoPW2Accessor.PobierzWartoscTypuSlownikWlasnySqlByInt(nazwaPola);
            if (biezacaPozycja != null)
            {
                int biezacyKlucz = biezacaPozycja.Klucz;
                string biezacaWartosc = biezacaPozycja.Wartosc;
            }
            //pobranie wszystkich pozycji slownika
            IEnumerable<ElementSlownikowegoZrodlaDanych<int>> pozycjeSlownika = _asoPW2Accessor.PobierzWartosciTypuSlownikWlasnySqlByInt(nazwaPola);
            foreach (ElementSlownikowegoZrodlaDanych<int> pozycja in pozycjeSlownika)
            {
                int klucz = pozycja.Klucz;
                string wartosc = pozycja.Wartosc;
            }
            //zapis nowej wartosci pola
            ElementSlownikowegoZrodlaDanych<int> nowaPozycja = pozycjeSlownika.Last(); //dla przykładu wybieram ostatnią pozycję słownika
            _asoPW2Accessor.UstawWartoscTypuSlownikWlasnySqlByInt(nazwaPola, nowaPozycja.Klucz);
        }

        public void OdczytIZapisWartosciPolaTypuSlownikWlasnySqlByGuid()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuSlownikWlasnySqlByGuid;

            //odczyt biezacej wartosci pola
            ElementSlownikowegoZrodlaDanych<Guid> biezacaPozycja = _asoPW2Accessor.PobierzWartoscTypuSlownikWlasnySqlByGuid(nazwaPola);
            if (biezacaPozycja != null)
            {
                Guid biezacyKlucz = biezacaPozycja.Klucz;
                string biezacaWartosc = biezacaPozycja.Wartosc;
            }
            //pobranie wszystkich pozycji slownika
            IEnumerable<ElementSlownikowegoZrodlaDanych<Guid>> pozycjeSlownika = _asoPW2Accessor.PobierzWartosciTypuSlownikWlasnySqlByGuid(nazwaPola);
            foreach (ElementSlownikowegoZrodlaDanych<Guid> pozycja in pozycjeSlownika)
            {
                Guid klucz = pozycja.Klucz;
                string wartosc = pozycja.Wartosc;
            }
            //zapis nowej wartosci pola
            ElementSlownikowegoZrodlaDanych<Guid> nowaPozycja = pozycjeSlownika.Last(); //dla przykładu wybieram ostatnią pozycję słownika
            _asoPW2Accessor.UstawWartoscTypuSlownikWlasnySqlByGuid(nazwaPola, nowaPozycja.Klucz);
        }

        public void OdczytIZapisWartosciPolaTypuSlownikSystemowyWalut()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuSlownikSystemowyWalut;

            //odczyt biezacej wartosci pola
            Waluta biezacaPozycja = _asoPW2Accessor.PobierzWartoscTypuSlownikSystemowyWalut(nazwaPola);
            if (biezacaPozycja != null)
            {
                Guid biezacyKlucz = biezacaPozycja.Id;
                string biezacaNazwa = biezacaPozycja.Nazwa;
                string biezacySymbol = biezacaPozycja.Symbol;
            }
            //pobranie wszystkich pozycji slownika
            IEnumerable<Waluta> pozycjeSlownika = _asoPW2Accessor.PobierzWartosciTypuSlownikSystemowyWalut(nazwaPola);
            foreach (Waluta pozycja in pozycjeSlownika)
            {
                Guid klucz = pozycja.Id;
                string nazwa = pozycja.Nazwa;
                string symbol = pozycja.Symbol;
            }
            //zapis nowej wartosci pola
            Waluta nowaPozycja = pozycjeSlownika.Last(); //dla przykładu wybieram ostatnią pozycję słownika
            _asoPW2Accessor.UstawWartoscTypuSlownikSystemowyWalut(nazwaPola, nowaPozycja.Id);
        }

        public void OdczytIZapisWartosciPolaTypuSlownikSystemowyMagazynow()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuSlownikSystemowyMagazynow;

            //odczyt biezacej wartosci pola
            Magazyn biezacaPozycja = _asoPW2Accessor.PobierzWartoscTypuSlownikSystemowyMagazynow(nazwaPola);
            if (biezacaPozycja != null)
            {
                int biezacyKlucz = biezacaPozycja.Id;
                string biezacaNazwa = biezacaPozycja.Nazwa;
                string biezacySymbol = biezacaPozycja.Symbol;
            }
            //pobranie wszystkich pozycji slownika
            IEnumerable<Magazyn> pozycjeSlownika = _asoPW2Accessor.PobierzWartosciTypuSlownikSystemowyMagazynow(nazwaPola);
            foreach (Magazyn pozycja in pozycjeSlownika)
            {
                int klucz = pozycja.Id;
                string nazwa = pozycja.Nazwa;
                string symbol = pozycja.Symbol;
            }
            //zapis nowej wartosci pola
            Magazyn nowaPozycja = pozycjeSlownika.Last(); //dla przykładu wybieram ostatnią pozycję słownika
            _asoPW2Accessor.UstawWartoscTypuSlownikSystemowyMagazynow(nazwaPola, nowaPozycja.Id);
        }

        public void OdczytIZapisWartosciPolaTypuSlownikSystemowyRachunkowBankowych()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuSlownikSystemowyRachunkowBankowych;

            //odczyt biezacej wartosci pola
            RachunekBankowy biezacaPozycja = _asoPW2Accessor.PobierzWartoscTypuSlownikSystemowyRachunkowBankowych(nazwaPola);
            if (biezacaPozycja != null)
            {
                int biezacyKlucz = biezacaPozycja.Id;
                string biezacaNazwa = biezacaPozycja.Nazwa;
                string biezacyNumer = biezacaPozycja.Numer;
            }
            //pobranie wszystkich pozycji slownika
            IEnumerable<RachunekBankowy> pozycjeSlownika = _asoPW2Accessor.PobierzWartosciTypuSlownikSystemowyRachunkowBankowych(nazwaPola);
            foreach (RachunekBankowy pozycja in pozycjeSlownika)
            {
                int klucz = pozycja.Id;
                string nazwa = pozycja.Nazwa;
                string numer = pozycja.Numer;
            }
            //zapis nowej wartosci pola
            RachunekBankowy nowaPozycja = pozycjeSlownika.Last(); //dla przykładu wybieram ostatnią pozycję słownika
            _asoPW2Accessor.UstawWartoscTypuSlownikSystemowyRachunkowBankowych(nazwaPola, nowaPozycja.Id);
        }
    }
}
