using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.PolaWlasne2;

namespace RozwiazanieWlasne
{
    public class ZaawansowanePolaWlasneTester
    {
        private readonly IZaawansowanePolaWlasne _zaawansowanePolaWlasne;

        public ZaawansowanePolaWlasneTester(IZaawansowanePolaWlasne zaawansowanePolaWlasne)
        {
            if (zaawansowanePolaWlasne == null)
            {
                throw new ArgumentNullException(nameof(zaawansowanePolaWlasne));
            }
            _zaawansowanePolaWlasne = zaawansowanePolaWlasne;
        }

        public void Test()
        {
            ObslugujeZaawansowanePolaWlasne_Test();
            PosiadaZaawansowanePolaWlasne_Test();
            SprobujPobracZaawansowanePolaWlasne_Test();
        }

        private void ObslugujeZaawansowanePolaWlasne_Test()
        {
            bool obsluguje;
            obsluguje = _zaawansowanePolaWlasne.ObslugujeZaawansowanePolaWlasne(typeof(Asortyment));
            obsluguje = _zaawansowanePolaWlasne.ObslugujeZaawansowanePolaWlasne<Asortyment>();
        }

        private void PosiadaZaawansowanePolaWlasne_Test()
        {
            bool posiada;
            posiada = _zaawansowanePolaWlasne.PosiadaZaawansowanePolaWlasne(typeof(Asortyment));
            posiada = _zaawansowanePolaWlasne.PosiadaZaawansowanePolaWlasne<Asortyment>();
        }

        private void SprobujPobracZaawansowanePolaWlasne_Test()
        {
            IEnumerable<IZaawansowanePoleWlasne> pola;

            if (_zaawansowanePolaWlasne.SprobujPobracZaawansowanePolaWlasne(typeof(Asortyment), out pola))
            {
                foreach (IZaawansowanePoleWlasne pole in pola)
                {
                    SprawdzZaawansowanePoleWlasne(pole, false);
                }
            }

            if (_zaawansowanePolaWlasne.SprobujPobracZaawansowanePolaWlasne<Asortyment>(out pola))
            {
                foreach (IZaawansowanePoleWlasne pole in pola)
                {
                    SprawdzZaawansowanePoleWlasne(pole, true);
                }
            }
        }

        private void SprawdzZaawansowanePoleWlasne(IZaawansowanePoleWlasne pole, bool wypiszNaKonsole)
        {
            string id = pole.Id;
            string nazwa = pole.Nazwa;
            string grupa = pole.Grupa == null ? "(brak)" : pole.Grupa.Nazwa;
            string opis = pole.Opis;
            TypSkalarnyZaawansowanegoPolaWlasnego typ = pole.Typ;
            Type typClr = pole.TypClr;
            int precyzja = pole.Precyzja;
            int minWidoczneLinie = pole.MinWidoczneLinie;
            int maxWidoczneLinie = pole.MaxWidoczneLinie;
            object wartoscDomyslna = pole.WartoscDomyslna;
            bool wymagane = pole.Wymagane;
            bool widoczne = pole.Widoczne;
            bool edytowalne = pole.Edytowalne;
            bool klonowalne = pole.Klonowalne;
            if (wypiszNaKonsole)
            {
                Console.WriteLine($"         IdPola={id}, Nazwa={nazwa}, Grupa={grupa}, Opis={opis}, TypSkalarny={typ}, Precyzja={precyzja}, MinWidoczneLinie={minWidoczneLinie}, MaxWidoczneLinie={maxWidoczneLinie}, WartoscDomyslna={wartoscDomyslna}");
            }

            if (pole.JestReferencjaDoSlownika)
            {
                ISlownikoweZrodloDanych zrodloDanych = pole.PobierzDefinicjeSlownika();
                switch (pole.RodzajSlownika) //w zależności od rodzaju słownika zwrócone zrodloDanych można rzutować na bardziej szczegółowy interfejs
                {
                    case RodzajSlownikowegoZrodlaDanych.SlownikWlasny:
                        var slownikWlasny = (ISlownikoweZrodloDanych<int, PozycjaSlownikaWlasnego>)zrodloDanych;
                        //pobieramy wszystkie wartości słownika
                        foreach (PozycjaSlownikaWlasnego item in slownikWlasny.UtworzZapytanieLinqTypowane())
                        {
                            int idPozycjiSlownika = item.Id;
                            string wartoscPozycjiSlownika = item.Wartosc;
                            bool pozycjaSlownikaAktywna = item.Aktywna;
                            if (wypiszNaKonsole)
                            {
                                Console.WriteLine($"             Klucz={idPozycjiSlownika}, Wartość={wartoscPozycjiSlownika}, Aktywna={pozycjaSlownikaAktywna}");
                            }
                        }
                        //pobieramy tylko widoczne w UI wartości słownika
                        foreach (PozycjaSlownikaWlasnego item in slownikWlasny.UtworzZapytanieFiltrowaneLinqTypowane())
                        {
                            int idPozycjiSlownika = item.Id;
                            string wartoscPozycjiSlownika = item.Wartosc;
                            bool pozycjaSlownikaAktywna = item.Aktywna;
                        }
                        break;
                    case RodzajSlownikowegoZrodlaDanych.SlownikWlasnySql:
                        if (zrodloDanych.TypKlucza == typeof(int))
                        {
                            var slownikWlasnySqlOKluczuTypuInt = (ISlownikoweZrodloDanych<int>)zrodloDanych;
                            //pobieramy wszystkie wartości słownika
                            foreach (ElementSlownikowegoZrodlaDanych<int> item in slownikWlasnySqlOKluczuTypuInt.UtworzZapytanieLinq())
                            {
                                int idPozycjiSlownika = item.Klucz;
                                string wartoscPozycjiSlownika = item.Wartosc;
                                if (wypiszNaKonsole)
                                {
                                    Console.WriteLine($"             Klucz={idPozycjiSlownika}, Wartość={wartoscPozycjiSlownika}");
                                }
                            }
                            //pobieramy tylko widoczne w UI wartości słownika
                            foreach (ElementSlownikowegoZrodlaDanych<int> item in slownikWlasnySqlOKluczuTypuInt.UtworzZapytanieFiltrowaneLinq())
                            {
                                int idPozycjiSlownika = item.Klucz;
                                string wartoscPozycjiSlownika = item.Wartosc;
                            }
                        }
                        else if (zrodloDanych.TypKlucza == typeof(Guid))
                        {
                            var slownikWlasnySqlOKluczuTypuGuid = (ISlownikoweZrodloDanych<Guid>)zrodloDanych;
                            //pobieramy wszystkie wartości słownika
                            foreach (ElementSlownikowegoZrodlaDanych<Guid> item in slownikWlasnySqlOKluczuTypuGuid.UtworzZapytanieLinq())
                            {
                                Guid idPozycjiSlownika = item.Klucz;
                                string wartoscPozycjiSlownika = item.Wartosc;
                                if (wypiszNaKonsole)
                                {
                                    Console.WriteLine($"             Klucz={idPozycjiSlownika}, Wartość={wartoscPozycjiSlownika}");
                                }
                            }
                            //pobieramy tylko widoczne w UI wartości słownika
                            foreach (ElementSlownikowegoZrodlaDanych<Guid> item in slownikWlasnySqlOKluczuTypuGuid.UtworzZapytanieFiltrowaneLinq())
                            {
                                Guid idPozycjiSlownika = item.Klucz;
                                string wartoscPozycjiSlownika = item.Wartosc;
                            }
                        }
                        else
                        {
                            throw new NotSupportedException(zrodloDanych.TypKlucza.FullName);
                        }
                        break;
                    case RodzajSlownikowegoZrodlaDanych.SlownikSystemowy:
                        switch (zrodloDanych.Id)
                        {
                            case IdentyfikatorSlownikaSystemowego.Waluty:
                                var slownikWalut = (ISlownikoweZrodloDanych<Guid, Waluta>)zrodloDanych;
                                //pobieramy wszystkie wartości słownika
                                foreach (Waluta item in slownikWalut.UtworzZapytanieLinqTypowane())
                                {
                                    if (wypiszNaKonsole)
                                    {
                                        Console.WriteLine($"             Waluta={item}");
                                    }
                                }
                                //dla słowników systemowych zapytanie filtrowane zwraca te same dane, co zapytanie pełne
                                foreach (Waluta item in slownikWalut.UtworzZapytanieFiltrowaneLinqTypowane())
                                {
                                }
                                break;
                            case IdentyfikatorSlownikaSystemowego.Magazyny:
                                var slownikMagazynow = (ISlownikoweZrodloDanych<int, Magazyn>)zrodloDanych;
                                //pobieramy wszystkie wartości słownika
                                foreach (Magazyn item in slownikMagazynow.UtworzZapytanieLinqTypowane())
                                {
                                    if (wypiszNaKonsole)
                                    {
                                        Console.WriteLine($"             Magazyn={item}");
                                    }
                                }
                                //dla słowników systemowych zapytanie filtrowane zwraca te same dane, co zapytanie pełne
                                foreach (Magazyn item in slownikMagazynow.UtworzZapytanieFiltrowaneLinqTypowane())
                                {
                                }
                                break;
                            case IdentyfikatorSlownikaSystemowego.RachunkiBankowe:
                                var slownikRachunkow = (ISlownikoweZrodloDanych<int, RachunekBankowy>)zrodloDanych;
                                //pobieramy wszystkie wartości słownika
                                foreach (RachunekBankowy item in slownikRachunkow.UtworzZapytanieLinqTypowane())
                                {
                                    if (wypiszNaKonsole)
                                    {
                                        Console.WriteLine($"             Rachunek bankowy={item}");
                                    }
                                }
                                //dla słowników systemowych zapytanie filtrowane zwraca te same dane, co zapytanie pełne
                                foreach (RachunekBankowy item in slownikRachunkow.UtworzZapytanieFiltrowaneLinqTypowane())
                                {
                                }
                                break;
                            default:
                                throw new NotSupportedException(zrodloDanych.Id.ToString());
                        }
                        break;
                    default:
                        throw new NotSupportedException(pole.RodzajSlownika.ToString());
                }
            }
        }
    }
}
