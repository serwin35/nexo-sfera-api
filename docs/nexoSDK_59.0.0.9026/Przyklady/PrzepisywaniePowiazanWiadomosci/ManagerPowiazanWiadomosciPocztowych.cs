using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Dzialania;
using InsERT.Moria.Klienci;
using InsERT.Moria.KlientPoczty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Procesy;
using InsERT.Moria.Sfera;
using InsERT.Mox.Product;
using PrzepisywaniePowiazanWiadomosci.Exceptions;
using PrzepisywaniePowiazanWiadomosci.Models;

namespace PrzepisywaniePowiazanWiadomosci
{
    public class ManagerPowiazanWiadomosciPocztowych
    {
        private Uchwyt _sfera;
        private IWiadomosciPocztowe _wiadomosciPocztowe;
        private IPodmioty _klienci;
        private IDzialania _dzialania;
        private IDokumenty _dokumenty;
        private IZleceniaSerwisowe _zleceniaSerwisowe;
        private ISerwisowaneUrzadzenia _serwisowaneUrzadzenia;
        private IProcesyOfertowe _procesyOfertowe;
        private IZasoby _zasoby;
        private IRezerwacjeZasobow _rezerwacjeZasobow;
        private List<UproszczonaWiadomoscPocztowa> _przetworzoneWiadomosciPocztowe;

        public bool UruchomionoSfere = false;

        public List<UproszczoneKontoPocztowe> PolaczSieZeSferaIPobierzDostepneKontaPocztowe(
            string login,
            string haslo,
            string serwer,
            string baza,
            bool autentykacjaWindows,
            string nazwaUzytkownikaSerwera,
            string hasloUzytkownikaSerwera)
        {
            if (_sfera == null)
            {
                try
                {
                    _sfera = UruchomSfere(serwer, baza, autentykacjaWindows, nazwaUzytkownikaSerwera, hasloUzytkownikaSerwera);
                    UruchomionoSfere = true;
                }
                catch (Exception ex)
                {
                    if (ex is ArgumentOutOfRangeException)
                    {
                        throw new SferaException("Nie udało się połączyć ze Sferą. Podaj wszystkie dane logowania.");
                    }
                    else if (ex is SqlException)
                    {
                        throw new SferaException("Nie udało się połączyć ze Sferą. Podano niepoprawne dane logowania do serwera SQL.");
                    }
                    else
                    {
                        throw new SferaException($"Nie udało się połączyć ze Sferą. Wystąpił nieoczekiwany błąd: {ex.Message}");
                    }
                }
            }

            if (String.IsNullOrWhiteSpace(login))
            {
                throw new SferaException("Nie udało się połączyć ze Sferą. Podaj login.");
            }
            if (String.IsNullOrWhiteSpace(haslo))
            {
                throw new SferaException("Nie udało się połączyć ze Sferą. Podaj hasło.");
            }

            bool poprawneDaneLogowania = _sfera.SprawdzHaslo(login, haslo);
            if (poprawneDaneLogowania)
            {
                _sfera.ZalogujOperatora(login, haslo);
            }
            else
            {
                throw new SferaException("Nie udało się połączyć ze Sferą. Podano niepoprawne dane logowania użytkownika nexo.");
            }

            try
            {
                IKontaPocztowe kartkotekaKontPocztowych = _sfera.PodajObiektTypu<IKontaPocztowe>();
                var kontaPocztowe =
                    kartkotekaKontPocztowych.Dane
                    .Wszystkie()
                    .Select(x => new UproszczoneKontoPocztowe
                    {
                        Nazwa = x.Nazwa,
                        Adres = x.Adres
                    })
                    .OrderBy(x => x.Adres)
                    .ToList();

                return kontaPocztowe;
            }
            catch (Exception ex)
            {
                throw new SferaException($"Nie udało się połączyć ze Sferą. Wystąpił nieoczekiwany błąd: {ex.Message}");
            }
        }

        private Uchwyt UruchomSfere(
            string serwer,
            string baza,
            bool autentykacjaWindows,
            string nazwaUzytkownikaSerwera,
            string hasloUzytkownikaSerwera)
        {
            DanePolaczenia danePolaczenia = DanePolaczenia.Jawne(serwer, baza, autentykacjaWindows, nazwaUzytkownikaSerwera, hasloUzytkownikaSerwera);
            MenedzerPolaczen mp = new MenedzerPolaczen
            {
                DostepDoUI = true
            };
            Uchwyt sfera = mp.Polacz(danePolaczenia, ProductId.Gestor);
            return sfera;
        }

        public IEnumerable<UproszczonaWiadomoscPocztowa> PrzepiszPowiazaniaWiadomosciPocztowych(
            UproszczoneKontoPocztowe kontoZrodlowe,
            UproszczoneKontoPocztowe kontoDocelowe)
        {
            if (kontoZrodlowe == null || kontoDocelowe == null)
            {
                throw new SferaException("Wybierz konta pocztowe, dla których mają zostać przepisane powiązania wiadomości.");
            }

            string nazwaKontaZrodlowego = kontoZrodlowe.Nazwa;
            string nazwaKontaDocelowego = kontoDocelowe.Nazwa;
            if (nazwaKontaZrodlowego == nazwaKontaDocelowego)
            {
                throw new SferaException("Należy wybrać dwa różne konta pocztowe.");
            }

            if (kontoZrodlowe.Adres != kontoDocelowe.Adres)
            {
                throw new SferaException("Wybrane konta pocztowe muszą mieć taki sam adres e-mail.");
            }

            _przetworzoneWiadomosciPocztowe = new List<UproszczonaWiadomoscPocztowa>();

            try
            {
                _wiadomosciPocztowe = _sfera.PodajObiektTypu<IWiadomosciPocztowe>();
                _klienci = _sfera.PodajObiektTypu<IPodmioty>();
                _dzialania = _sfera.PodajObiektTypu<IDzialania>();
                _dokumenty = _sfera.PodajObiektTypu<IDokumenty>();
                _zleceniaSerwisowe = _sfera.PodajObiektTypu<IZleceniaSerwisowe>();
                _serwisowaneUrzadzenia = _sfera.PodajObiektTypu<ISerwisowaneUrzadzenia>();
                _procesyOfertowe = _sfera.PodajObiektTypu<IProcesyOfertowe>();
                _zasoby = _sfera.PodajObiektTypu<IZasoby>();
                _rezerwacjeZasobow = _sfera.PodajObiektTypu<IRezerwacjeZasobow>();

                var odebraneWiadomosciNaKoncieZrodlowym = PobierzWiadomosciPocztoweNaKoncieZrodlowym(nazwaKontaZrodlowego, true);
                var wyslaneWiadomosciNaKoncieZrodlowym = PobierzWiadomosciPocztoweNaKoncieZrodlowym(nazwaKontaZrodlowego, false);

                var odebraneWiadomosciNaKoncieDocelowym = PobierzWiadomosciPocztoweNaKoncieDocelowym(nazwaKontaDocelowego, true);
                var wyslaneWiadomosciNaKoncieDocelowym = PobierzWiadomosciPocztoweNaKoncieDocelowym(nazwaKontaDocelowego, false);

                WyszukajWspolneWiadomosciOdebraneDlaObuKontIPrzepiszPowiazaniaWiadomosci(odebraneWiadomosciNaKoncieZrodlowym, odebraneWiadomosciNaKoncieDocelowym);
                WyszukajWspolneWiadomosciWyslaneDlaObuKontIPrzepiszPowiazaniaWiadomosci(wyslaneWiadomosciNaKoncieZrodlowym, wyslaneWiadomosciNaKoncieDocelowym);

                return _przetworzoneWiadomosciPocztowe;
            }
            catch (Exception ex)
            {
                throw new SferaException($"Nie udało się przepisać powiązań wiadomości. Wystąpił nieoczekiwany błąd: {ex.Message}");
            }
        }

        private List<UproszczonaWiadomoscPocztowa> PobierzWiadomosciPocztoweNaKoncieZrodlowym(string nazwaKontaZrodlowego, bool przychodzace)
        {
            var wiadomosci =
                _wiadomosciPocztowe
                .Dane
                .Wszystkie()
                .Where(x => x.Konto.Nazwa == nazwaKontaZrodlowego && x.Kopie.FirstOrDefault().Przychodzaca == przychodzace)
                .Select(x => new UproszczonaWiadomoscPocztowa
                {
                    Temat = x.Temat,
                    DataWyslania = x.DataWyslania,
                    Nadawca = x.NadawcaAdres,
                    Adresaci = x.Adresaci.Select(y => y.Adres),
                    HashTresci = x.Tresc.Tekst,
                    KlientId = x.KlientId,
                    DokumentId = x.DokumentId,
                    DzialanieId = x.DzialanieId,
                    ProcesOfertowyId = x.ProcesOfertowyId,
                    RezerwacjaZasobuId = x.RezerwacjaZasobuId,
                    ZasobId = x.ZasobId,
                    ZlecenieSerwisoweId = x.ZlecenieSerwisoweId,
                    SerwisowaneUrzadzenieId = (x.ZlecenieSerwisowe == null) ? 0 : x.ZlecenieSerwisowe.SerwisowaneUrzadzenieId
                })
                .ToList();

            return wiadomosci;
        }

        private IQueryable<WiadomoscPocztowa> PobierzWiadomosciPocztoweNaKoncieDocelowym(string nazwaKontaDocelowego, bool przychodzace)
        {
            var wiadomosci =
                _wiadomosciPocztowe
                .Dane
                .Wszystkie()
                .Where(x => x.Konto.Nazwa == nazwaKontaDocelowego && x.Kopie.FirstOrDefault().Przychodzaca == przychodzace);

            return wiadomosci;
        }

        private void WyszukajWspolneWiadomosciOdebraneDlaObuKontIPrzepiszPowiazaniaWiadomosci(List<UproszczonaWiadomoscPocztowa> odebraneWiadomosciNaKoncieZrodlowym, IQueryable<WiadomoscPocztowa> odebraneWiadomosciNaKoncieDocelowym)
        {
            foreach (var wiadomoscNaKoncieZrodlowym in odebraneWiadomosciNaKoncieZrodlowym)
            {
                if (!wiadomoscNaKoncieZrodlowym.MaPowiazania())
                {
                    continue;
                }

                var takieSameWiadomosciNaKoncieDocelowym =
                     odebraneWiadomosciNaKoncieDocelowym
                     .Where(x => x.NadawcaAdres == wiadomoscNaKoncieZrodlowym.Nadawca &&
                            x.Temat == wiadomoscNaKoncieZrodlowym.Temat &&
                            x.DataWyslania == wiadomoscNaKoncieZrodlowym.DataWyslania);

                int liczbaWiadomosci = takieSameWiadomosciNaKoncieDocelowym.Count();
                if (liczbaWiadomosci == 1)
                {
                    WiadomoscPocztowa wiadomoscNaKoncieDocelowym = takieSameWiadomosciNaKoncieDocelowym.FirstOrDefault();
                    PrzepiszPowiazaniaWiadomosciZKontaZrodlowegoDoDocelowego(wiadomoscNaKoncieZrodlowym, wiadomoscNaKoncieDocelowym);
                }
                else if (liczbaWiadomosci > 1)
                {
                    foreach (var wiadomoscNaKoncieDocelowym in takieSameWiadomosciNaKoncieDocelowym)
                    {
                        if (wiadomoscNaKoncieDocelowym.Tresc.Tekst.SequenceEqual(wiadomoscNaKoncieZrodlowym.HashTresci))
                        {
                            PrzepiszPowiazaniaWiadomosciZKontaZrodlowegoDoDocelowego(wiadomoscNaKoncieZrodlowym, wiadomoscNaKoncieDocelowym);
                            break;
                        }
                    }
                }
            }
        }

        private void WyszukajWspolneWiadomosciWyslaneDlaObuKontIPrzepiszPowiazaniaWiadomosci(List<UproszczonaWiadomoscPocztowa> wyslaneWiadomosciNaKoncieZrodlowym, IQueryable<WiadomoscPocztowa> wyslaneWiadomosciNaKoncieDocelowym)
        {
            foreach (var wiadomoscNaKoncieZrodlowym in wyslaneWiadomosciNaKoncieZrodlowym)
            {
                if (!wiadomoscNaKoncieZrodlowym.MaPowiazania())
                {
                    continue;
                }

                var takieSameWiadomosciNaKoncieDocelowym =
                     wyslaneWiadomosciNaKoncieDocelowym
                     .Where(x => x.Temat == wiadomoscNaKoncieZrodlowym.Temat && x.DataWyslania == wiadomoscNaKoncieZrodlowym.DataWyslania)
                     .ToList();

                for (int i = takieSameWiadomosciNaKoncieDocelowym.Count - 1; i >= 0; i--)
                {
                    bool zgodniAdresaci = true;
                    foreach (var adresat in takieSameWiadomosciNaKoncieDocelowym[i].Adresaci)
                    {
                        if (!wiadomoscNaKoncieZrodlowym.Adresaci.Contains(adresat.Adres))
                        {
                            zgodniAdresaci = false;
                            break;
                        }
                    }

                    if (!zgodniAdresaci)
                    {
                        takieSameWiadomosciNaKoncieDocelowym.RemoveAt(i);
                    }
                }

                int liczbaWiadomosci = takieSameWiadomosciNaKoncieDocelowym.Count;
                if (liczbaWiadomosci == 1)
                {
                    WiadomoscPocztowa wiadomoscNaKoncieDocelowym = takieSameWiadomosciNaKoncieDocelowym.FirstOrDefault();
                    PrzepiszPowiazaniaWiadomosciZKontaZrodlowegoDoDocelowego(wiadomoscNaKoncieZrodlowym, wiadomoscNaKoncieDocelowym);
                }
                else if (liczbaWiadomosci > 1)
                {
                    for (int i = liczbaWiadomosci - 1; i >= 0; i--)
                    {
                        if (takieSameWiadomosciNaKoncieDocelowym[i].Tresc.Tekst.SequenceEqual(wiadomoscNaKoncieZrodlowym.HashTresci) == false)
                        {
                            takieSameWiadomosciNaKoncieDocelowym.RemoveAt(i);
                        }
                    }

                    liczbaWiadomosci = takieSameWiadomosciNaKoncieDocelowym.Count;
                    if (liczbaWiadomosci == 1)
                    {
                        WiadomoscPocztowa wiadomoscNaKoncieDocelowym = takieSameWiadomosciNaKoncieDocelowym.FirstOrDefault();
                        PrzepiszPowiazaniaWiadomosciZKontaZrodlowegoDoDocelowego(wiadomoscNaKoncieZrodlowym, wiadomoscNaKoncieDocelowym);
                    }
                    else if (liczbaWiadomosci > 1)
                    {
                        /// TODO
                        /// Mamy do czynienia z przypadkiem, że wysłano wiadomość do samego siebie.
                        /// Wtedy wiadomość jest zarówno w odebranych i wysłanych (i może być też w szkicach),
                        /// a jej kopia jest oznaczona jako wiadomość wychodząca (wiadomosc.Kopie.FirstOrDefault().Przychodzaca ma wartość false).
                        /// Trzeba poczekać na naprawę błędu 317258 i dostosować flow programu.
                    }
                }
            }
        }

        private void PrzepiszPowiazaniaWiadomosciZKontaZrodlowegoDoDocelowego(UproszczonaWiadomoscPocztowa wiadomoscNaKoncieZrodlowym, WiadomoscPocztowa wiadomoscNaKoncieDocelowym)
        {
            using (IWiadomoscPocztowa wiadomoscPocztowaBO = _wiadomosciPocztowe.Znajdz(wiadomoscNaKoncieDocelowym))
            {
                if (wiadomoscPocztowaBO == null && wiadomoscPocztowaBO.Dane == null)
                {
                    return;
                }

                bool doszloDoZmiany = false;
                if (wiadomoscPocztowaBO.Dane.KlientId == null && wiadomoscNaKoncieZrodlowym.KlientId != null)
                {
                    Podmiot klient = _klienci.Dane.Wszystkie().Where(x => x.Id == wiadomoscNaKoncieZrodlowym.KlientId).FirstOrDefault();
                    if (klient != null)
                    {
                        wiadomoscPocztowaBO.Dane.Klient = klient;
                        doszloDoZmiany = true;
                    }
                }

                if (wiadomoscPocztowaBO.Dane.DzialanieId == null && wiadomoscNaKoncieZrodlowym.DzialanieId != null)
                {
                    Dzialanie dzialanie = _dzialania.Dane.Wszystkie().Where(x => x.Id == wiadomoscNaKoncieZrodlowym.DzialanieId).FirstOrDefault();
                    if (dzialanie != null)
                    {
                        wiadomoscPocztowaBO.Dane.Dzialanie = dzialanie;
                        doszloDoZmiany = true;
                    }
                }

                if (wiadomoscPocztowaBO.Dane.DokumentId == null && wiadomoscNaKoncieZrodlowym.DokumentId != null)
                {
                    Dokument dokument = _dokumenty.Dane.Wszystkie().Where(x => x.Id == wiadomoscNaKoncieZrodlowym.DokumentId).FirstOrDefault();
                    if (dokument != null)
                    {
                        wiadomoscPocztowaBO.Dane.Dokument = dokument;
                        doszloDoZmiany = true;
                    }
                }

                if (wiadomoscPocztowaBO.Dane.ZlecenieSerwisoweId == null && wiadomoscNaKoncieZrodlowym.ZlecenieSerwisoweId != null)
                {
                    ZlecenieSerwisowe zlecenieSerwisowe = _zleceniaSerwisowe.Dane.Wszystkie().Where(x => x.Id == wiadomoscNaKoncieZrodlowym.ZlecenieSerwisoweId).FirstOrDefault();
                    if (zlecenieSerwisowe != null)
                    {
                        wiadomoscPocztowaBO.Dane.ZlecenieSerwisowe = zlecenieSerwisowe;
                        doszloDoZmiany = true;
                    }
                }

                if (wiadomoscPocztowaBO.Dane.SerwisowaneUrzadzenieId == null && wiadomoscNaKoncieZrodlowym.SerwisowaneUrzadzenieId != null && wiadomoscNaKoncieZrodlowym.SerwisowaneUrzadzenieId != 0)
                {
                    SerwisowaneUrzadzenie serwisowaneUrzadzenie = _serwisowaneUrzadzenia.Dane.Wszystkie().Where(x => x.Id == wiadomoscNaKoncieZrodlowym.SerwisowaneUrzadzenieId).FirstOrDefault();
                    if (serwisowaneUrzadzenie != null)
                    {
                        wiadomoscPocztowaBO.Dane.SerwisowaneUrzadzenie = serwisowaneUrzadzenie;
                        doszloDoZmiany = true;
                    }
                }

                if (wiadomoscPocztowaBO.Dane.ProcesOfertowyId == null && wiadomoscNaKoncieZrodlowym.ProcesOfertowyId != null)
                {
                    ProcesOfertowy procesOfertowy = _procesyOfertowe.Dane.Wszystkie().Where(x => x.Id == wiadomoscNaKoncieZrodlowym.ProcesOfertowyId).FirstOrDefault();
                    if (procesOfertowy != null)
                    {
                        wiadomoscPocztowaBO.Dane.ProcesOfertowy = procesOfertowy;
                        doszloDoZmiany = true;
                    }
                }

                if (wiadomoscPocztowaBO.Dane.ZasobId == null && wiadomoscNaKoncieZrodlowym.ZasobId != null)
                {
                    Zasob zasob = _zasoby.Dane.Wszystkie().Where(x => x.Id == wiadomoscNaKoncieZrodlowym.ZasobId).FirstOrDefault();
                    if (zasob != null)
                    {
                        wiadomoscPocztowaBO.Dane.Zasob = zasob;
                        doszloDoZmiany = true;
                    }
                }

                if (wiadomoscPocztowaBO.Dane.RezerwacjaZasobuId == null && wiadomoscNaKoncieZrodlowym.RezerwacjaZasobuId != null)
                {
                    RezerwacjaZasobu rezerwacjaZasobu = _rezerwacjeZasobow.Dane.Wszystkie().Where(x => x.Id == wiadomoscNaKoncieZrodlowym.RezerwacjaZasobuId).FirstOrDefault();
                    if (rezerwacjaZasobu != null)
                    {
                        wiadomoscPocztowaBO.Dane.RezerwacjaZasobu = rezerwacjaZasobu;
                        doszloDoZmiany = true;
                    }
                }

                if (doszloDoZmiany)
                {
                    if (wiadomoscPocztowaBO.Zapisz())
                    {
                        string temat = wiadomoscPocztowaBO.Dane.Temat;
                        if (String.IsNullOrWhiteSpace(temat))
                        {
                            temat = "(bez tematu)";
                        }

                        var uproszczonaWiadomoscPocztowa = new UproszczonaWiadomoscPocztowa
                        {
                            Id = wiadomoscPocztowaBO.Dane.Id,
                            Temat = temat,
                        };

                        _przetworzoneWiadomosciPocztowe.Add(uproszczonaWiadomoscPocztowa);
                    }
                }
            }
        }

        public void PokazWiadomoscPocztowaWOknie(UproszczonaWiadomoscPocztowa uproszczonaWiadomosc)
        {
            IWiadomosciPocztowe wiadomosciPocztowe = _sfera.PodajObiektTypu<IWiadomosciPocztowe>();
            var wiadomoscPocztowa = wiadomosciPocztowe.Dane.Wszystkie().Where(x => x.Id == uproszczonaWiadomosc.Id).FirstOrDefault();
            IWiadomoscPocztowa wiadomoscBO = wiadomosciPocztowe.Znajdz(wiadomoscPocztowa);
            IWiadomoscPocztowaOkno okno = _sfera.PodajObiektTypu<IWiadomoscPocztowaOkno>();
            KopiaWiadomosciPocztowej kopia = wiadomoscPocztowa.Kopie.FirstOrDefault();
            okno.Popraw(wiadomoscBO, kopia);
        }

        public void RozlaczSfere()
        {
            if (_sfera != null)
            {
                _sfera.Dispose();
                _sfera = null;
            }
        }
    }
}
