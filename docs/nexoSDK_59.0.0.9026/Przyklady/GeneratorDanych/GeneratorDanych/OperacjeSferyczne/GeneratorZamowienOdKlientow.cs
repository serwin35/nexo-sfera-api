using InsERT.Moria.Asortymenty;
using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.ModelOrganizacyjny;
using InsERT.Moria.Sfera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GeneratorDanych.OperacjeSferyczne
{
	internal class GeneratorZamowienOdKlientow
	{
		private readonly PomiarCzasuOperacji _pomiarCzasu = new PomiarCzasuOperacji();
		private readonly Random _random = new Random();
		private readonly Uchwyt _sfera;
		private string _magazyn;
		private List<string> _nipyKlientow;
		private List<string> _osobyWystawiajace;
		private List<string> _symboleTowarow;

		internal GeneratorZamowienOdKlientow(Uchwyt sfera)
		{
			_sfera = sfera;
		}

		internal async Task DodajZamowienia(int ileZamowien, ILogOperacji logOperacji)
		{
			await Task.Run(() =>
			{				
				string magazyn = PobierzMagazyn();
				var symboleTowarow = PobierzSymboleTowarow();
				var nipyKlientow = PobierzNipyKlientow();
				var osobyWystawiajace = PobierzOsobyWystawiajace();

				var asortymenty = _sfera.Asortymenty();
				var podmioty = _sfera.Podmioty();
				var konfiguracjaZk = _sfera.Konfiguracje().DaneDomyslne.ZamowienieOdKlienta;
				var magazyny = _sfera.Magazyny();

				for (int i = 0; i < ileZamowien; i++)
				{
					try
					{
						string symbolTowaru = Losowanie.LosujZTablicy(symboleTowarow, _random);
						string nipKlienta = Losowanie.LosujZTablicy(nipyKlientow, _random);
						string osobaWystawiajaca = Losowanie.LosujZTablicy(osobyWystawiajace, _random);

						_pomiarCzasu.Start();
						DodajZamowienieOdKlienta(asortymenty, podmioty, magazyny, konfiguracjaZk, magazyn, symbolTowaru, nipKlienta, osobaWystawiajaca, logOperacji);
						var czas = _pomiarCzasu.Stop();
						logOperacji?.Zaloguj($"Dodano zamówienie w czasie {czas}.");
					}
					catch (Exception e)
					{
						logOperacji?.Zaloguj($"Wystąpił błąd {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
					}
				}
			});
		}

		internal (int IleOperacji, double SredniCzasOperacji, int IleOperacjiNaMinute) PobierzStatystyki()
		{
			return _pomiarCzasu.PobierzStatystyki();
		}

		private void DodajZamowienieOdKlienta(IAsortymenty asortymenty, IPodmioty podmioty, IMagazyny magazyny, Konfiguracja konfiguracjaZk, string symbolMagazynu, string symbolTowaru, string nipKlienta, string wystawil, ILogOperacji logOperacji)
		{
			var magazyn = magazyny.Dane.Pierwszy(m => m.Symbol == symbolMagazynu);

			if (magazyn == null)
			{
				logOperacji?.Zaloguj($"Nie znaleziono magazynu o symbolu {symbolMagazynu}");
			}

			var zamowienia = _sfera.ZamowieniaOdKlientow();

			using (var zk = zamowienia.Utworz(konfiguracjaZk))
			{
				zk.Dane.Magazyn = magazyn;

				var klient = podmioty.Dane.Pierwszy(p => p.NIP == nipKlienta);
				if (klient == null)
				{
					logOperacji?.Zaloguj($"Nie znaleziono klienta o NIP {nipKlienta}");
				}

				zk.Dane.Podmiot = klient;

				var asortyment = asortymenty.Dane.Wszystkie(t => t.Symbol == symbolTowaru).First();
				_ = zk.Pozycje.Dodaj(asortyment, 1m, asortyment.JednostkaSprzedazy);

				zk.Dane.WystawilaOsoba = podmioty.Dane.Pierwszy(p => p.Osoba != null && p.NazwaSkrocona == wystawil).Osoba;
				zk.Dane.Uwagi = "Zamówienie z generatora";

				if (zk.Zapisz())
				{
					logOperacji?.Zaloguj($"Zapisano {zk.Dane.NumerWewnetrzny.PelnaSygnatura}");
				}
				else
				{
					logOperacji?.Zaloguj("Błędy podczas zapisu:");
					logOperacji?.Zaloguj(zk.PobierzBledy());
				}
			}
		}

		private string PobierzMagazyn()
		{
			if (_magazyn == null)
			{
				_magazyn = PobieranieDanych.PobierzMagazyn(_sfera);
			}
			return _magazyn;
		}

		private List<string> PobierzNipyKlientow()
		{
			if (_nipyKlientow == null)
			{
				_nipyKlientow = PobieranieDanych.PobierzNipyKlientow(_sfera);
			}
			return _nipyKlientow;
		}

		private List<string> PobierzOsobyWystawiajace()
		{
			if (_osobyWystawiajace == null)
			{
				_osobyWystawiajace = PobieranieDanych.PobierzOsobyWystawiajace(_sfera);
			}
			return _osobyWystawiajace;
		}

		private List<string> PobierzSymboleTowarow()
		{
			if (_symboleTowarow == null)
			{
				_symboleTowarow = PobieranieDanych.PobierzSymboleTowarow(_sfera);
			}
			return _symboleTowarow;
		}
	}
}