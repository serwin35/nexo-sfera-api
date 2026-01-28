using InsERT.Moria.Sfera;
using InsERT.Mox.Product;
using System;
using System.Linq;

namespace PrzykladyKsef
{
    public static class Uchwyty
    {
        public static Uchwyt UtworzNowy(string[] args, string loginNexo, string hasloNexo, IPostepLadowaniaSfery postep)
        {
			DanePolaczenia danePolaczenia;

			if ( args != null && args.Contains(@"/UruchomionePrzezInsLauncher"))
			{
				danePolaczenia = DanePolaczenia.Odbierz();
			}
			else
			{
				danePolaczenia = DanePolaczenia.Jawne(@"(local)\INSERTNEXO", "Nexo_Demo_1", true);
			}

			Console.WriteLine($"Trwa uruchamianie Sfery w wersji {DanePolaczenia.WersjaSfery}...");
            var mp = new MenedzerPolaczen();
            var sfera = mp.Polacz(danePolaczenia, ProductId.Subiekt, postep);
            _ = sfera.ZalogujOperatora(loginNexo, hasloNexo);

            return sfera;
        }
    }
}
