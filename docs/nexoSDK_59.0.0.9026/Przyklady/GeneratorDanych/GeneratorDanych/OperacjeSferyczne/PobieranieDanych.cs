using InsERT.Moria.Klienci;
using InsERT.Moria.Sfera;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GeneratorDanych.OperacjeSferyczne
{
	internal static class PobieranieDanych
	{
		internal static string PobierzMagazyn(Uchwyt sfera)
		{
			var pierwszyZBrzegu = sfera.Magazyny().Dane.Pierwszy(x => true);
			if (pierwszyZBrzegu == null)
			{
				throw new GeneratorDanychException("Brak magazynów w bazie.");
			}

			return pierwszyZBrzegu.Symbol;
		}

		internal static List<string> PobierzNipyKlientow(Uchwyt sfera)
		{
			return sfera.Podmioty().Dane.Wszystkie(x => x.Typ == (short)TypPodmiotu.Firmy).Select(x => x.NIP).ToList();
		}

		internal static List<string> PobierzOsobyWystawiajace(Uchwyt sfera)
		{
			return sfera.Podmioty().Dane.Wszystkie(x => x.Osoba != null).Select(x => x.NazwaSkrocona).ToList();
		}

		internal static List<string> PobierzSymboleTowarow(Uchwyt sfera)
		{
			return sfera.Asortymenty().Dane.Wszystkie().Select(x => x.Symbol).ToList();
		}

		internal static async Task<List<string>> PobierzUzytkownikow(DanePolaczenia danePolaczenia)
		{
			return await Task.Run(() =>
			{
				var wynik = new List<string>();
				using (var connection = danePolaczenia.PodajPolaczenie())
				{
					if (connection.State != System.Data.ConnectionState.Open)
					{
						connection.Open();
					}

					using (var command = connection.CreateCommand())
					{
						command.CommandText = "SELECT Login FROM ModelDanychContainer.Uzytkownicy WHERE Ukryty=0 AND NOT Osoba_Id IS NULL;";

						using (var reader = command.ExecuteReader())
						{
							while (reader.Read())
							{
								wynik.Add(reader.GetString(0));
							}
						}
					}
				}
				return wynik;
			});
		}
	}
}