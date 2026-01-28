using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GeneratorDanych.OperacjeSferyczne
{
	internal class GeneratorTowarow
	{
		private readonly GeneratorNazwTowarow _generatorNazwTowarow;
		private readonly PomiarCzasuOperacji _pomiarCzasu = new PomiarCzasuOperacji();
		private readonly Uchwyt _sfera;

		internal GeneratorTowarow(Uchwyt sfera)
		{
			_sfera = sfera;
			_generatorNazwTowarow = new GeneratorNazwTowarow();
		}

		internal async Task DodajTowary(int ileTowarow, ILogOperacji logOperacji)
		{
			await Task.Run(() =>
			{
				var towary = _sfera.Asortymenty();
				var szablony = _sfera.SzablonyAsortymentu();
				var szablon = szablony.Dane.Wszystkie().First();

				for (int i = 0; i < ileTowarow; i++)
				{
					try
					{
						_pomiarCzasu.Start();
						DodajTowar(towary, szablon, logOperacji);
						var czas = _pomiarCzasu.Stop();
						logOperacji?.Zaloguj($"Dodano towar w czasie {czas}.");
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

		private void DodajTowar(IAsortymenty managerTowarow, SzablonAsortymentu szablon, ILogOperacji logOperacji)
		{
			bool dodane = false;

			string nazwa = _generatorNazwTowarow.GenerujNazweTowaru();
			string symbol = GeneratorNazwTowarow.GenerujSymbolNaPodstawieNazwy(nazwa);

			int counter = 0;

			using (var newBusinessObject = managerTowarow.Utworz())
			{
				newBusinessObject.WypelnijNaPodstawieSzablonu(szablon);

				while (!dodane)
				{
					newBusinessObject.Dane.Nazwa = nazwa;
					newBusinessObject.Dane.Symbol = symbol;

					bool succeeded = newBusinessObject.Zapisz();
					if (succeeded)
					{
						dodane = true;
						logOperacji?.Zaloguj($"Zapisano towar {symbol} {nazwa}");
					}
					else
					{
						nazwa = $"{nazwa} {counter++}";
						symbol = GeneratorNazwTowarow.GenerujSymbolNaPodstawieNazwy(nazwa);
					}
				}
			}
		}
	}
}