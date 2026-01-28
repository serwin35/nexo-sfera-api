using System;
using System.IO;
using System.Linq;
using System.Collections;

using InsERT.Moria.ModelDanych;
using InsERT.Moria.Wydruki.Enums;
using InsERT.Moria.Wydruki;
using InsERT.Moria.Raporty;
using InsERT.Moria.Sfera;

namespace EksporterRaportu
{
	internal class UruchamiaczRaportu
	{
		internal static UruchamiaczRaportu DlaRaportu(IRaport obiektBiznesowyRaportu, IWydruki wydruki)
		{
			return new UruchamiaczRaportu(wydruki, obiektBiznesowyRaportu);
		}

		private readonly IWydruki _wydruki;
		private readonly IRaport _obiektBiznesowyRaportu;
		private string _pdfWynikowyFolder;
		private string _pdfNazwaPliku;
		private string _preferowanyWzorzecWydruku;

		/// <summary>
		/// Wyświetla raport i eksportuje do pliku pdf, wykorzystując wzorzec wydruku, zdefiniowany przez w programie.
		/// </summary>
		/// <param name="wydruki">Komponent do drukowania (pobierany ze sfery)</param>
		/// <param name="obiektBiznesowyRaportu">raport <see cref="IRaport"/>, który chcemy wyeksportować</param>
		private UruchamiaczRaportu(IWydruki wydruki, IRaport obiektBiznesowyRaportu)
		{
			_wydruki = wydruki;
			_obiektBiznesowyRaportu = obiektBiznesowyRaportu;
		}

		/// <summary>
		/// Jeżeli chcemy wskazać wzorzec inny niż domyślny, to tutaj należy wskazać jego nazwę
		/// </summary>
		/// <param name="nazwaWzorcaWydruku"></param>
		/// <returns></returns>
		internal UruchamiaczRaportu WzorzecWydruku(string nazwaWzorcaWydruku)
		{
			_preferowanyWzorzecWydruku = nazwaWzorcaWydruku;

			return this;
		}

		/// <summary>
		/// Jeżeli chcemy wskazać nazwę wynikowego pliku pdf
		/// </summary>
		/// <param name="nazwaPliku"></param>
		/// <returns></returns>
		internal UruchamiaczRaportu ZapisywanieDoPlikuPdf(string nazwaPliku)
		{
			string tylkoNazwaPliku = Path.GetFileName(nazwaPliku);
			if (tylkoNazwaPliku == nazwaPliku)
			{
				_pdfWynikowyFolder = null;
			}
			else
			{
				_pdfNazwaPliku = tylkoNazwaPliku;
				_pdfWynikowyFolder = nazwaPliku.Substring(0, nazwaPliku.Length - tylkoNazwaPliku.Length);

				if (!Directory.Exists(_pdfWynikowyFolder))
				{ 
					Directory.CreateDirectory(_pdfWynikowyFolder);
				}
			}

			return this;
		}


		/// <summary>
		/// Wylicza raport i eksportuje do pdf'a.
		/// Żeby operacja przebiegła pomyślnie należy samodzielnie zdefiniować wzorzec wydruku dla raportu.
		/// </summary>
		internal void WyliczRaportEksportujDoPdf()
		{
			Console.WriteLine($"Pobieranie danych z raportu:  {_obiektBiznesowyRaportu.Dane.Nazwa}.");

			var przygotowanyDoWykonania = _obiektBiznesowyRaportu.RaportWlasny.Przygotuj();


			if (przygotowanyDoWykonania != null)
			{
				// Jeżeli raport zawiera parametry, to tutaj można modyfikować ich wartości.
				Console.WriteLine($"Parametry przed zmianami {przygotowanyDoWykonania.PodajOpisParametrow()}");
				_ = przygotowanyDoWykonania.UstawWartoscParametru("Maksymalna liczba wierszy", 5);
				Console.WriteLine($"Po zmianach wartości  {przygotowanyDoWykonania.PodajOpisParametrow()}");

				// Wyliczenie raportu.
				var wyniki = przygotowanyDoWykonania.Wylicz();
				if (wyniki.Any())
				{
					Console.WriteLine($"Znaleziono wiersze z danymi ({wyniki.Count()} wierszy).");


					using (var wydruk = _wydruki.Utworz(TypWzorcaWydruku.Raport))
					{
						// Raport własny wymaga ustawienia obiektu do wydruku w poniższy sposób.
						wydruk.ObiektDoWydruku = przygotowanyDoWykonania.PrzygotujObiektDoWydruku(_obiektBiznesowyRaportu.Dane, wyniki.ToList());

						WzorzecWydruku wzorzecWydruku = null;
						if (!string.IsNullOrEmpty(_preferowanyWzorzecWydruku))
						{
							wzorzecWydruku = wydruk.ParametryDrukowania.DostepneWzorce.FirstOrDefault(w => w.Nazwa.Contains(_preferowanyWzorzecWydruku));
						}

						if (wzorzecWydruku == null)
						{
							wzorzecWydruku = wydruk.ParametryDrukowania.DostepneWzorce.FirstOrDefault();
						}

						if (wzorzecWydruku == null)
						{
							Console.WriteLine($"Nie znaleziono wzorca wydruku dla raportu {_obiektBiznesowyRaportu.Dane.Nazwa}. Nie można wyeksportować wyniku raportu bez wzorca.");
						}
						else
						{
							Console.WriteLine($"Użyto wzorca wydruku: {wzorzecWydruku.Nazwa}.");


							wydruk.ParametryDrukowania.WybranyWzorzec = wzorzecWydruku;

							string nazwaPlikuWynikowego = null;
							if (string.IsNullOrEmpty(_pdfNazwaPliku))
							{
								nazwaPlikuWynikowego = $"wydrukRaportu-wg-wzorca-{wzorzecWydruku.Nazwa}.pdf";
							}
							else
							{
								nazwaPlikuWynikowego = _pdfNazwaPliku;
							}
														
							wydruk.ParametryDrukowania.NazwaDokumentuUzytkownika = nazwaPlikuWynikowego;
							if (!string.IsNullOrEmpty(_pdfWynikowyFolder))
							{
								wydruk.ParametryDrukowania.SciezkaEksportu = _pdfWynikowyFolder;
							}

							wydruk.ParametryDrukowania.FormatEksportu = "pdf";
							wydruk.Eksport();

							Console.WriteLine($"Plik wynikowy: {nazwaPlikuWynikowego}.");
							Console.WriteLine($"W folderze: {wydruk.ParametryDrukowania.SciezkaEksportu}");
						}
					}
				}
				else
				{
					Console.WriteLine($"Nie znaleziono żadnego wiersza z danymi (zero wierszy).");
				}
			}
			else
			{
				Console.WriteLine("Nie udało się przygotować raportu do wykonania");
			}
		}
	}
}
