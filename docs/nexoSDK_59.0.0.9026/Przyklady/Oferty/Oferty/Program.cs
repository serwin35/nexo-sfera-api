using InsERT.Moria.Sfera;
using InsERT.Mox.Product;
using System;
using System.Linq;

namespace Oferty
{
    public class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Sferyczna aplikacja konsolowa");
            var daneDoUruchomienia = PobierzDaneDoUruchomieniaSfery();

            Console.WriteLine("Trwa uruchamianie Sfery w wersji {0}{1}...", DanePolaczenia.WersjaSfery,
                !string.IsNullOrEmpty(daneDoUruchomienia.Baza) ? $" na bazie {daneDoUruchomienia.Baza}" : string.Empty);

            using (var sfera = Uchwyty.UtworzNowy(daneDoUruchomienia, new PostepLadowaniaSfery()))
            {
                Console.WriteLine("Sfera została uruchomiona!");
                new TworzenieOfertyZJednymWariantem(sfera).Utworz();
                new TworzenieOfertyZDwomaWariantami(sfera).Utworz();
            }

            Console.WriteLine("Naciśnij dowolny klawisz, aby zakończyć...");
            Console.ReadKey();
        }

        private static DaneDoUruchomieniaSfery PobierzDaneDoUruchomieniaSfery()
        {
            // pobieranie danych z InsLauncher:
            var danePolaczenia = OdbierzDanePolaczeniaZInsLauncher();
            if (danePolaczenia != null)
            {
                return PodajDaneDoUruchomienia(danePolaczenia);
            }

            // domyślne dane zaszyte w programie:
            return PodajDomyslneDaneDoUruchomienia();
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
                Produkt = ProductId.Gestor,
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
                Produkt = ProductId.Gestor,
                LoginNexo = "Szef",
                HasloNexo = "robocze"
            };
            return dane;
        }
    }
}