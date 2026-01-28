using InsERT.Moria.Asortymenty;
using InsERT.Moria.CennikiICeny;
using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Moria.Uzytkownicy;
using InsERT.Moria.Waluty;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrzykladyCenniki
{
    public class ObslugaCennikow
    {
        private readonly Uchwyt _sfera;


		private static readonly string NazwaPoziomuCen = "Nowy poziom cen";
        private static readonly string TytulCennika = "Nowy cennik ";

        public ObslugaCennikow(Uchwyt sfera)
        {
            _sfera = sfera;
        }


        /// <summary>
        /// Dodaje nowy poziom cen oraz cennik główny na całą kartotekę asortymentu.
        /// </summary>
        public ObslugaCennikow DodajCennikGlowny()
        {
            IParametryCennikow parametryCennikow = _sfera.PodajObiektTypu<IParametryCennikow>();
            ICenniki menadzerCennikow = _sfera.PodajObiektTypu<ICenniki>();
            IPoziomyCen menadzerPoziomowCen = _sfera.PodajObiektTypu<IPoziomyCen>();
            IWalutyDaneDomyslne walutyDD = _sfera.PodajObiektTypu<IWaluty>().DaneDomyslne;
            IUzytkownicy uzytkownicy = _sfera.PodajObiektTypu<IUzytkownicy>();

            ParametrCennikow glowneParametryCennikow = parametryCennikow.Dane.Wszystkie().FirstOrDefault();
            PoziomCen poziom = null;
            using (IPoziomCen poziomCen = menadzerPoziomowCen.Utworz())
            {
                poziomCen.Dane.Symbol = "NOWY";
                poziomCen.Dane.Nazwa = NazwaPoziomuCen;
                poziomCen.Dane.Waluta = walutyDD.PLN;
                poziomCen.Dane.FunkcjaWyboruCennika = FunkcjeWyboruPozycjiCennika.FunkcjaWyboruPozycjiCennika_ID; // domyślna
                poziomCen.Dane.FunkcjaWyliczaniaCenyBazowej = glowneParametryCennikow.DomyslnaFunkcjaWyliczaniaCenyBazowejTowarow;
                poziomCen.Dane.FunkcjaWyliczaniaCenyBazowejBezStanow = glowneParametryCennikow.DomyslnaFunkcjaWyliczaniaCenyBazowejUslug;
                if (poziomCen.Zapisz())
                    poziom = poziomCen.Dane;
                else
                    poziomCen.WypiszBledy();
            }
            if (poziom != null)
            {
                using (ICennik cennik = menadzerCennikow.Utworz())
                {
                    cennik.Dane.PoziomCen = poziom;
                    cennik.Dane.Bazowy = true; // ustawiamy cennik jako główny
                    cennik.Dane.Tytul = TytulCennika + poziom.Nazwa;
                    // pobieramy domyślne parametry zestawu cenowego:
                    ParametrGrupyPozycjiCennika parametr = cennik.Dane.ParametryPozycjiDomyslne();
                    parametr.WyliczajPozycjeWedlug = (int)MetodaWyliczaniaPozycjiCennika.WedlugMarzy;
                    parametr.Marza = 0.65m;
                    cennik.WypelnijCennik(); // generujemy pozycje dla całego asortymentu

                    //opublikowanie (zatwierdzenie) cennika:
                    cennik.UstawStatus(uzytkownicy.Dane.Wszystkie().First(), StatusCennika.Zatwierdzony);

                    if (cennik.Zapisz())
                        Console.WriteLine("Poprawnie zakończono zapis cennika głównego.");
                    else
                        cennik.WypiszBledy();
                }
            }

            return this;
        }

        /// <summary>
        /// Wykonuje dla cennika przecenę procentową z zaokrągleniem ceny do końcówki 0.99 dla asortymentów z podanej grupy.
        /// </summary>
        public ObslugaCennikow Przecena()
        {
            ICenniki cenniki = _sfera.PodajObiektTypu<ICenniki>();
            Cennik encjaPodstawowego = cenniki.Dane.Wszystkie().Where(c => c.Tytul == "Podstawowy").FirstOrDefault() // z danych prezentacyjnych
                ?? cenniki.Dane.Wszystkie().Where(c => c.Bazowy == true && c.Tytul == (TytulCennika + NazwaPoziomuCen)).FirstOrDefault(); // lub uprzednio dodany metodą DodajCennikGlowny()
            if (encjaPodstawowego == null)
            {
                Console.WriteLine("Nie znaleziono cennika podstawowego.");
                return this;
            }
            using (ICennik podstawowy = cenniki.Znajdz(encjaPodstawowego))
            {
                podstawowy.Przecen(
                    podstawowy.Pozycje.Wszystkie.Where(p => p.GrupaAsortymentu == "Pomadki"),
                    RodzajPrzeceny.ObnizkaProcentowa,
                    0.25m, // obniżka o 25%
                    FunkcjeWyrownywaniaCeny.WyrownywanieDoJednostek_Id,
                    false, // ujemna korekta ceny
                    0.01m, // korekta ceny o 1 grosz
                    true,
                    (v, m, d) =>
                    {
                        Console.WriteLine("Postep {0}. Maksymalny postep: {1}. {2}", v, m, d);
                        return false;
                    });
                if (podstawowy.Zapisz())
                    Console.WriteLine("Poprawnie zakończono wykonywanie przeceny.");
                else
                    podstawowy.WypiszBledy();
            }

            return this;
        }

        /// <summary>
        /// Zmienia sposób wyliczania ceny sprzedaży na 'wg marży' równej 60% dla pozycji cennika z cechą 'Kosmetyk popularny'.
        /// </summary>
        public ObslugaCennikow ZmianaSposobuWyliczaniaCenySprzedazy()
        {
            ICenniki cenniki = _sfera.PodajObiektTypu<ICenniki>();
            ICechyAsortymentu cechy = _sfera.PodajObiektTypu<ICechyAsortymentu>();
            Cennik encjaPodstawowego = cenniki.Dane.Wszystkie().Where(c => c.Tytul == "Podstawowy").FirstOrDefault() // z danych prezentacyjnych
                ?? cenniki.Dane.Wszystkie().Where(c => c.Bazowy == true && c.Tytul == (TytulCennika + NazwaPoziomuCen)).FirstOrDefault(); // lub uprzednio dodany metodą DodajCennikGlowny()
            CechaAsortymentu kosmetykPopularny = cechy.Dane.Wszystkie().Where(c => c.Nazwa == "Kosmetyk popularny").FirstOrDefault();
            if (encjaPodstawowego == null)
            {
                Console.WriteLine("Nie znaleziono cennika podstawowego.");
                return this;

            }
            if (kosmetykPopularny == null)
            {
                Console.WriteLine("Nie znaleziono cechy o nazwie 'Kosmetyk popularny'");
                return this;
            }
            using (ICennik podstawowy = cenniki.Znajdz(encjaPodstawowego))
            {
                foreach (IUproszczonaPozycjaCennika pozycja in podstawowy.Pozycje.Wszystkie.Where(p => p.IdCechAsortymentu.Contains(kosmetykPopularny.Id)))
                {
                    pozycja.RozpocznijEdycje();
                    try
                    {
                        Console.WriteLine("Edytuję pozycję cennika asortymentu {0}.", pozycja.NazwaAsortymentu);
                        pozycja.DomyslneWyliczajPozycjeWedlug = (int)MetodaWyliczaniaPozycjiCennika.WedlugMarzy;
                        pozycja.ParametrKalkulacyjny = 0.6m; // 60%
                        pozycja.PrzeliczPozycje();
                    }
                    finally
                    {
                        pozycja.ZakonczEdycje();
                    }
                }
                if (podstawowy.Zapisz())
                    Console.WriteLine("Poprawnie zakończono wykonywanie zmiany sposobu wyliczania pozycji.");
                else
                    podstawowy.WypiszBledy();
            }

            return this;
        }

        /// <summary>
        /// Dodaje nowy próg cenowy dla asortymentu o symbolu 'BANAW200' dla ilości 10.
        /// </summary>
        public ObslugaCennikow DodajProgCenowy()
        {
            ICenniki cenniki = _sfera.PodajObiektTypu<ICenniki>();
            IAsortymenty asortymenty = _sfera.PodajObiektTypu<IAsortymenty>();
            Cennik encjaPodstawowego = cenniki.Dane.Wszystkie().Where(c => c.Tytul == "Podstawowy").FirstOrDefault() // z danych prezentacyjnych
                ?? cenniki.Dane.Wszystkie().Where(c => c.Bazowy == true && c.Tytul == (TytulCennika + NazwaPoziomuCen)).FirstOrDefault(); // lub uprzednio dodany metodą DodajCennikGlowny()
            Asortyment balsam = asortymenty.Dane.Wszystkie().Where(a => a.Symbol == "BANAW200").FirstOrDefault();
            if (encjaPodstawowego == null)
            {
                Console.WriteLine("Nie znaleziono cennika podstawowego.");
                return this;
            }
            if (balsam == null)
            {
                Console.WriteLine("Nie znaleziono asortymentu o symbolu 'BANAW200'");
                return this;
            }
            using (ICennik podstawowy = cenniki.Znajdz(encjaPodstawowego))
            {
                IUproszczonaPozycjaCennika pozycjaBazowa = podstawowy.Pozycje.Wszystkie.Where(p => p.SymbolAsortymentu == "BANAW200" && p.Glowna).FirstOrDefault();
                IUproszczonaPozycjaCennika progCenowy = podstawowy.Pozycje.Dodaj(balsam);
                progCenowy.RozpocznijEdycje();
                try
                {
                    progCenowy.IloscMinAsortymentu = 10m;
                    if (pozycjaBazowa != null)
                        progCenowy.CenaBrutto = Math.Round(0.75m * pozycjaBazowa.CenaBrutto, podstawowy.Dane.Waluta.PrecyzjaCeny, MidpointRounding.AwayFromZero);
                }
                finally
                {
                    progCenowy.ZakonczEdycje();
                }
                if (podstawowy.Zapisz())
                    Console.WriteLine("Poprawnie zakończono dodawanie progu cenowego.");
                else
                    podstawowy.WypiszBledy();
            }
            return this;
        }

        /// <summary>
        /// Dodaje nowy cennik dodatkowy uzupełniający do głównego na asortyment z grupy 'Pudry' oraz ustawia rabat domyślny dla pozycji nowego cennika na 5%.
        /// </summary>
        public ObslugaCennikow DodawanieCennikaDodatkowego()
        {
            ICenniki cenniki = _sfera.PodajObiektTypu<ICenniki>();
            IAsortymenty asortymenty = _sfera.PodajObiektTypu<IAsortymenty>();
            IUzytkownicy uzytkownicy = _sfera.PodajObiektTypu<IUzytkownicy>();
            Cennik encjaPodstawowego = cenniki.Dane.Wszystkie().Where(c => c.Tytul == "Podstawowy").FirstOrDefault() // z danych prezentacyjnych
                ?? cenniki.Dane.Wszystkie().Where(c => c.Bazowy == true && c.Tytul == (TytulCennika + NazwaPoziomuCen)).FirstOrDefault(); // lub uprzednio dodany metodą DodajCennikGlowny()
            if (encjaPodstawowego == null)
            {
                Console.WriteLine("Nie znaleziono cennika podstawowego.");
                return this;
            }
            using (ICennik cennik = cenniki.Utworz())
            {
                cennik.Dane.Tytul = "Uzupełniający do podstawowego";
                cennik.UstawJakoDodatkowy(encjaPodstawowego);
                cennik.UstawStatus(uzytkownicy.Dane.Wszystkie().Where(u => u.Login == "Szef").FirstOrDefault(), StatusCennika.Zatwierdzony);

                IEnumerable<IUproszczonaPozycjaCennika> dodanePozycje =
                    cennik.Dodaj(asortymenty.Dane.Wszystkie().Where(a => a.Grupa != null && a.Grupa.Nazwa == "Pudry").Select(a => a.Id),
                    (v, m, d) =>
                    {
                        Console.WriteLine("Postęp: {0}. Maksymalny postęp: {1}. {2}", v, m, d);
                        return false;
                    });
                foreach (IUproszczonaPozycjaCennika pozycja in dodanePozycje)
                {
                    pozycja.RozpocznijEdycje();
                    try
                    {
                        pozycja.RabatDopuszczalny = 0.10m;
                        pozycja.RabatDomyslny = 0.05m;
                    }
                    finally
                    {
                        pozycja.ZakonczEdycje();
                    }
                }
                if (cennik.Zapisz())
                    Console.WriteLine("Poprawnie zakończono dodawanie powiązanego cennika dodatkowego.");
                else
                    cennik.WypiszBledy();
            }
            return this;
        }

        /// <summary>
        /// Dodaje nowy cennik dodatkowy niepowiązany z głównym i ustawia go jako cennik dodatkowy dla klientów z grupy 'Drogerie'.
        /// </summary>
        public ObslugaCennikow DodawanieCennikaDodatkowegoDlaKlienta()
        {
            ICenniki cenniki = _sfera.PodajObiektTypu<ICenniki>();
            IAsortymenty asortymenty = _sfera.PodajObiektTypu<IAsortymenty>();
            IUzytkownicy uzytkownicy = _sfera.PodajObiektTypu<IUzytkownicy>();
            IPodmioty podmioty = _sfera.PodajObiektTypu<IPodmioty>();
            int identyfikatorCennikaDodatkowego = 0;

            using (ICennik cennik = cenniki.Utworz())
            {
                cennik.Dane.Tytul = "Dodatkowy dla klientów z grupy 'Drogerie'";
                cennik.UstawStatus(uzytkownicy.Dane.Wszystkie().Where(u => u.Login == "Szef").FirstOrDefault(), StatusCennika.Zatwierdzony);
                ParametrGrupyPozycjiCennika parametr = cennik.Dane.ParametryPozycjiDomyslne();
                parametr.FunkcjaWyliczaniaCenyBazowej = FunkcjeWyliczaniaCenyBazowej.FunkcjaWyliczaniaCenyBazowejWgCenyEwidencyjnej_ID;
                parametr.Narzut = 0.2m; // 20 %
                parametr.WyliczajPozycjeWedlug = (int)MetodaWyliczaniaPozycjiCennika.WedlugNarzutu;
                parametr.FunkcjaWyrownywaniaCen = FunkcjeWyrownywaniaCeny.WyrownywanieDoCzesciDziesietnej_0_5_Id;
                parametr.ZnakKorektyCeny = false; // minus
                parametr.KorektaCeny = 0.01m;

                IEnumerable<IUproszczonaPozycjaCennika> dodanePozycje =
                    cennik.Dodaj(asortymenty.Dane.Wszystkie().Where(a => a.Cechy.Any(c => c.Nazwa == "Produkt dla mężczyzn")).Select(a => a.Id),
                    (v, m, d) =>
                    {
                        Console.WriteLine("Postęp: {0}. Maksymalny postęp: {1}. {2}", v, m, d);
                        return false;
                    });

                if (cennik.Zapisz())
                    Console.WriteLine("Poprawnie zakończono dodawanie niepowiązanego cennika dodatkowego.");
                else
                {
                    cennik.WypiszBledy();
                    return this;
                }

                identyfikatorCennikaDodatkowego = cennik.Dane.Id;
            }

            Cennik dodatkowy = cenniki.Dane.Wszystkie().Where(c => c.Id == identyfikatorCennikaDodatkowego).FirstOrDefault();
            foreach (Podmiot drogeria in podmioty.Dane.Wszystkie().Where(pdm => pdm.Grupy.Any(g => g.Nazwa == "Drogerie")))
            {
                using (IPodmiot podmiot = podmioty.Znajdz(drogeria))
                {
                    podmiot.Dane.CennikDodatkowy = dodatkowy;
                    if (podmiot.Zapisz())
                        Console.WriteLine("Poprawnie zakończono ustawianie cennika dodatkowego dla klienta {0}.", drogeria.NazwaSkrocona);
                    else
                        podmiot.WypiszBledy();
                }
            }
            return this;
        }
	}
}
