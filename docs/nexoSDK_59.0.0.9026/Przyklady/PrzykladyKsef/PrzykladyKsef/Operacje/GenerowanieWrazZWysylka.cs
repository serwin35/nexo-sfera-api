using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PrzykladyKsef.Operacje
{
    /// <summary>
    /// W tym przykładzie generowane są 3 faktury sprzedaży, dla których następnie generowane są e-Faktury z włączoną automatyczną wysyłką do KSeF.
    /// Następnie dla tych wysłanych e-Faktur pobierany jest status przetworzenia.
    /// </summary>
    public class GenerowanieWrazZWysylka : BazowaOperacja
    {
        public GenerowanieWrazZWysylka(Uchwyt sfera) : base(sfera) { }

        public override void Wykonaj()
        {
            List<Dokument> dokumenty = DodajDokumentySprzedazy(
                new string[] { "WOPINKLACE", "DZSO50", "PEWTK50" }
                , new decimal[] { 2m, 4m, 1m }
                , new string[] { "895-45-24-460", "852-56-52-753", "894-598-65-88" });
            WygenerujIWyslijZbiorczo(dokumenty);
        }

        private void WygenerujIWyslijZbiorczo(List<Dokument> dokumenty)
        {
            // generowanie e-Faktur dla podanej listy dokumentów:
            IFabrykaGeneratorowEFaktury fabryka = _sfera.FabrykaGeneratorowEFaktury();
            IEnumerable<IWynikGenerowaniaEFaktury> wynikGenerowania = fabryka.PobierzAktualny().WygenerujEFaktury(dokumenty
                // ustawienie parametru Wyslij na true oznacza, że wygenerowane e-Faktury będą od razu przekazywane do KSeF:
                , new ParametryGenerowaniaEFaktury() { Wyslij = true });
            foreach (IWynikGenerowaniaEFaktury wynik in wynikGenerowania)
            {
                if (wynik.Sukces)
                    Console.WriteLine($"Poprawnie wygenerowano i przekazano do KSeF dokument {wynik.NumerDokumentu}.");
                else
                    Console.WriteLine($"Nie udało się wygenerować lub wysłać dokumentu do KSeF: {string.Join(Environment.NewLine, wynik.Bledy)}");
            }
            IKoordynatorWysylaniaEFaktur koordynatorWysylania = _sfera.KoordynatorWysylaniaEFaktur();
            foreach (IWysylanaEFaktura wysylana in koordynatorWysylania.WysylaneFaktury.Values)
                Console.WriteLine($"e-Faktura {wysylana.Numer}: {wysylana.OpisStatusu}");
            // KSeF prawdopodobnie nie przetworzy wysłanych faktur natychmiastowo
            // więc aplikacja czeka przez określony czas (w przykładzie - 10s)
            Thread.Sleep(10000);
            List<IWynikSprawdzaniaStatusuWysylki> statusy = koordynatorWysylania.SprawdzStatus();
            ObsluzListeStatusowWysylki(statusy);
            // jeśli kolekcja WysylaneFaktury dalej zawiera jakieś elementy to oznacza,
            // że KSeF nie przetworzył jeszcze wszystkich wysłanych faktur
            // i należy operację ponowić
            if (koordynatorWysylania.WysylaneFaktury.Any())
                Console.WriteLine("Istnieją e-Faktury jeszcze nie przetworzone.");
            else
                Console.WriteLine("Wszystkie e-Faktury zostały przetworzone.");
        }
    }
}
