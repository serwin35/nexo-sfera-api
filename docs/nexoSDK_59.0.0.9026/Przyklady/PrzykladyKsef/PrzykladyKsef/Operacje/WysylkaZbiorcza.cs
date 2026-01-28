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
    /// W tym przykładzie najpierw generowane są 3 faktury sprzedaży, dla których następnie generowane są zbiorczo e-Faktury poprzez obiekt typu IGeneratorEFaktury.
    /// W kolejnym kroku wygenerowane e-Faktury są zbiorczo przekazywane do KSeF i później następuje odpytywanie o status ich przetworzenia.
    /// </summary>
    public class WysylkaZbiorcza : BazowaOperacja
    {
        public WysylkaZbiorcza(Uchwyt sfera) : base(sfera) { }

        public override void Wykonaj()
        {
            List<Dokument> dokumenty = DodajDokumentySprzedazy(
                new string[] { "DZSO100", "PEFLEUR15", "ZELAQUA" }
                , new decimal[] { 3m, 2m, 5m }
                , new string[] { "887-745-77-52", "894-56-53-567", "863-63-63-357" });
            WygenerujIWyslijZbiorczo(dokumenty);
        }

        private void WygenerujIWyslijZbiorczo(List<Dokument> dokumenty)
        {
            // generowanie e-Faktur dla podanej listy dokumentów:
            IFabrykaGeneratorowEFaktury fabrykaGeneratorow = _sfera.FabrykaGeneratorowEFaktury();
            IEnumerable<IWynikGenerowaniaEFaktury> wynikiGenerowania = fabrykaGeneratorow.PobierzAktualny().WygenerujEFaktury(dokumenty, new ParametryGenerowaniaEFaktury());
            IDokumentyElektroniczne dokumentyElektroniczne = _sfera.DokumentyElektroniczne();
            List<int> identyfikatoryWygenerowanychEFaktur = wynikiGenerowania.Where(w => w.Sukces && w.DokumentElektronicznyId.HasValue).Select(w => w.DokumentElektronicznyId.Value).ToList();
            List<DokumentElektroniczny> eFakturyDoWysylki = dokumentyElektroniczne.Dane.Wszystkie().Where(d => identyfikatoryWygenerowanychEFaktur.Contains(d.Id)).ToList();
            // przekazywanie do KSeF wygenerowanych e-Faktur:
            IKoordynatorWysylaniaEFaktur koordynatorWysylania = _sfera.KoordynatorWysylaniaEFaktur();
            List<IWynikPrzekazywaniaDokumentuDoWysylki> wynikWysylki = koordynatorWysylania.PrzekazDoWysylki(eFakturyDoWysylki, (progress, maxProgress, opis) => Console.WriteLine($"{opis} ({progress} z {maxProgress})"));
            foreach (IWynikPrzekazywaniaDokumentuDoWysylki wynik in wynikWysylki)
            {
                if (wynik.Sukces)
                    Console.WriteLine($"e-Faktura {wynik.NumerDokumentu} została poprawnie przekazana do KSeF.");
                else
                    Console.WriteLine($"e-Faktura {wynik.NumerDokumentu} nie została poprawnie wysłana: {string.Join(Environment.NewLine, wynik.Bledy)}");
            }
            // w kolekcji WysylaneFaktury obiektu IKoordynatorWysylaniaEFaktur przechowywane są dokumenty przekazane do KSeF, ale jeszcze nie przetworzone:
            foreach (IWysylanaEFaktura wysylana in koordynatorWysylania.WysylaneFaktury.Values)
                Console.WriteLine($"e-Faktura {wysylana.Numer} jest aktualnie przetwarzana: {wysylana.OpisStatusu}");
            // KSeF prawdopodobnie nie przetworzy wysłanych faktur natychmiastowo
            // więc aplikacja czeka przez określony czas (w przykładzie - 10s):
            Thread.Sleep(10000);
            // sprawdzenie statusu dla wszystkich aktualnie przetwarzanych dokumentów:
            List<IWynikSprawdzaniaStatusuWysylki> statusyWysylki = koordynatorWysylania.SprawdzStatus();
            ObsluzListeStatusowWysylki(statusyWysylki);
            // lub sprawdzenie statusu wybranych dokumentów:
            List<int> identyfikatoryNieprzetworzonych = statusyWysylki.Where(s => !s.PrzetwarzanieZakonczone).Select(s => s.DokumentId).ToList();
            if (identyfikatoryNieprzetworzonych.Any())
            {
                List<DokumentElektroniczny> eFakturyDoPonownegoSprawdzenia = dokumentyElektroniczne.Dane.Wszystkie().Where(d => identyfikatoryNieprzetworzonych.Contains(d.Id)).ToList();
                List<IWynikSprawdzaniaStatusuWysylki> statusyWysylkiWybranych = koordynatorWysylania.SprawdzStatus(eFakturyDoPonownegoSprawdzenia);
                ObsluzListeStatusowWysylki(statusyWysylkiWybranych);
            }
            if (koordynatorWysylania.WysylaneFaktury.Any())
                Console.WriteLine("Istnieją jeszcze dokumenty nieprzetworzone.");
            else
                Console.WriteLine("Wszystkie wysłane dokumenty zostały przetworzone.");
        }
    }
}
