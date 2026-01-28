using InsERT.Moria.Sfera;
using InsERT.Mox.Product;
using RealizacjeDokumentow.Przyklady;
using System;
using System.Linq;

namespace RealizacjeDokumentow
{
    public class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Sferyczna aplikacja konsolowa - przykłady realizacji dokumentów.");

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
                    sfera.Kontekst().UstawMagazynWedlugSymbolu("MAG");
                    sfera.Kontekst().UstawOddzialWedlugSymbolu("CENTRALA");
                    sfera.Kontekst().UstawStanowiskoKasoweWdlugSymbolu("CENTR");

                    RealizacjaZamowieniaOdKlientaDoDokumentuSprzedazy realizacjaZkDoDs = new RealizacjaZamowieniaOdKlientaDoDokumentuSprzedazy(sfera);
                    realizacjaZkDoDs.ZrealizujJednoZamowienieDoFaktury_BezKonsolidacjiPozycji();
                    realizacjaZkDoDs.ZrealizujJednoZamowienieDoParagonu_BezKonsolidacjiPozycji();
                    realizacjaZkDoDs.ZrealizujJednoZamowienieDoFaktury_ZKonsolidacjaPozycji();
                    realizacjaZkDoDs.ZrealizujWieleZamowienDoFaktury_BezKonsolidacjiPozycji();
                    realizacjaZkDoDs.ZrealizujWieleZamowienDoFaktury_ZKonsolidacjaPozycji();
                    realizacjaZkDoDs.ZrealizujWieleZamowienDoParagonuImiennego_ZKonsolidacjaPozycji();
                    realizacjaZkDoDs.ZrealizujCzesciowoWieleZamowienDoFaktury_BezKonsolidacjiPozycji();
                    realizacjaZkDoDs.ZrealizujCzesciowoWieleZamowienDoFaktury_ZKonsolidacjaPozycji();
                    realizacjaZkDoDs.ZrealizujWybranePozycjeZamowien();
                    realizacjaZkDoDs.RealizacjaZamowieniaZPlatnosciaNatychmiastowaDoFakturySprzedazy();
                    realizacjaZkDoDs.RealizacjaZamowieniaZPrzedplataDoFakturySprzedazy();
                    realizacjaZkDoDs.ZrealizujZamowieniaZPrzenoszeniemPlatnosciIKonsolidacja();
                    realizacjaZkDoDs.ZrealizujZamowieniaZPlatnosciamiDoFakturyZaliczkowej();
                    realizacjaZkDoDs.ZrealizujZamowienieBezPlatnosciDoFakturyZaliczkowej();

                    RealizacjaZamowieniaDoDostawcyDoDokumentuZakupu realizacjaZdDoDz = new RealizacjaZamowieniaDoDostawcyDoDokumentuZakupu(sfera);
                    realizacjaZdDoDz.ZrealizujJednoZamowienieDoFaktury_BezKonsolidacjiPozycji();
                    realizacjaZdDoDz.ZrealizujJednoZamowienieDoFaktury_ZKonsolidacjaPozycji();
                    realizacjaZdDoDz.ZrealizujWieleZamowienDoFaktury_BezKonsolidacjiPozycji();
                    realizacjaZdDoDz.ZrealizujWieleZamowienDoFaktury_ZKonsolidacjaPozycji();
                    realizacjaZdDoDz.ZrealizujWieleZamowienDoFakturyRR_ZKonsolidacjaPozycji();
                    realizacjaZdDoDz.ZrealizujCzesciowoWieleZamowienDoFaktury_BezKonsolidacjiPozycji();
                    realizacjaZdDoDz.ZrealizujCzesciowoWieleZamowienDoFaktury_ZKonsolidacjaPozycji();
                    realizacjaZdDoDz.ZrealizujWybranePozycjeZamowien();

                    RealizacjaZamowieniaOdKlientaDoDokumentuMagazynowego realizacjaZkDoMagazynowego = new RealizacjaZamowieniaOdKlientaDoDokumentuMagazynowego(sfera);
                    realizacjaZkDoMagazynowego.ZrealizujJednoZamowienieDoWydania_BezKonsolidacjiPozycji();
                    realizacjaZkDoMagazynowego.ZrealizujWieleZamowienDoWydania_ZKonsolidacjaPozycji();
                    realizacjaZkDoMagazynowego.ZrealizujJednoZamowienieDoRozchoduWewnetrznego_BezKonsolidacji();
                    realizacjaZkDoMagazynowego.ZrealizujWieleZamowienDoRozchoduWewnetrznego_ZKonsolidacja();
                    realizacjaZkDoMagazynowego.ZrealizujWybranePozycjeZamowien();

                    RealizacjaZamowieniaDoDostawcyDoDokumentuMagazynowego realizacjaZdDoMagazynowego = new RealizacjaZamowieniaDoDostawcyDoDokumentuMagazynowego(sfera);
                    realizacjaZdDoMagazynowego.ZrealizujJednoZamowienieDoPrzyjecia_BezKonsolidacjiPozycji();
                    realizacjaZdDoMagazynowego.ZrealizujWieleZamowienDoPrzyjecia_ZKonsolidacjaPozycji();
                    realizacjaZdDoMagazynowego.ZrealizujJednoZamowienieDoPrzychoduWewnetrznego_BezKonsolidacji();
                    realizacjaZdDoMagazynowego.ZrealizujWieleZamowienDoPrzychoduWewnetrznego_ZKonsolidacja();
                    realizacjaZdDoMagazynowego.ZrealizujWybranePozycjeZamowien();

                    FakturowanieWydan fakturowanieWydan = new FakturowanieWydan(sfera);
                    fakturowanieWydan.FakturowanieJednegoWydaniaBezKonsolidacji();
                    fakturowanieWydan.FakturowanieJednegoWydaniaZKonsolidacja();
                    fakturowanieWydan.FakturowanieWieluWydanZKonsolidacja();
                    fakturowanieWydan.WystawienieParagonuImiennegoDoWydanZKonsolidacja();
                    fakturowanieWydan.FakturowanieWieluWydanZPrzenoszeniemPlatnosci();
                    fakturowanieWydan.FakturowanieWieluWydanBezPrzenoszeniaPlatnosci();
                    fakturowanieWydan.FakturowanieWydaniaZaliczkowego();
                    fakturowanieWydan.FakturowanieWieluWydanZaliczkowych();

                    FakturowaniePrzyjec fakturowaniePrzyjec = new FakturowaniePrzyjec(sfera);
                    fakturowaniePrzyjec.FakturowanieJednegoPrzyjeciaBezKonsolidacji();
                    fakturowaniePrzyjec.FakturowanieJednegoPrzyjeciaZKonsolidacja();
                    fakturowaniePrzyjec.FakturowanieWieluPrzyjecZKonsolidacja();
                    fakturowaniePrzyjec.FakturowanieWieluPrzyjecZPrzenoszeniemPlatnosci();
                    fakturowaniePrzyjec.FakturowanieWieluPrzyjecBezPrzenoszeniaPlatnosci();

                    LancuchyZaliczek zaliczki = new LancuchyZaliczek(sfera);
                    zaliczki.KolejnaFakturaZaliczkowa();
                    zaliczki.FakturaKoncowa();
                    zaliczki.ZwrotZaliczki();

                    FakturaDetalicznaNaPodstawieParagonu fakturaDetaliczna = new FakturaDetalicznaNaPodstawieParagonu(sfera);
                    fakturaDetaliczna.DodajFaktureDetalicznaNaPodstawieParagonuBezNIP();
                    fakturaDetaliczna.DodajFaktureDetalicznaNaPodstawieParagonuZNIPBezKonsolidacji();
                    fakturaDetaliczna.DodajFaktureDetalicznaNaPodstawieParagonuZNIPZKonsolidacja();
                    fakturaDetaliczna.DodajFaktureDetalicznaNaPodstawieWieluParagonowZNIPZKonsolidacja();
                    fakturaDetaliczna.DodajFaktureDetalicznaDlaCzesciPozycjiZParagonowZNIPZKonsolidacja();

                    GenerowanieZamowienDoDostawcow generowanieZD = new GenerowanieZamowienDoDostawcow(sfera);
                    generowanieZD.PrzetworzZKDoZD();
                    generowanieZD.GenerujZamowieniaDoDostawcowNaPodstawieZamowienOdKlientow();
                    generowanieZD.GenerujZamowieniaDoDostawcowNaPodstawieZlecenMontowania();
                    generowanieZD.GenerujZamowieniaDoDostawcowDlaAsortymentuPonizejIlosciOptymalnej();

                    Rozne rozne = new Rozne(sfera);
                    rozne.RealizacjaZamowieniaZUstawionaOsobaPrzedstawiciela_DokumentSprzedazy();
                    rozne.RealizacjaZamowieniaZUstawionaOsobaPrzedstawiciela_WydanieZewnetrzne();
                    rozne.RealizacjaZamowieniaZUstawionaOsobaPrzedstawicielaINumeremOryginalu_DokumentSprzedazy();
                    rozne.RealizacjaZamowieniaZUstawionaOsobaPrzedstawicielaINumeremOryginalu_WydanieZewnetrzne();
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
                Baza = "Nexo_Demo_1",
                Produkt = ProductId.Subiekt,
                LoginNexo = "Szef",
                HasloNexo = "robocze"
            };
            return dane;
        }
    }
}
