using InsERT.Moria.Asortymenty;
using InsERT.Moria.Bank;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.ModelOrganizacyjny;
using InsERT.Moria.Sfera;
using System;
using System.Linq;

namespace RealizacjeDokumentow.Przyklady
{
    public abstract class RealizacjaBase
    {
        protected readonly Uchwyt _sfera;

        public RealizacjaBase(Uchwyt sfera)
        {
            _sfera = sfera ?? throw new ArgumentNullException(nameof(sfera));
        }

        protected DokumentDS ZapiszDokumentSprzedazy(IDokumentSprzedazy dokument)
        {
            if (dokument.Zapisz())
            {
                Console.WriteLine($"Zapisano dokument sprzedaży o numerze {dokument.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                int identyfikatorDokumentu = dokument.Dane.Id;
                return _sfera.DokumentySprzedazy().Dane.Pierwszy(ds => ds.Id == identyfikatorDokumentu);
            }
            else
            {
                dokument.WypiszBledy();
                return null;
            }
        }

        protected DokumentDZ ZapiszDokumentZakupu(IDokumentZakupu dokument)
        {
            if (dokument.Zapisz())
            {
                Console.WriteLine($"Zapisano dokument zakupu o numerze {dokument.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                int identyfikatorDokumentu = dokument.Dane.Id;
                return _sfera.DokumentyZakupu().Dane.Pierwszy(ds => ds.Id == identyfikatorDokumentu);
            }
            else
            {
                dokument.WypiszBledy();
                return null;
            }
        }

        protected DokumentKDS ZapiszKorekteDokumentuSprzedazy(IKorektaDokumentuSprzedazy korekta)
        {
            if (korekta.Zapisz())
            {
                Console.WriteLine($"Zapisano korektę dokumentu sprzedaży o numerze {korekta.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                int identyfikatorDokumentu = korekta.Dane.Id;
                return _sfera.KorektyDokumentowSprzedazy().Dane.Pierwszy(ds => ds.Id == identyfikatorDokumentu);
            }
            else
            {
                korekta.WypiszBledy();
                return null;
            }
        }

        protected DokumentWZ ZapiszWydanieZewnetrzne(IWydanieZewnetrzne wydanie)
        {
            if (wydanie.Zapisz())
            {
                Console.WriteLine($"Poprawnie zapisano wydanie nr {wydanie.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                int identyfikatorWydania = wydanie.Dane.Id;
                return _sfera.WydaniaZewnetrzne().Dane.Pierwszy(wz => wz.Id == identyfikatorWydania);
            }
            else
            {
                wydanie.WypiszBledy();
                return null;
            }
        }


        protected DokumentPZ ZapiszPrzyjecieZewnetrzne(IPrzyjecieZewnetrzne przyjecie)
        {
            if (przyjecie.Zapisz())
            {
                Console.WriteLine($"Poprawnie zapisano przyjęcie nr {przyjecie.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                int identyfikatorWydania = przyjecie.Dane.Id;
                return _sfera.PrzyjeciaZewnetrzne().Dane.Pierwszy(pz => pz.Id == identyfikatorWydania);
            }
            else
            {
                przyjecie.WypiszBledy();
                return null;
            }
        }

        protected DokumentRW ZapiszRozchodWewnetrzny(IRozchodWewnetrzny rozchod)
        {
            if (rozchod.Zapisz())
            {
                Console.WriteLine($"Poprawnie zapisano rozchód nr {rozchod.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                int identyfikatorrozchodu = rozchod.Dane.Id;
                return _sfera.RozchodyWewnetrzne().Dane.Pierwszy(wz => wz.Id == identyfikatorrozchodu);
            }
            else
            {
                rozchod.WypiszBledy();
                return null;
            }
        }

        protected DokumentPW ZapiszPrzychodWewnetrzny(IPrzychodWewnetrzny przychod)
        {
            if (przychod.Zapisz())
            {
                Console.WriteLine($"Poprawnie zapisano przychód nr {przychod.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                int identyfikatorrozchodu = przychod.Dane.Id;
                return _sfera.PrzychodyWewnetrzne().Dane.Pierwszy(wz => wz.Id == identyfikatorrozchodu);
            }
            else
            {
                przychod.WypiszBledy();
                return null;
            }
        }

        protected void Ustaw100ProcentPlatnosciOdroczonejKlienta(string symbolKlienta)
        {
            IPodmioty podmioty = _sfera.Podmioty();
            using (IPodmiot podmiot = podmioty.Znajdz(p => p.Sygnatura.PelnaSygnatura == symbolKlienta))
            {
                if (podmiot.Dane.DomyslneFormyPlatnosci.Any())
                    podmiot.Dane.DomyslneFormyPlatnosci.Clear();
                FormaPlatnosciPodmiotu odroczona100Procent = new FormaPlatnosciPodmiotu();
                podmiot.Dane.DomyslneFormyPlatnosci.Add(odroczona100Procent);
                odroczona100Procent.FormaPlatnosci = _sfera.FormyPlatnosci().DaneDomyslne.Przelew;
                odroczona100Procent.Procent = 100m;
                if (podmiot.Zapisz())
                {
                    Console.WriteLine("Pomyślnie zapisano domyślną formę płatności klienta.");
                }
                else
                {
                    podmiot.WypiszBledy();
                }
            }
        }

        protected PozycjaHarmonogramuRozrachunku DodajPrzedplateBankowa(string symbolKlienta, bool wplyw = true)
        {
            int? identyfikator = null;
            IOperacjeBankowe operacje = _sfera.OperacjeBankowe();
            using (IOperacjaBankowa operacja = operacje.Utworz())
            {
                operacja.Dane.Wplyw = wplyw;
                operacja.Dane.Kontrahent = _sfera.Podmioty().Dane.Wszystkie().Where(p => p.Sygnatura.PelnaSygnatura == symbolKlienta).FirstOrDefault();
                operacja.Dane.Kwota = 100m;
                operacja.Dane.Tytul = $"zaliczka od {symbolKlienta}";
                if (operacja.Zapisz())
                {
                    Console.WriteLine("Poprawnie zapisano przedpłatę.");
                    identyfikator = operacja.Dane.Id;
                }
                else
                {
                    operacja.WypiszBledy();
                }
            }
            if (identyfikator.HasValue)
            {
                PozycjaHarmonogramuRozrachunku przedplata = operacje.Dane.Wszystkie().Where(o => o.Id == identyfikator.Value).SelectMany(o => o.Rozrachunek.Pozycje).FirstOrDefault();
                if (przedplata == null)
                    throw new InvalidOperationException("Nie udało się odnaleźć pozycji harmonogramu rozrachunku utworzonej przez zapisaną operację bankową.");
                return przedplata;
            }
            return null;
        }

        /// <summary>
        /// Dodaje wskazaną przedpłatę na 100% wartości dokumentu, płatność odroczoną na 100% wartości dokumentu lub płatność natychmiastową na 50% wartości dokumentu.
        /// </summary>
        /// <param name="przedplata">Pozycja harmonogramu rozrachunku wybranej przedpłaty.</param>
        /// <param name="formaPlatnosci">Forma płatności do dodania.</param>
        private void DodajPlatnosci(IPlatnosciNaDokumencie platnosci, decimal kwotaDoZaplaty, PozycjaHarmonogramuRozrachunku przedplata, FormaPlatnosci formaPlatnosci)
        {
            if (przedplata != null)
            {
                _ = platnosci.DodajPrzedplate(przedplata);
            }
            if (formaPlatnosci != null)
            {
                if (formaPlatnosci.TypPlatnosci.Odroczony)
                {
                    _ = platnosci.DodajPlatnoscOdroczona(formaPlatnosci, kwotaDoZaplaty);
                }
                else
                {
                    _ = platnosci.DodajPlatnoscNatychmiastowa(formaPlatnosci, Math.Round(kwotaDoZaplaty * 0.5m, 2, MidpointRounding.AwayFromZero));
                }
            }
        }

        protected DokumentZK DodajZamowienieOdKlienta(string symbolKlienta, PozycjaHarmonogramuRozrachunku przedplata, FormaPlatnosci formaPlatnosci, params string[] symboleAsortymentu)
        {
            Console.WriteLine($"Dodawanie zamówienia. Symbol klienta: {symbolKlienta}.");
            IZamowieniaOdKlientow zamowienia = _sfera.ZamowieniaOdKlientow();
            int? identyfikatorZamowienia = null;
            using (IZamowienieOdKlienta zamowienie = zamowienia.UtworzZamowienieOdKlienta())
            {
                zamowienie.PodmiotyDokumentu.UstawZamawiajacegoWedlugSymbolu(symbolKlienta);
                foreach (string symbol in symboleAsortymentu)
                {
                    zamowienie.Pozycje.Dodaj(symbol);
                }
                zamowienie.Przelicz();
                DodajPlatnosci(zamowienie.Platnosci, zamowienie.Dane.KwotaDoZaplaty, przedplata, formaPlatnosci);
                Console.WriteLine($"Zapis zamówienia. Liczba pozycji: {zamowienie.Dane.Pozycje.Count}.");
                if (zamowienie.Zapisz())
                {
                    Console.WriteLine($"Poprawnie zapisano zamówienie nr {zamowienie.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                    identyfikatorZamowienia = zamowienie.Dane.Id;
                }
                else
                {
                    zamowienie.WypiszBledy();
                }
            }
            if (identyfikatorZamowienia.HasValue)
            {
                DokumentZK utworzoneZamowienie = zamowienia.Dane.Pierwszy(zk => zk.Id == identyfikatorZamowienia.Value);
                if (utworzoneZamowienie == null)
                    throw new InvalidOperationException($"Nie udało się odnaleźć zapisanego zamówienia o identyfikatorze {identyfikatorZamowienia.Value}");
                return utworzoneZamowienie;
            }
            return null;
        }

        protected DokumentZD DodajZamowienieDoDostawcy(string symbolKlienta, PozycjaHarmonogramuRozrachunku przedplata, FormaPlatnosci formaPlatnosci, params string[] symboleAsortymentu)
        {
            Console.WriteLine($"Dodawanie zamówienia. Symbol klienta: {symbolKlienta}.");
            IZamowieniaDoDostawcow zamowienia = _sfera.ZamowieniaDoDostawcow();
            int? identyfikatorZamowienia = null;
            using (IZamowienieDoDostawcy zamowienie = zamowienia.Utworz())
            {
                zamowienie.PodmiotyDokumentu.UstawDostawceWedlugSymbolu(symbolKlienta);
                foreach (string symbol in symboleAsortymentu)
                {
                    zamowienie.Pozycje.Dodaj(symbol);
                }
                zamowienie.Przelicz();
                DodajPlatnosci(zamowienie.Platnosci, zamowienie.Dane.KwotaDoZaplaty, przedplata, formaPlatnosci);
                Console.WriteLine($"Zapis zamówienia. Liczba pozycji: {zamowienie.Dane.Pozycje.Count}.");
                if (zamowienie.Zapisz())
                {
                    Console.WriteLine($"Poprawnie zapisano zamówienie nr {zamowienie.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                    identyfikatorZamowienia = zamowienie.Dane.Id;
                }
                else
                {
                    zamowienie.WypiszBledy();
                }
            }
            if (identyfikatorZamowienia.HasValue)
            {
                DokumentZD utworzoneZamowienie = zamowienia.Dane.Pierwszy(zk => zk.Id == identyfikatorZamowienia.Value);
                if (utworzoneZamowienie == null)
                    throw new InvalidOperationException($"Nie udało się odnaleźć zapisanego zamówienia o identyfikatorze {identyfikatorZamowienia.Value}");
                return utworzoneZamowienie;
            }
            return null;
        }

        protected DokumentZPM DodajZlecenieProdukcyjne()
        {
            Console.WriteLine("Dodawanie zlecenia montażu.");
            IZleceniaProdukcyjneMontowania zlecenia = _sfera.ZleceniaProdukcyjneMontowania();
            int? identyfikatorZlecenia = null;
            using (IZlecenieProdukcyjneMontowania zlecenie = zlecenia.Utworz())
            {
                zlecenie.Dane.StatusDokumentu = _sfera.StatusyDokumentow().DaneDomyslne.ZlecenieProdukcyjne_DoRealizacji;
                zlecenie.Montuj(_sfera.Asortymenty().Dane.WyszukajPoSymbolu("ZESO20"));
                Console.WriteLine("Zapis zlecenia.");
                if (zlecenie.Zapisz())
                {
                    Console.WriteLine($"Poprawnie zapisano zlecenie nr {zlecenie.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                    identyfikatorZlecenia = zlecenie.Dane.Id;
                }
                else
                {
                    zlecenie.WypiszBledy();
                }
            }
            if (identyfikatorZlecenia.HasValue)
            {
                DokumentZPM utworzoneZlecenie = zlecenia.Dane.Pierwszy(zk => zk.Id == identyfikatorZlecenia.Value);
                if (utworzoneZlecenie == null)
                    throw new InvalidOperationException($"Nie udało się odnaleźć zapisanego zlecenia o identyfikatorze {identyfikatorZlecenia.Value}");
                return utworzoneZlecenie;
            }
            return null;
        }

        protected void UstawDostawceAsortymentu(Asortyment asortyment, Podmiot dostawca)
        {
            using (IAsortyment edycjaAsortymentu = _sfera.Asortymenty().Znajdz(asortyment))
            {
                DaneAsortymentuDlaPodmiotu dane = edycjaAsortymentu.Dostawcy.Dodaj(dostawca);
                dane.CenaDeklarowana = 100m;
                if (!edycjaAsortymentu.Zapisz())
                    edycjaAsortymentu.WypiszBledy();
            }
        }

        protected Asortyment DodajAsortymentZDostawcaIIlosciaOptymalna(decimal iloscOptymalna, Podmiot dostawca)
        {
            if (iloscOptymalna < 1m)
                throw new ArgumentOutOfRangeException(nameof(iloscOptymalna), "Ilość musi być większa lub równa 1");
            int? id = null;
            IAsortymenty asortymenty = _sfera.Asortymenty();
            ISzablonyAsortymentu szablony = _sfera.SzablonyAsortymentu();
            IMagazyny magazyny = _sfera.Magazyny();
            using (IAsortyment asortyment = asortymenty.Utworz())
            {
                asortyment.WypelnijNaPodstawieSzablonu(szablony.DaneDomyslne.Towar);
                asortyment.AutoSymbol();
                asortyment.Dane.Nazwa = "Asortyment wygenerowany";
                foreach (Magazyn magazyn in magazyny.Dane.Wszystkie())
                {
                    StanWMagazynieZakres stan = new StanWMagazynieZakres();
                    asortyment.Dane.StanyWMagazynachZakresy.Add(stan);
                    stan.StanMinimalny = 1m;
                    stan.StanOptymalny = iloscOptymalna;
                    stan.Magazyn = magazyn;
                }
                DaneAsortymentuDlaPodmiotu dane = asortyment.Dostawcy.Dodaj(dostawca);
                dane.CenaDeklarowana = 100m;
                if (asortyment.Zapisz())
                {
                    id = asortyment.Dane.Id;
                }
                else
                {
                    asortyment.WypiszBledy();
                }
            }
            return id.HasValue ? asortymenty.Dane.Pierwszy(a => a.Id == id.Value) : null;
        }

        protected void ZmienObslugePlatnosci(Guid identyfikatorKonfiguracji, bool obsluguj)
        {
            using (IKonfiguracja parametryKonfiguracji = _sfera.Konfiguracje().Znajdz(k => k.Id == identyfikatorKonfiguracji))
            {
                parametryKonfiguracji.ParametryKonfiguracji.PosiadaAspektFinansowy = obsluguj;
                if (parametryKonfiguracji.Zapisz())
                {
                    Console.WriteLine("Pomyślnie zapisano parametr obsługi płatności.");
                }
                else
                {
                    parametryKonfiguracji.WypiszBledy();
                }
            }
        }

        protected DokumentWZ DodajWydanieZewnetrzne(string symbolKlienta, PozycjaHarmonogramuRozrachunku przedplata, FormaPlatnosci formaPlatnosci, params string[] symboleAsortymentu)
        {
            Console.WriteLine($"Dodawanie wydania. Symbol klienta: {symbolKlienta}.");
            IWydaniaZewnetrzne wydania = _sfera.WydaniaZewnetrzne();
            using (IWydanieZewnetrzne wydanie = wydania.UtworzWydanieZewnetrzne())
            {
                wydanie.PodmiotyDokumentu.UstawOdbiorceWedlugSymbolu(symbolKlienta);
                foreach (string symbol in symboleAsortymentu)
                {
                    wydanie.Pozycje.Dodaj(symbol);
                }
                wydanie.Przelicz();
                DodajPlatnosci(wydanie.Platnosci, wydanie.Dane.KwotaDoZaplaty, przedplata, formaPlatnosci);
                Console.WriteLine($"Zapis wydania. Liczba pozycji: {wydanie.Dane.Pozycje.Count}.");
                return ZapiszWydanieZewnetrzne(wydanie);
            }
        }

        protected DokumentPZ DodajPrzyjecieZewnetrzne(string symbolKlienta, PozycjaHarmonogramuRozrachunku przedplata, FormaPlatnosci formaPlatnosci, params string[] symboleAsortymentu)
        {
            Console.WriteLine($"Dodawanie przyjęcia. Symbol klienta: {symbolKlienta}.");
            IPrzyjeciaZewnetrzne przyjecia = _sfera.PrzyjeciaZewnetrzne();
            using (IPrzyjecieZewnetrzne przyjecie = przyjecia.UtworzPrzyjecieZewnetrzne())
            {
                przyjecie.PodmiotyDokumentu.UstawDostawceWedlugSymbolu(symbolKlienta);
                foreach (string symbol in symboleAsortymentu)
                {
                    przyjecie.Pozycje.Dodaj(symbol);
                }
                przyjecie.Przelicz();
                DodajPlatnosci(przyjecie.Platnosci, przyjecie.Dane.KwotaDoZaplaty, przedplata, formaPlatnosci);
                Console.WriteLine($"Zapis przyjęcia. Liczba pozycji: {przyjecie.Dane.Pozycje.Count}.");
                return ZapiszPrzyjecieZewnetrzne(przyjecie);
            }
        }

        protected DokumentDS DodajParagon(string nipNabywcy, params string[] symboleAsortymentu)
        {
            Console.WriteLine($"Dodawanie paragonu.");
            IDokumentySprzedazy dokumentySprzedazy = _sfera.DokumentySprzedazy();
            int? identyfikatorParagonu = null;
            using (IDokumentSprzedazy paragon = dokumentySprzedazy.UtworzParagon())
            {
                if (!string.IsNullOrEmpty(nipNabywcy))
                {
                    paragon.Dane.SposobWskazaniaKontrahenta = (byte)SposobWskazaniaKontrahenta.NIP;
                    paragon.Dane.IdentyfikatorKontrahenta = nipNabywcy;
                }
                foreach (string symbol in symboleAsortymentu)
                {
                    paragon.Pozycje.Dodaj(symbol);
                }
                paragon.Przelicz();
                paragon.Platnosci.DodajDomyslnaPlatnoscNatychmiastowaNaKwoteDokumentu();
                Console.WriteLine($"Zapis paragonu. Liczba pozycji: {paragon.Dane.Pozycje.Count}.");
                if (paragon.Zapisz())
                {
                    Console.WriteLine($"Poprawnie zapisano paragon nr {paragon.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                    identyfikatorParagonu = paragon.Dane.Id;
                }
                else
                {
                    paragon.WypiszBledy();
                }
            }
            if (identyfikatorParagonu.HasValue)
            {
                DokumentDS utworzonyParagon = dokumentySprzedazy.Dane.Pierwszy(ds => ds.Id == identyfikatorParagonu.Value);
                if (utworzonyParagon == null)
                    throw new InvalidOperationException($"Nie udało się odnaleźć zapisanego paragonu o identyfikatorze {identyfikatorParagonu.Value}");
                return utworzonyParagon;
            }
            return null;
        }
    }
}
