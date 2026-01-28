/// Program do prawidłowego działania wymaga raportu własnego, dla którego zdefiniowano wzorzec wydruku.
/// 
/// W katalogu tego projektu znajduje się xml z raportem własnym (Lista towarów - raport SQL.xml), 
/// który należy zaimportować do Subiekta przed uruchomieniem przykładu.
/// W tym samym katalogu znajduje się także wzorzec wydruku (Lista towarów - raport SQL- domyślny wzorzec.mrt), 
/// który trzeba zaimportować do tego raportu.
/// Dopiero wtedy przykład będzie gotowy do uruchomienia.
/// 
/// Przykładowy raport ma parametr, który jest używany w <<see cref="EksporterRaportu.UruchamiaczRaportu.WyliczRaportEksportujDoPdf"/>>.
/// Można użyć innego raportu. Raport nie musi mieć parametrów.
/// Należy jednak zadbać, żeby dla raportu był zdefiniowany wzorzec wydruku, który zostanie użyty podczas eksportu zawartości raportu do pliku pdf.
/// W metodzie <see cref="EksporterRaportu.Program.Main"/> inicjowana jest zmienna raport na podstawie nazwy raportu z załącznika.
/// 
/// Jeżeli chcemy użyć innego raportu, należy zmodyfikować program tak, żeby zmienna raport zawierała dane raportu, który chcemy wyeksportować.
/// 

using InsERT.Moria.Raporty;
using InsERT.Moria.Sfera;
using InsERT.Mox.Product;
using System;
using System.IO;
using System.Linq;


namespace EksporterRaportu
{
	public static class Program
	{
		private static void Main()
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

					Console.WriteLine("Sfera została uruchomiona!");

					// szukanie raportu testowego (użyto nazwy z załączonego do projektu raportu).
					var raporty = sfera.PodajObiektTypu<IRaporty>();
					var raport = raporty.Dane.Wszystkie().Where(r => r.Nazwa.Contains("Lista towarów")).FirstOrDefault();

					var obiektRaport = raporty.Znajdz(raport);

					if (obiektRaport == null)
					{
						Console.WriteLine("Nie znaleziono raportu własnego.");
					}
					else
					{
						string docelowyPlik = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "InsERT", "ListaTowarow");
						UruchamiaczRaportu.DlaRaportu(obiektRaport, sfera.Wydruki())
							.ZapisywanieDoPlikuPdf(docelowyPlik)
							.WyliczRaportEksportujDoPdf();
					}
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
				Serwer = "(local)\\INSERTNEXO",
				Baza = "Nexo_demo_1",
				Produkt = ProductId.Subiekt,
				LoginNexo = "Szef",
				HasloNexo = "robocze"
			};
			return dane;
		}
	}
}
