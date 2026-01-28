using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Asortymenty;

namespace RozwiazanieWlasne
{
    public class OdczytIZapisWartosciPolWlasnychStarySposob : IOdczytIZapisWartosciPolWlasnychTester
    {
        private readonly IAsortyment _aso;
        private readonly IZaawansowanePolaWlasne _zaawansowanePolaWlasne;

        public OdczytIZapisWartosciPolWlasnychStarySposob(IAsortyment aso,
            IZaawansowanePolaWlasne zaawansowanePolaWlasne)
        {
            if (aso == null)
            {
                throw new ArgumentNullException(nameof(aso));
            }

            if (zaawansowanePolaWlasne == null)
            {
                throw new ArgumentNullException(nameof(zaawansowanePolaWlasne));
            }

            _aso = aso;
            _zaawansowanePolaWlasne = zaawansowanePolaWlasne;
        }

        #region Obsluga zdarzenia PropertyChanged

        private void PolaWlasneAdv2_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //Nazwa wlasciwosci przekazywana jako e.PropertyName to identyfikator pola wlasnego
            var zmienionePole = _zaawansowanePolaWlasne.PobierzZaawansowanePolaWlasne<Asortyment>().FirstOrDefault(p => string.Equals(p.Id, e.PropertyName, StringComparison.Ordinal));
            if (zmienionePole != null)
            {
                string nazwaPola = zmienionePole.Nazwa;
            }
        }

        public void PodpiecieDoZdarzeniaPropertyChanged()
        {
            _aso.Dane.PolaWlasneAdv2.PropertyChanged += PolaWlasneAdv2_PropertyChanged;
        }

        public void OdpiecieOdZdarzeniaPropertyChanged()
        {
            _aso.Dane.PolaWlasneAdv2.PropertyChanged -= PolaWlasneAdv2_PropertyChanged;
        }

        #endregion

        public void OdczytIZapisWartosciPolaTypuTekst()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuTekst;

            string biezacaWartosc = _aso.Dane.PolaWlasneAdv2.Get<string>(nazwaPola); //odczyt
            _aso.Dane.PolaWlasneAdv2.Set<string>(nazwaPola, "abcd"); //zapis
        }

        public void OdczytIZapisWartosciPolaTypuDlugiTekst()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuDlugiTekst;

            string biezacaWartosc = _aso.Dane.PolaWlasneAdv2.Get<string>(nazwaPola); //odczyt

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("Pierwsza linijka tekstu przykładowego.");
            stringBuilder.AppendLine();
            stringBuilder.Append("Druga linijka tekstu przykładowego.");
            _aso.Dane.PolaWlasneAdv2.Set<string>(nazwaPola, stringBuilder.ToString()); //zapis
        }

        public void OdczytIZapisWartosciPolaTypuLiczbaCalkowita()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuLiczbaCalkowita;

            int? biezacaWartosc = _aso.Dane.PolaWlasneAdv2.Get<int?>(nazwaPola); //odczyt
            _aso.Dane.PolaWlasneAdv2.Set<int?>(nazwaPola, 5); //zapis
        }

        public void OdczytIZapisWartosciPolaTypuLiczbaRzeczywista()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuLiczbaRzeczywista;

            decimal? biezacaWartosc = _aso.Dane.PolaWlasneAdv2.Get<decimal?>(nazwaPola); //odczyt
            _aso.Dane.PolaWlasneAdv2.Set<decimal?>(nazwaPola, 1.23m); //zapis
        }

        public void OdczytIZapisWartosciPolaTypuWartoscLogiczna()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuWartoscLogiczna;

            bool? biezacaWartosc = _aso.Dane.PolaWlasneAdv2.Get<bool?>(nazwaPola); //odczyt
            _aso.Dane.PolaWlasneAdv2.Set<bool?>(nazwaPola, true); //zapis
        }

        public void OdczytIZapisWartosciPolaTypuData()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuData;

            DateTime? biezacaWartosc = _aso.Dane.PolaWlasneAdv2.Get<DateTime?>(nazwaPola); //odczyt
            _aso.Dane.PolaWlasneAdv2.Set<DateTime?>(nazwaPola, DateTime.Now); //zapis
        }

        public void OdczytIZapisWartosciPolaTypuSlownikWlasny()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuSlownikWlasny;

            //pobranie wszystkich pozycji slownika
            var pole = _zaawansowanePolaWlasne.PobierzZaawansowanePoleWlasne<Asortyment>(nazwaPola);
            var slownik = (ISlownikoweZrodloDanych<int>)pole.PobierzDefinicjeSlownika();
            var pozycjeSlownika = slownik.UtworzZapytanieFiltrowaneLinq().ToList(); //pobierz tylko aktywne pozycje
            //odczyt biezacej wartosci pola
            int? kluczObcyDoWartosciSlownika = _aso.Dane.PolaWlasneAdv2.Get<int?>(nazwaPola); //najpierw odczytujemy klucz obcy z pola, który jest identyfikatorem pozycji słownika
            if (kluczObcyDoWartosciSlownika.HasValue)
            {
                //znalezienie biezacej pozycji slownika o danym kluczu
                var biezacaPozycja = pozycjeSlownika.FirstOrDefault(p => p.Klucz == kluczObcyDoWartosciSlownika);
                if (biezacaPozycja != null)
                {
                    string biezacaWartosc = biezacaPozycja.Wartosc;
                }
            }
            //zapis nowej wartosci pola
            var nowaPozycja = pozycjeSlownika.Last(); //dla przykładu wybieram ostatnią pozycję słownika
            _aso.Dane.PolaWlasneAdv2.Set<int?>(nazwaPola, nowaPozycja.Klucz); //zapisujemy klucz obcy pozycji slownika do pola
        }

        public void OdczytIZapisWartosciPolaTypuSlownikWlasnySqlByInt()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuSlownikWlasnySqlByInt;

            //pobranie wszystkich pozycji slownika
            var pole = _zaawansowanePolaWlasne.PobierzZaawansowanePoleWlasne<Asortyment>(nazwaPola);
            var slownik = (ISlownikoweZrodloDanych<int>)pole.PobierzDefinicjeSlownika();
            var pozycjeSlownika = slownik.UtworzZapytanieFiltrowaneLinq().ToList(); //pobierz pozycje z użyciem klauzuli filtrującej
            //odczyt biezacej wartosci pola
            int? kluczObcyDoWartosciSlownika = _aso.Dane.PolaWlasneAdv2.Get<int?>(nazwaPola); //najpierw odczytujemy klucz obcy z pola, który jest identyfikatorem pozycji słownika
            if (kluczObcyDoWartosciSlownika.HasValue)
            {
                var biezacaPozycja = pozycjeSlownika.FirstOrDefault(p => p.Klucz == kluczObcyDoWartosciSlownika);
                if (biezacaPozycja != null)
                {
                    string biezacaWartosc = biezacaPozycja.Wartosc;
                }
            }
            //zapis nowej wartosci pola
            var nowaPozycja = pozycjeSlownika.Last(); //dla przykładu wybieram ostatnią pozycję słownika
            _aso.Dane.PolaWlasneAdv2.Set<int?>(nazwaPola, nowaPozycja.Klucz); //zapisujemy klucz obcy pozycji slownika do pola
        }

        public void OdczytIZapisWartosciPolaTypuSlownikWlasnySqlByGuid()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuSlownikWlasnySqlByGuid;

            //pobranie wszystkich pozycji slownika
            var pole = _zaawansowanePolaWlasne.PobierzZaawansowanePoleWlasne<Asortyment>(nazwaPola);
            var slownik = (ISlownikoweZrodloDanych<Guid>)pole.PobierzDefinicjeSlownika();
            var pozycjeSlownika = slownik.UtworzZapytanieFiltrowaneLinq().ToList(); //pobierz pozycje z użyciem klauzuli filtrującej
            //odczyt biezacej wartosci pola
            Guid? kluczObcyDoWartosciSlownika = _aso.Dane.PolaWlasneAdv2.Get<Guid?>(nazwaPola); //najpierw odczytujemy klucz obcy z pola, który jest identyfikatorem pozycji słownika
            if (kluczObcyDoWartosciSlownika.HasValue)
            {
                var biezacaPozycja = pozycjeSlownika.FirstOrDefault(p => p.Klucz == kluczObcyDoWartosciSlownika);
                if (biezacaPozycja != null)
                {
                    string biezacaWartosc = biezacaPozycja.Wartosc;
                }
            }
            //zapis nowej wartosci pola
            var nowaPozycja = pozycjeSlownika.Last(); //dla przykładu wybieram ostatnią pozycję słownika
            _aso.Dane.PolaWlasneAdv2.Set<Guid?>(nazwaPola, nowaPozycja.Klucz); //zapisujemy klucz obcy pozycji slownika do pola

        }

        public void OdczytIZapisWartosciPolaTypuSlownikSystemowyWalut()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuSlownikSystemowyWalut;

            //pobranie wszystkich pozycji slownika
            var pole = _zaawansowanePolaWlasne.PobierzZaawansowanePoleWlasne<Asortyment>(nazwaPola);
            var slownik = (ISlownikoweZrodloDanych<Guid, Waluta>)pole.PobierzDefinicjeSlownika();
            var pozycjeSlownika = slownik.UtworzZapytanieFiltrowaneLinqTypowane().ToList(); //nie ma znaczenia, czy użyjemy wersji filtrowanej, czy nie filtrowanej
            //odczyt biezacej wartosci pola
            Guid? kluczObcyDoWartosciSlownika = _aso.Dane.PolaWlasneAdv2.Get<Guid?>(nazwaPola); //najpierw odczytujemy klucz obcy z pola, który jest identyfikatorem pozycji słownika
            if (kluczObcyDoWartosciSlownika.HasValue)
            {
                var biezacaPozycja = pozycjeSlownika.FirstOrDefault(p => p.Id == kluczObcyDoWartosciSlownika);
                if (biezacaPozycja != null)
                {
                    Waluta biezacaWartosc = biezacaPozycja;
                }
            }
            //zapis nowej wartosci pola
            var nowaPozycja = pozycjeSlownika.Last(); //dla przykładu wybieram ostatnią pozycję słownika
            _aso.Dane.PolaWlasneAdv2.Set<Guid?>(nazwaPola, nowaPozycja.Id); //zapisujemy klucz obcy pozycji slownika do pola
        }

        public void OdczytIZapisWartosciPolaTypuSlownikSystemowyMagazynow()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuSlownikSystemowyMagazynow;

            //pobranie wszystkich pozycji slownika
            var pole = _zaawansowanePolaWlasne.PobierzZaawansowanePoleWlasne<Asortyment>(nazwaPola);
            var slownik = (ISlownikoweZrodloDanych<int, Magazyn>)pole.PobierzDefinicjeSlownika();
            var pozycjeSlownika = slownik.UtworzZapytanieFiltrowaneLinqTypowane().ToList(); //nie ma znaczenia, czy użyjemy wersji filtrowanej, czy nie filtrowanej
            //odczyt biezacej wartosci pola
            int? kluczObcyDoWartosciSlownika = _aso.Dane.PolaWlasneAdv2.Get<int?>(nazwaPola); //najpierw odczytujemy klucz obcy z pola, który jest identyfikatorem pozycji słownika
            if (kluczObcyDoWartosciSlownika.HasValue)
            {
                var biezacaPozycja = pozycjeSlownika.FirstOrDefault(p => p.Id == kluczObcyDoWartosciSlownika);
                if (biezacaPozycja != null)
                {
                    Magazyn biezacaWartosc = biezacaPozycja;
                }
            }
            //zapis nowej wartosci pola
            var nowaPozycja = pozycjeSlownika.Last(); //dla przykładu wybieram ostatnią pozycję słownika
            _aso.Dane.PolaWlasneAdv2.Set<int?>(nazwaPola, nowaPozycja.Id); //zapisujemy klucz obcy pozycji slownika do pola
        }

        public void OdczytIZapisWartosciPolaTypuSlownikSystemowyRachunkowBankowych()
        {
            const string nazwaPola = TestowePolaWlasneAsortymentu.PoleTypuSlownikSystemowyRachunkowBankowych;

            //pobranie wszystkich pozycji slownika
            var pole = _zaawansowanePolaWlasne.PobierzZaawansowanePoleWlasne<Asortyment>(nazwaPola);
            var slownik = (ISlownikoweZrodloDanych<int, RachunekBankowy>)pole.PobierzDefinicjeSlownika();
            var pozycjeSlownika = slownik.UtworzZapytanieFiltrowaneLinqTypowane().ToList(); //nie ma znaczenia, czy użyjemy wersji filtrowanej, czy nie filtrowanej
            //odczyt biezacej wartosci pola
            int? kluczObcyDoWartosciSlownika = _aso.Dane.PolaWlasneAdv2.Get<int?>(nazwaPola); //najpierw odczytujemy klucz obcy z pola, który jest identyfikatorem pozycji słownika
            if (kluczObcyDoWartosciSlownika.HasValue)
            {
                var biezacaPozycja = pozycjeSlownika.FirstOrDefault(p => p.Id == kluczObcyDoWartosciSlownika);
                if (biezacaPozycja != null)
                {
                    RachunekBankowy biezacaWartosc = biezacaPozycja;
                }
            }
            //zapis nowej wartosci pola
            var nowaPozycja = pozycjeSlownika.Last(); //dla przykładu wybieram ostatnią pozycję słownika
            _aso.Dane.PolaWlasneAdv2.Set<int?>(nazwaPola, nowaPozycja.Id); //zapisujemy klucz obcy pozycji slownika do pola
        }
    }
}
