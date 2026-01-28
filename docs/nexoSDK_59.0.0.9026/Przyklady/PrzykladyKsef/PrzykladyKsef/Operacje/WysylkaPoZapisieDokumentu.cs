using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Sfera;
using System;
using System.Threading;

namespace PrzykladyKsef.Operacje
{
    /// <summary>
    /// W tym przykładzie generowany jest dokument sprzedaży, dla którego od razu po zapisie jest generowana e-Faktura, która potem jest przekazywana do KSeF oraz program oczekuje na nadanie numeru KSeF.
    /// </summary>
    public class WysylkaPoZapisieDokumentu : BazowaOperacja
    {
        public WysylkaPoZapisieDokumentu(Uchwyt sfera) : base(sfera) { }

        public override void Wykonaj()
        {
            WystawDokumentIWyslijDoKsef();
        }

        private void WystawDokumentIWyslijDoKsef()
        {
            Console.WriteLine("Przykład dodawania dokumentu sprzedaży oraz wysyłki po zapisie.");
            IDokumentySprzedazy dokumentySprzedazy = _sfera.DokumentySprzedazy();
            using (IDokumentSprzedazy faktura = dokumentySprzedazy.UtworzFaktureSprzedazy())
            {
                faktura.Dane.Uwagi = "Dokument wygenerowany i wysłany do KSeF przez Sferę nexo.";
                faktura.PodmiotyDokumentu.UstawNabywceWedlugNIP("836-84-63-635");
                faktura.Pozycje.Dodaj("WOBLACK100", 10m);
                faktura.Przelicz();
                faktura.Platnosci.DodajDomyslnaPlatnoscNatychmiastowaNaKwoteDokumentu();
                Console.WriteLine("Zapis dokumentu.");
                if (faktura.Zapisz())
                {
                    // generowanie e-Faktury:
                    IWynikGenerowaniaEFaktury wynikGenerowania = faktura.ObslugaKSeF.WygenerujEFakture(new ParametryGenerowaniaEFaktury());
                    if (wynikGenerowania.Sukces)
                    {
                        Console.WriteLine($"Wygenerowano e-Fakturę dla {faktura.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                        // przekazywanie wygenerowanej e-Faktury do KSeF:
                        IWynikPrzekazywaniaDokumentuDoWysylki wynikWysylki = faktura.ObslugaKSeF.PrzekazDoWysylki();
                        if (wynikWysylki.Sukces)
                        {
                            int proba = 0;
                            Console.WriteLine($"Poprawnie przekazano e-Fakturę {faktura.Dane.NumerWewnetrzny.PelnaSygnatura} do KSeF. Trwa oczekiwanie na nadanie numeru KSeF.");
                            IWynikSprawdzaniaStatusuWysylki statusWysylki;
                            do
                            {
                                Thread.Sleep(1000);
                                Console.WriteLine($"Odpytywanie KSeF o status wysyłki. Próba nr {++proba}.");
                                // sprawdzanie statusu przetworzenia wysłanej e-Faktury:
                                statusWysylki = faktura.ObslugaKSeF.CzyPrzetwarzanieZakonczone();

                            } while (statusWysylki.PrzetwarzanieZakonczone != true && proba < 10);
                            if (statusWysylki.Sukces)
                                Console.WriteLine($"e-Faktura została przyjęta przez KSeF i został jej nadany numer: {statusWysylki.NumerKsef}.");
                            else if (!statusWysylki.PrzetwarzanieZakonczone)
                                Console.WriteLine("KSeF nie przetworzył dokumentu w oczekiwanym czasie.");
                            else
                                Console.WriteLine($"Dokument nie został przyjęty przez KSeF:{Environment.NewLine}{string.Join(Environment.NewLine, statusWysylki.Bledy)}");
                        }
                        else
                            Console.WriteLine($"Nie udało się przekazać e-Faktury do KSEF:{Environment.NewLine}{string.Join(Environment.NewLine, wynikWysylki.Bledy)}");
                    }
                    else
                        Console.WriteLine($"Nie udało się wygenerować e-Faktury:{Environment.NewLine}{string.Join(Environment.NewLine, wynikGenerowania.Bledy)}");
                }
            }
        }
    }
}
