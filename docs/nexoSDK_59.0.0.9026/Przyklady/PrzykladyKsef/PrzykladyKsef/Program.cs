using InsERT.Moria.Sfera;
using PrzykladyKsef.Operacje;
using System;
using System.Linq;

namespace PrzykladyKsef
{
    public class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Sferyczna aplikacja konsolowa");
                using (var sfera = Uchwyty.UtworzNowy(args, "Szef", "robocze", new PostepLadowaniaSfery()))
                {
                    Console.WriteLine("Sfera została uruchomiona!");
                    sfera.Kontekst().UstawMagazyn(sfera.Magazyny().Dane.Wszystkie().FirstOrDefault());
                    sfera.Kontekst().UstawOddzial(sfera.Centrale().Dane.Wszystkie().FirstOrDefault());
                    sfera.Kontekst().UstawStanowiskoKasowe(sfera.StanowiskaKasowe().Dane.Wszystkie().FirstOrDefault());
                    new WysylkaPoZapisieDokumentu(sfera).Wykonaj();
                    new WysylkaZbiorcza(sfera).Wykonaj();
                    new GenerowanieWrazZWysylka(sfera).Wykonaj();
                    new PobieranieUPO(sfera).Wykonaj();
                    new OdbiorNowychDokumentow(sfera).Wykonaj();
                    new OdbiorDokumentowZOkresu(sfera).Wykonaj();
                    new OdbiorPojedynczegoDokumentu(sfera).Wykonaj();
                    new ImportOdebranejEFaktury(sfera).Wykonaj();
                    new ImportEFakturyDoPrzyjecMagazynowych(sfera).Wykonaj();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(Helpers.GetExceptionMessage(ex));
            }

            Console.WriteLine("Aby zakończyć, wciśnij Enter");
            _ = Console.ReadLine();
        }
    }
}
