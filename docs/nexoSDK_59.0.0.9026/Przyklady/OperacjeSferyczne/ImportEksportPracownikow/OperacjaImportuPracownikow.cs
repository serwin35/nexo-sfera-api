using InsERT.Moria.Klienci;
using InsERT.Moria.Listy;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using InsERT.Moria.Sfera;
using Microsoft.Win32;
using OperacjeSferyczne.ImportEksportPracownikow.Model;
using OperacjeSferyczne.Narzedzia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace OperacjeSferyczne.ImportEksportPracownikow
{
    internal class OperacjaImportuPracownikow : OperacjaNaLiscieDanych<Pracownik, int>
    {
        protected override string[] SciezkaWMenu => new string[] { "Import/eksport pracowników" };

        public override string Nazwa => "Importuj";

        protected override bool SprawdzCzyMoznaWykonac(IReadOnlyCollection<int> identyfikatoryWybranychElementow, IKontekstListyDanych kontekstListyDanych, IKontekstOperacji kontekstOperacji)
        {
            return true;
        }

        protected override void Wykonaj(IReadOnlyCollection<int> identyfikatoryWybranychElementow, IKontekstListyDanych kontekstListyDanych, IKontekstOperacji kontekstOperacji)
        {
            if (PodajOSciezkeDoPliku(out string sciezka))
            {
                var danePracownikow = WczytajDanePracownikow(sciezka);

                if (danePracownikow == null || !danePracownikow.Pracownicy.Any())
                {
                    Okna.PokazOknoZBledem(kontekstOperacji.Uchwyt, "Wskazany plik nie zawiera danych pracowników w wymaganym formacie.");
                    return;
                }

                ImportujDanePracownikow(kontekstOperacji, danePracownikow);
            }
        }

        private DanePracownikow WczytajDanePracownikow(string sciezka)
        {
            var serializer = new XmlSerializer(typeof(DanePracownikow));
            using (var streamReader = new StreamReader(sciezka))
            {
                return (DanePracownikow)serializer.Deserialize(streamReader);
            }
        }

        private void ImportujDanePracownikow(IKontekstOperacji kontekstOperacji, DanePracownikow danePracownikow)
        {
            try
            {
                var okno = kontekstOperacji.Uchwyt.PodajObiektTypu<IOknoOperacjiZbiorczej>();
                okno.Tytul = "Import danych pracowników";
                okno.TekstPrzyciskuWykonaj = "Importuj";
                okno.Pokaz(danePracownikow.Pracownicy, x => ZaimportujPracownika(kontekstOperacji, x));
            }
            finally
            {
                WyczyscCache();
            }
        }

        private WynikOperacji ZaimportujPracownika(IKontekstOperacji kontekstOperacji, DanePracownika danePracownika)
        {
            try
            {
                var podmiotyMenadzer = kontekstOperacji.Uchwyt.Podmioty();
                using (var podmiotBO = podmiotyMenadzer.UtworzPracownika())
                {
                    var podmiot = podmiotBO.Dane;
                    var osoba = podmiot.Osoba;
                    osoba.Imie = danePracownika.Imie;
                    osoba.Nazwisko = danePracownika.Nazwisko;
                    osoba.PESEL = danePracownika.Pesel;
                    osoba.DataUrodzenia = danePracownika.DataUrodzenia;
                    osoba.Plec = (byte)Enum.Parse(typeof(Plec), danePracownika.Plec);
                    if (danePracownika.AdresZamieszkania != null)
                    {
                        var adresZamieszkania = podmiot.AdresPodstawowy ?? podmiotBO.DodajAdres(TypAdresuGlowny(kontekstOperacji.Uchwyt));
                        ImportujAdres(kontekstOperacji.Uchwyt, adresZamieszkania, danePracownika.AdresZamieszkania);
                    }
                    if (danePracownika.AdresKorespondencyjny != null)
                    {
                        var adresKorespondencyjny = podmiot.DomyslnyAdresKorespondencyjny ?? podmiotBO.DodajAdres(TypAdresuKorespondencyjny(kontekstOperacji.Uchwyt));
                        ImportujAdres(kontekstOperacji.Uchwyt, adresKorespondencyjny, danePracownika.AdresKorespondencyjny);
                    }
                    if (danePracownika.AdresZameldowania != null)
                    {
                        var adresZameldowania = podmiot.DomyslnyAdresZameldowania ?? podmiotBO.DodajAdres(TypAdresuZameldowania(kontekstOperacji.Uchwyt));
                        ImportujAdres(kontekstOperacji.Uchwyt, adresZameldowania, danePracownika.AdresZameldowania);
                    }
                    ImportujKontakty(kontekstOperacji.Uchwyt, podmiot, danePracownika.Kontakty);

                    if (!podmiotBO.Zapisz())
                    {
                        return WynikOperacji.ZakonczonaNiepowodzeniem($"Podczas importu pracownika wystąpiły błędy:\r\n{String.Join("\r\n", podmiotBO.PobierzKomunikatyBledow().Select(x => "- " + x.Tresc))}");
                    }
                    else
                    {
                        return WynikOperacji.ZakonczonaPowodzeniem();
                    }
                }
            }
            catch (ImportPracownikaException e)
            {
                return WynikOperacji.ZakonczonaNiepowodzeniem(e.Message);
            }
        }

        private void ImportujKontakty(IUchwyt uchwyt, Podmiot podmiot, IEnumerable<DaneKontaktu> kontakty)
        {
            foreach (var daneKontaktu in kontakty)
            {
                var kontakt = new Kontakt();
                podmiot.Kontakty.Add(kontakt);
                kontakt.Rodzaj = ZnajdzRodzajKontaktu(uchwyt, daneKontaktu.Rodzaj);
                kontakt.Wartosc = daneKontaktu.Wartosc;
                kontakt.Podstawowy = daneKontaktu.Podstawowy;
                kontakt.Komentarz = daneKontaktu.Komentarz;
            }
        }

        private void ImportujAdres(IUchwyt uchwyt, AdresPodmiotu adres, DaneAdresowe daneAdresowe)
        {
            if (adres.Szczegoly != null)
                adres.Szczegoly = new AdresSzczegoly();

            adres.Nazwa = daneAdresowe.Nazwa;
            adres.Szczegoly.Ulica = daneAdresowe.Ulica;
            adres.Szczegoly.NrDomu = daneAdresowe.NrDomu;
            adres.Szczegoly.NrLokalu = daneAdresowe.NrLokalu;
            adres.Szczegoly.Miejscowosc = daneAdresowe.Miejscowosc;
            adres.Szczegoly.KodPocztowy = daneAdresowe.KodPocztowy;
            adres.Szczegoly.Poczta = daneAdresowe.Poczta;
            adres.Panstwo = ZnajdzPanstwo(uchwyt, daneAdresowe.Panstwo);
            adres.Szczegoly.Wojewodztwo = ZnajdzWojewodztwo(uchwyt, adres.Panstwo, daneAdresowe.Wojewodztwo);
            var powiat = ZnajdzPowiat(uchwyt, adres.Szczegoly.Wojewodztwo, daneAdresowe.Powiat);
            adres.Szczegoly.Gmina = ZnajdzGmine(uchwyt, powiat, daneAdresowe.Gmina);
        }

        private bool PodajOSciezkeDoPliku(out string sciezka)
        {
            var dialog = new OpenFileDialog();

            dialog.Filter = "Pliki XML (*.xml)|*.xml";
            dialog.Title = "Wybierz plik z wyeksportowanymi danymi pracowników";
            dialog.CheckFileExists = true;

            var result = dialog.ShowDialog();
            if (result == true)
            {
                sciezka = dialog.FileName;
                return true;
            }
            else
            {
                sciezka = null;
                return false;
            }
        }

        private TypAdresu _typAdresuGlowny;
        private TypAdresu TypAdresuGlowny(IUchwyt uchwyt)
        {
            if (_typAdresuGlowny == null)
            {
                _typAdresuGlowny = uchwyt.TypyAdresu().DaneDomyslne.Glowny;
            }

            return _typAdresuGlowny;
        }

        private TypAdresu _typAdresuKorespondencyjny;
        private TypAdresu TypAdresuKorespondencyjny(IUchwyt uchwyt)
        {
            if (_typAdresuKorespondencyjny == null)
            {
                _typAdresuKorespondencyjny = uchwyt.TypyAdresu().DaneDomyslne.Korespondencyjny;
            }

            return _typAdresuKorespondencyjny;
        }

        private TypAdresu _typAdresuZameldowania;
        private TypAdresu TypAdresuZameldowania(IUchwyt uchwyt)
        {
            if (_typAdresuZameldowania == null)
            {
                _typAdresuZameldowania = uchwyt.TypyAdresu().DaneDomyslne.Zameldowania;
            }

            return _typAdresuZameldowania;
        }

        private readonly IDictionary<string, Panstwo> _panstwaCache = new Dictionary<string, Panstwo>();
        private Panstwo ZnajdzPanstwo(IUchwyt uchwyt, string nazwaPanstwa)
        {
            if (String.IsNullOrWhiteSpace(nazwaPanstwa))
                return null;

            return _panstwaCache.GetOrAdd(nazwaPanstwa, () =>
            {
                var panstwaMenadzer = uchwyt.Panstwa();
                var panstwo = panstwaMenadzer.Dane.Pierwszy(x => x.Nazwa == nazwaPanstwa);
                if (panstwo == null)
                {
                    using (var panstwoBO = panstwaMenadzer.Utworz())
                    {
                        panstwo = panstwoBO.Dane;
                        panstwoBO.Dane.Nazwa = nazwaPanstwa;

                        if (!panstwoBO.Zapisz())
                        {
                            throw new ImportPracownikaException($"Podczas importu państwa '{nazwaPanstwa}' wystąpiły błędy:\r\n{String.Join("\r\n", panstwoBO.PobierzKomunikatyBledow().Select(x => "- " + x.Tresc))}");
                        }
                    }
                }

                return panstwo;
            });
        }

        private readonly IDictionary<(int PanstwoId, string NazwaWojewodztwa), Wojewodztwo> _wojewodztwoCache = new Dictionary<(int, string), Wojewodztwo>();
        private Wojewodztwo ZnajdzWojewodztwo(IUchwyt uchwyt, Panstwo panstwo, string nazwaWojewodztwa)
        {
            if (panstwo == null || String.IsNullOrWhiteSpace(nazwaWojewodztwa))
                return null;

            return _wojewodztwoCache.GetOrAdd((panstwo.Id, nazwaWojewodztwa), () =>
            {
                var wojewodztwaMenadzer = uchwyt.Wojewodztwa();
                var wojewodztwo = wojewodztwaMenadzer.Dane.Pierwszy(x => x.Panstwo.Id == panstwo.Id && x.Nazwa == nazwaWojewodztwa);
                if (wojewodztwo == null)
                {
                    using (var wojewodztwoBO = wojewodztwaMenadzer.Utworz())
                    {
                        wojewodztwo = wojewodztwoBO.Dane;
                        wojewodztwoBO.Dane.Panstwo = panstwo;
                        wojewodztwoBO.Dane.Nazwa = nazwaWojewodztwa;

                        if (!wojewodztwoBO.Zapisz())
                        {
                            throw new ImportPracownikaException($"Podczas importu województwa '{nazwaWojewodztwa}' wystąpiły błędy:\r\n{String.Join("\r\n", wojewodztwoBO.PobierzKomunikatyBledow().Select(x => "- " + x.Tresc))}");
                        }
                    }
                }

                return wojewodztwo;
            });
        }

        private readonly IDictionary<(int WojewodztwoId, string NazwaPowiatu), Powiat> _powiatCache = new Dictionary<(int, string), Powiat>();
        private Powiat ZnajdzPowiat(IUchwyt uchwyt, Wojewodztwo wojewodztwo, string nazwaPowiatu)
        {
            if (wojewodztwo == null || String.IsNullOrWhiteSpace(nazwaPowiatu))
                return null;

            return _powiatCache.GetOrAdd((wojewodztwo.Id, nazwaPowiatu), () =>
            {
                var powiatyMenadzer = uchwyt.Powiaty();
                var powiat = powiatyMenadzer.Dane.Pierwszy(x => x.Wojewodztwo.Id == wojewodztwo.Id && x.Nazwa == nazwaPowiatu);
                if (powiat == null)
                {
                    using (var powiatBO = powiatyMenadzer.Utworz())
                    {
                        powiat = powiatBO.Dane;
                        powiatBO.Dane.Wojewodztwo = wojewodztwo;
                        powiatBO.Dane.Nazwa = nazwaPowiatu;

                        if (!powiatBO.Zapisz())
                        {
                            throw new ImportPracownikaException($"Podczas importu powiatu '{nazwaPowiatu}' wystąpiły błędy:\r\n{String.Join("\r\n", powiatBO.PobierzKomunikatyBledow().Select(x => "- " + x.Tresc))}");
                        }
                    }
                }

                return powiat;
            });
        }

        private readonly IDictionary<(int PowiatId, string NazwaGminy, string typGminy, string kodGminy), Gmina> _gminaCache = new Dictionary<(int, string, string, string), Gmina>();
        private Gmina ZnajdzGmine(IUchwyt uchwyt, Powiat powiat, DaneGminy daneGminy)
        {
            if (powiat == null || daneGminy == null)
                return null;

            return _gminaCache.GetOrAdd((powiat.Id, daneGminy.Nazwa, daneGminy.TypGminy, daneGminy.Kod), () =>
            {
                var typGminy = (int)Enum.Parse(typeof(TypGminy), daneGminy.TypGminy);

                var gminyMenadzer = uchwyt.Gminy();
                var gmina = gminyMenadzer.Dane.Pierwszy(x => x.Powiat.Id == powiat.Id && x.Nazwa == daneGminy.Nazwa && x.TypGminy == typGminy && x.KodGUS == daneGminy.Kod);
                if (gmina == null)
                {
                    using (var gminaBO = gminyMenadzer.Utworz())
                    {
                        gmina = gminaBO.Dane;
                        gminaBO.Dane.Powiat = powiat;
                        gminaBO.Dane.Nazwa = daneGminy.Nazwa;
                        gminaBO.Dane.TypGminy = typGminy;
                        gminaBO.Dane.KodGUS = daneGminy.Kod;

                        if (!gminaBO.Zapisz())
                        {
                            throw new ImportPracownikaException($"Podczas importu gminy '{daneGminy.Nazwa}'wystąpiły błędy:\r\n{String.Join("\r\n", gminaBO.PobierzKomunikatyBledow().Select(x => "- " + x.Tresc))}");
                        }
                    }
                }

                return gmina;
            });
        }

        private readonly IDictionary<string, RodzajKontaktu> _rodzajKontaktuCache = new Dictionary<string, RodzajKontaktu>();
        private RodzajKontaktu ZnajdzRodzajKontaktu(IUchwyt uchwyt, string nazwaRodzajuKontaktu)
        {
            if (String.IsNullOrWhiteSpace(nazwaRodzajuKontaktu))
                return null;

            return _rodzajKontaktuCache.GetOrAdd(nazwaRodzajuKontaktu, () =>
            {
                var rodzajeKontaktuMenadzer = uchwyt.RodzajeKontaktu();
                var rodzajKontaktu = rodzajeKontaktuMenadzer.Dane.Pierwszy(x => x.Nazwa == nazwaRodzajuKontaktu);
                if (rodzajKontaktu == null)
                {
                    using (var rodzajKontaktuBO = rodzajeKontaktuMenadzer.Utworz())
                    {
                        rodzajKontaktu = rodzajKontaktuBO.Dane;
                        rodzajKontaktu.Nazwa = nazwaRodzajuKontaktu;

                        if (!rodzajKontaktuBO.Zapisz())
                        {
                            throw new ImportPracownikaException($"Podczas importu rodzaju kontaktu '{nazwaRodzajuKontaktu}' wystąpiły błędy:\r\n{String.Join("\r\n", rodzajKontaktuBO.PobierzKomunikatyBledow().Select(x => "- " + x.Tresc))}");
                        }
                    }
                }

                return rodzajKontaktu;
            });
        }

        private void WyczyscCache()
        {
            _panstwaCache.Clear();
            _wojewodztwoCache.Clear();
            _powiatCache.Clear();
            _gminaCache.Clear();
            _rodzajKontaktuCache.Clear();
        }
    }
}
