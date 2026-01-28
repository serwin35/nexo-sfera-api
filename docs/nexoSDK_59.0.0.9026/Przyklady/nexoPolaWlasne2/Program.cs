using System;
using System.Linq;
using InsERT.Mox.Product;
using InsERT.Moria;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Asortymenty;
using InsERT.Moria.Sfera;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Narzedzia.PolaWlasne2;

namespace RozwiazanieWlasne
{
	class Program
    {
        private static void DostepDoMetadanychPolWlasnychV2_Test(Uchwyt uchwyt)
        {
            /* Jesli chodzi o dostep do metadanych prostych pol wlasnych, to nadal mozna stosowac dotychczasowy sposob, 
             * ale dla ułatwienia i spójności powstał dodatkowy sposob, ktory zalecamy używać w nowych rozwiazaniach wlasnych opartych o pola v2
             */
            Console.WriteLine("     Dostęp do metadanych prostych pól własnych...");
            var prostePolaWlasne = uchwyt.PodajObiektTypu<IProstePolaWlasne>();
            var prostePolaWlasneTester = new ProstePolaWlasneTester(prostePolaWlasne);
            prostePolaWlasneTester.Test();

            Console.WriteLine("     Dostep do metadanych zaawansowanych pol wlasnych...");
            var zaawansowanePolaWlasne = uchwyt.PodajObiektTypu<IZaawansowanePolaWlasne>();
            var zaawansowanePolaWlasneTester = new ZaawansowanePolaWlasneTester(zaawansowanePolaWlasne);
            zaawansowanePolaWlasneTester.Test();

            Console.WriteLine("     Dostęp do zdefiniowanych słowników własnych...");
            var slownikiWlasne = uchwyt.PodajObiektTypu<ISlownikiWlasne>();
            var slownikiWlasneTester = new SlownikiWlasneTester(slownikiWlasne);
            slownikiWlasneTester.Test();
        }

        private static void OdczytIZapisWartosciPolWlasnychV2_NowySposob_Test(Uchwyt uchwyt)
        {
            string symbolAso = "testpolv2_NowySposob";
            Console.WriteLine("     Sprawdzanie istnienia asortymentu o symbolu={0}", symbolAso);
            IAsortymenty asortymenty = uchwyt.PodajObiektTypu<IAsortymenty>();
            IAsortyment aso = asortymenty.Znajdz(symbolAso);
            if (aso == null)
            {
                Console.WriteLine("     Tworzenie asortymentu o symbolu={0}", symbolAso);
                //throw new InvalidOperationException("Nie znaleziono asorytmentu");
                var szablonyAso = uchwyt.PodajObiektTypu<ISzablonyAsortymentu>();
                var szablonTowaru = szablonyAso.DaneDomyslne.Towar;

                aso = asortymenty.Utworz();
                //aso.AutoSymbol();
                aso.Dane.Symbol = symbolAso;
                aso.Dane.Nazwa = "Test pól v2";
                aso.WypelnijNaPodstawieSzablonu(szablonTowaru);

                bool moznaZapisac = aso.MoznaZapisac;
            }
            else
            {
                Console.WriteLine("     Znaleziono asortyment o symbolu={0}", symbolAso);
            }

            var asoPW2Accessor = uchwyt.UtworzPolaWlasneAdv2Accessor(aso.Dane);

            var tester = new OdczytIZapisWartosciPolWlasnychNowySposob(asoPW2Accessor);

            Console.WriteLine("     Podpięcie do zdarzenia PropertyChanged");
            tester.PodpiecieDoZdarzeniaPropertyChanged();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Tekst'");
            tester.OdczytIZapisWartosciPolaTypuTekst();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Długi tekst'");
            tester.OdczytIZapisWartosciPolaTypuDlugiTekst();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Liczba całkowita'");
            tester.OdczytIZapisWartosciPolaTypuLiczbaCalkowita();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Liczba rzeczywista'");
            tester.OdczytIZapisWartosciPolaTypuLiczbaRzeczywista();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Wartość logiczna'");
            tester.OdczytIZapisWartosciPolaTypuWartoscLogiczna();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Data'");
            tester.OdczytIZapisWartosciPolaTypuData();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Słownik własny'");
            tester.OdczytIZapisWartosciPolaTypuSlownikWlasny();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Słownik własny SQL' z kluczem Int");
            tester.OdczytIZapisWartosciPolaTypuSlownikWlasnySqlByInt();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Słownik własny SQL' z kluczem Guid");
            tester.OdczytIZapisWartosciPolaTypuSlownikWlasnySqlByGuid();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Słownik systemowy' walut");
            tester.OdczytIZapisWartosciPolaTypuSlownikSystemowyWalut();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Słownik systemowy' magazynów");
            tester.OdczytIZapisWartosciPolaTypuSlownikSystemowyMagazynow();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Słownik systemowy' rachunków bankowych");
            tester.OdczytIZapisWartosciPolaTypuSlownikSystemowyRachunkowBankowych();

            Console.WriteLine("     Odpięcie od zdarzenia PropertyChanged");
            tester.OdpiecieOdZdarzeniaPropertyChanged();

            System.Diagnostics.Trace.Assert(aso.Zapisz());
        }

        private static void OdczytIZapisWartosciPolWlasnychV2_StarySposob_Test(Uchwyt uchwyt)
        {
            string symbolAso = "testpolv2_StarySposob";
            Console.WriteLine("     Sprawdzanie istnienia asortymentu o symbolu={0}", symbolAso);
            IAsortymenty asortymenty = uchwyt.PodajObiektTypu<IAsortymenty>();
            IAsortyment aso = asortymenty.Znajdz(symbolAso);
            if (aso == null)
            {
                Console.WriteLine("     Tworzenie asortymentu o symbolu={0}", symbolAso);
                //throw new InvalidOperationException("Nie znaleziono asorytmentu");
                var szablonyAso = uchwyt.PodajObiektTypu<ISzablonyAsortymentu>();
                var szablonTowaru = szablonyAso.DaneDomyslne.Towar;

                aso = asortymenty.Utworz();
                //aso.AutoSymbol();
                aso.Dane.Symbol = symbolAso;
                aso.Dane.Nazwa = "Test pól v2";
                aso.WypelnijNaPodstawieSzablonu(szablonTowaru);

                bool moznaZapisac = aso.MoznaZapisac;
            }
            else
            {
                Console.WriteLine("     Znaleziono asortyment o symbolu={0}", symbolAso);
            }

            if (aso.Dane.PolaWlasneAdv2 == null)
            {
                aso.Dane.PolaWlasneAdv2 = new PolaWlasneAsortyment_Adv2();
            }

            var zaawansowanePolaWlasne = uchwyt.PodajObiektTypu<IZaawansowanePolaWlasne>();

            var tester = new OdczytIZapisWartosciPolWlasnychStarySposob(aso, zaawansowanePolaWlasne);

            Console.WriteLine("     Podpięcie do zdarzenia PropertyChanged");
            tester.PodpiecieDoZdarzeniaPropertyChanged();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Tekst'");
            tester.OdczytIZapisWartosciPolaTypuTekst();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Długi tekst'");
            tester.OdczytIZapisWartosciPolaTypuDlugiTekst();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Liczba całkowita'");
            tester.OdczytIZapisWartosciPolaTypuLiczbaCalkowita();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Liczba rzeczywista'");
            tester.OdczytIZapisWartosciPolaTypuLiczbaRzeczywista();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Wartość logiczna'");
            tester.OdczytIZapisWartosciPolaTypuWartoscLogiczna();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Data'");
            tester.OdczytIZapisWartosciPolaTypuData();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Słownik własny'");
            tester.OdczytIZapisWartosciPolaTypuSlownikWlasny();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Słownik własny SQL' z kluczem Int");
            tester.OdczytIZapisWartosciPolaTypuSlownikWlasnySqlByInt();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Słownik własny SQL' z kluczem Guid");
            tester.OdczytIZapisWartosciPolaTypuSlownikWlasnySqlByGuid();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Słownik systemowy' walut");
            tester.OdczytIZapisWartosciPolaTypuSlownikSystemowyWalut();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Słownik systemowy' magazynów");
            tester.OdczytIZapisWartosciPolaTypuSlownikSystemowyMagazynow();

            Console.WriteLine("     Odczyt i zapis wartości pola typu 'Słownik systemowy' rachunków bankowych");
            tester.OdczytIZapisWartosciPolaTypuSlownikSystemowyRachunkowBankowych();

            Console.WriteLine("     Odpięcie od zdarzenia PropertyChanged");
            tester.OdpiecieOdZdarzeniaPropertyChanged();

            System.Diagnostics.Trace.Assert(aso.Zapisz());
        }

        private static void UzyciePolWlasnychV2WZapytaniachLINQ_Test(Uchwyt uchwyt)
        {
            const string nazwaPolaTypuTekst = TestowePolaWlasneAsortymentu.PoleTypuTekst;
            const string nazwaPolaTypuDlugiTekst = TestowePolaWlasneAsortymentu.PoleTypuDlugiTekst;
            const string nazwaPolaTypuLiczbaCalkowita = TestowePolaWlasneAsortymentu.PoleTypuLiczbaCalkowita;
            const string nazwaPolaTypuLiczbaRzeczywista = TestowePolaWlasneAsortymentu.PoleTypuLiczbaRzeczywista;
            const string nazwaPolaTypuWartoscLogiczna = TestowePolaWlasneAsortymentu.PoleTypuWartoscLogiczna;
            const string nazwaPolaTypuData = TestowePolaWlasneAsortymentu.PoleTypuData;
            const string nazwaPolaTypuSlownikWlasny = TestowePolaWlasneAsortymentu.PoleTypuSlownikWlasny;
            const string nazwaPolaTypuSlownikWlasnySqlByInt = TestowePolaWlasneAsortymentu.PoleTypuSlownikWlasnySqlByInt;
            const string nazwaPolaTypuSlownikWlasnySqlByGuid = TestowePolaWlasneAsortymentu.PoleTypuSlownikWlasnySqlByGuid;
            const string nazwaPolaTypuSlownikSystemowyWalut = TestowePolaWlasneAsortymentu.PoleTypuSlownikSystemowyWalut;
            const string nazwaPolaTypuSlownikSystemowyMagazynow = TestowePolaWlasneAsortymentu.PoleTypuSlownikSystemowyMagazynow;
            const string nazwaPolaTypuSlownikSystemowyRachunkowBankowych = TestowePolaWlasneAsortymentu.PoleTypuSlownikSystemowyRachunkowBankowych;

            var asortymenty = uchwyt.PodajObiektTypu<IAsortymenty>();

            string szukanaWartoscPola = "abcd";
            Console.Write("     Wyszukiwanie asortymentu z polem własnym o wartości '{0}'...", szukanaWartoscPola);
            var aso = asortymenty.Dane.Wszystkie()
                .Where(a => a.PolaWlasneAdv2.Get<string>(nazwaPolaTypuTekst) == szukanaWartoscPola) //odczytanie wartosci pola i porownanie jej z szukana wartoscia
                .Select(a => new
                {
                    //z asortymenu zwracam jego symbol i wartosc kazdego pola
                    a.Symbol,
                    PoleTypuTekst = a.PolaWlasneAdv2.Get<string>(nazwaPolaTypuTekst),
                    PoleTypuDlugiTekst = a.PolaWlasneAdv2.Get<string>(nazwaPolaTypuDlugiTekst),
                    PoleTypuLiczbaCalkowita = a.PolaWlasneAdv2.Get<int?>(nazwaPolaTypuLiczbaCalkowita),
                    PoleTypuLiczbaRzeczywista = a.PolaWlasneAdv2.Get<decimal?>(nazwaPolaTypuLiczbaRzeczywista),
                    PoleTypuWartoscLogiczna = a.PolaWlasneAdv2.Get<bool?>(nazwaPolaTypuWartoscLogiczna),
                    PoleTypuData = a.PolaWlasneAdv2.Get<DateTime?>(nazwaPolaTypuData),
                    KluczObcyDoSlownikaWlasnego = a.PolaWlasneAdv2.Get<int?>(nazwaPolaTypuSlownikWlasny),
                    KluczObcyDoSlownikaWlasnegoSqlByInt = a.PolaWlasneAdv2.Get<int?>(nazwaPolaTypuSlownikWlasnySqlByInt),
                    KluczObcyDoSlownikaWlasnegoSqlByGuid = a.PolaWlasneAdv2.Get<Guid?>(nazwaPolaTypuSlownikWlasnySqlByGuid),
                    KluczObcyDoSlownikaSystemowegoWalut = a.PolaWlasneAdv2.Get<Guid?>(nazwaPolaTypuSlownikSystemowyWalut),
                    KluczObcyDoSlownikaSystemowegoMagazynow = a.PolaWlasneAdv2.Get<int?>(nazwaPolaTypuSlownikSystemowyMagazynow),
                    KluczObcyDoSlownikaSystemowegoRachunkowBankowych = a.PolaWlasneAdv2.Get<int?>(nazwaPolaTypuSlownikSystemowyRachunkowBankowych),
                })
                .ResolveExtensionProperties() //wymagane, aby odwolania do metod "Get<Typ>" zostaly przetlumaczone na zapytanie LINQ
                .FirstOrDefault();
            System.Diagnostics.Trace.Assert(aso != null);
            Console.WriteLine(" znaleziono asortyment o symbolu={0}", aso.Symbol);
        }


		private static DanePolaczenia OdbierzDanePolaczeniaZInsLauncher()
		{
			var commandLineArguments = Environment.GetCommandLineArgs();
			if (commandLineArguments != null && commandLineArguments.Contains(@"/UruchomionePrzezInsLauncher"))
			{
				//pobieramy parametry podane przez Launcher
				return DanePolaczenia.Odbierz();
			}

			return null;
		}

		private static DaneDoUruchomieniaSfery PodajDaneDoUruchomienia(DanePolaczenia danePolaczenia)
		{
			var dane = new DaneDoUruchomieniaSfery()
			{
				DanePolaczenia = danePolaczenia,
				Produkt = ProductId.Subiekt,
				LoginNexo = "Szef",
				HasloNexo = "robocze"
			};
			return dane;
		}

		private static DaneDoUruchomieniaSfery PodajDomyslneDaneDoUruchomienia()
		{
			var dane = new DaneDoUruchomieniaSfery()
			{
				Serwer = "(local)",
				Baza = "Nexo_demo_1",
				Produkt = ProductId.Subiekt,
				LoginNexo = "Szef",
				HasloNexo = "robocze"
			};
			return dane;
		}


		static void Main()
        {
            try
            {
				//TODO: Należy zmienić dane połączenia i dane operatora na poprawne
				DaneDoUruchomieniaSfery daneDoUruchomienia;


				// pobieranie danych z InsLauncher:
				var danePolaczenia = OdbierzDanePolaczeniaZInsLauncher();
				if (danePolaczenia != null)
				{
					daneDoUruchomienia = PodajDaneDoUruchomienia(danePolaczenia);
				}
				else
				{
					// domyślne dane zaszyte w programie:
					daneDoUruchomienia = PodajDomyslneDaneDoUruchomienia();
				}

				Console.WriteLine("Trwa uruchamianie Sfery w wersji {0}{1}...", DanePolaczenia.WersjaSfery,
					!string.IsNullOrEmpty(daneDoUruchomienia.Baza) ? $" na bazie {daneDoUruchomienia.Baza}" : string.Empty);
				
                using (Uchwyt uchwyt = Uchwyty.UtworzNowy(daneDoUruchomienia, new PostepLadowaniaSfery()))
                {
					Console.WriteLine("Sfera została uruchomiona!");

					//jak sprawdzić, czy aktualnie działają pola własne w wersji 1 czy 2
					if (uchwyt.PodajObiektTypu<IWersjaPolWlasnych>().NumerWersji == NumerWersjiPolWlasnych.Wersja1)
                    {
                        Console.WriteLine("Podmiot nie znajduje się w trybie pól własnych v2.");
                        Console.WriteLine("Nie wykonano żadnych testów");
                    }
                    else
                    {
                        TestowePolaWlasneAsortymentu.UpewnijSieCzyZdefiniowanoZaawansowanePolaWlasneAsortymentu(uchwyt);

                        Console.WriteLine("Test dostępu do metadanych pól własnych v2...");
                        DostepDoMetadanychPolWlasnychV2_Test(uchwyt);

                        Console.WriteLine("Test odczytu i zapisu wartości pól własnych v2 - nowy (zalecany) sposób...");
                        OdczytIZapisWartosciPolWlasnychV2_NowySposob_Test(uchwyt);

                        Console.WriteLine("Test odczytu i zapisu wartości pól własnych v2 - stary (niezalecany) sposób...");
                        OdczytIZapisWartosciPolWlasnychV2_StarySposob_Test(uchwyt);

                        Console.WriteLine("Test użycia pól własnych v2 w zapytaniach LINQ...");
                        UzyciePolWlasnychV2WZapytaniachLINQ_Test(uchwyt);

                        Console.WriteLine("Wszystkie testy zakończyły się powodzeniem.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd podczas wykonywania testów:");
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine("Naciśnij dowolny przycisk aby zakończyć program.");
            Console.ReadLine();
        }
    }
}
