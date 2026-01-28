using InsERT.Moria.Sfera;

namespace GeneratorDanych
{
	public static class Uchwyty
	{
		public static Uchwyt UtworzNowy(DaneDoUruchomieniaSfery dane)
		{
			return UtworzNowy(dane, postep: null);
		}

		public static Uchwyt UtworzNowy(DaneDoUruchomieniaSfery dane, IPostepLadowaniaSfery postep)
		{
			var danePolaczenia = ZapewnijDanePolaczenia(dane);
			var mp = new MenedzerPolaczen()
			{
				DostepDoUI = true
			};

			var sfera = mp.Polacz(danePolaczenia, dane.Produkt, postep);
			if (!sfera.ZalogujOperatora(dane.LoginNexo, dane.HasloNexo))
			{
				throw new GeneratorDanychException("Nie udało się zalogować operatora.");
			}

			return sfera;
		}

		private static DanePolaczenia ZapewnijDanePolaczenia(DaneDoUruchomieniaSfery dane)
		{
			if (dane.DanePolaczenia != null)
			{
				return dane.DanePolaczenia;
			}

			if (string.IsNullOrEmpty(dane.LoginSql))
			{
				return DanePolaczenia.Jawne(
					serwer: dane.Serwer,
					baza: dane.Baza,
					autentykacjaWindowsDoSerwera: true);
			}

			return DanePolaczenia.Jawne(
					serwer: dane.Serwer,
					baza: dane.Baza,
					uzytkownikSerwera: dane.LoginSql,
					hasloUzytkownikaSerwera: dane.HasloSql);
		}
	}
}