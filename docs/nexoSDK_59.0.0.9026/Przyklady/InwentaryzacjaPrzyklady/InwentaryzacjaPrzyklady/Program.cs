using InsERT.Moria.Sfera;
using InsERT.Mox.Product;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InwentaryzacjaPrzyklady
{
    class Program
    {
        static void Main(string[] args)
        {
			try
			{
				Console.WriteLine("Sferyczna aplikacja konsolowa");

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

				using (var sfera = Uchwyty.UtworzNowy(daneDoUruchomienia, new PostepLadowaniaSfery()))
				{
					_ = new ObslugaInwentaryzacji(sfera)
						.TworzenieSpisuInwentaryzacyjnegoDlaWybranychTowarow()
						.TworzenieSpisuDlaWybranejGrupyAsortymentu()
						.TworzenieIWykonanieInwentaryzacjiRecznie()
						.WycofanieInwentaryzacji()
						.DodawanieInwentaryzacjiNaPodstawieSpisu()
						;
				}

			}
			catch (Exception ex)
			{
				Console.WriteLine(Helpers.GetExceptionMessage(ex));
			}

			Console.WriteLine("Aby zakończyć, wciśnij Enter");
			_ = Console.ReadLine();
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
	}
}
