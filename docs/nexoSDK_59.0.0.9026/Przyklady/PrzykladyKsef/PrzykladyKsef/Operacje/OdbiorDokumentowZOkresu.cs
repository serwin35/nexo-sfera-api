using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Komunikacja.DocumentsGateway;
using InsERT.Moria.Sfera;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrzykladyKsef.Operacje
{
    /// <summary>
    /// Ten przykład prezentuje możliwość odbierania e-Faktur z KSeF wg zadanego okresu.
    /// </summary>
    public class OdbiorDokumentowZOkresu : BazowaOperacja
    {
        public OdbiorDokumentowZOkresu(Uchwyt sfera) : base(sfera) { }

        public override void Wykonaj()
        {
            DateTime dzisiaj = DateTime.Today;
            OdbierzZOkresu(dzisiaj.AddDays(-30), dzisiaj);
        }

        private void OdbierzZOkresu(DateTime dataPoczatkowa, DateTime dataKoncowa)
        {
            IKoordynatorOdbioruEFaktur koordynatorOdbioru = _sfera.KoordynatorOdbioruEFaktur();
            // pobieranie e-Faktur z zadanego okresu, w których moja firma występuje jako sprzedawca, nabywca lub jako inny podmiot (w zależności od ustawień w parametrach KSeF):
            IEnumerable<IWynikSynchronizacjiDokumentu> wynik = koordynatorOdbioru.Pobierz(dataPoczatkowa, dataKoncowa, (progress, maxProgress, opis, czyProgressNiedeterministyczny) =>
            {
                if (czyProgressNiedeterministyczny) // ten parametr decyduje o tym czy aktualnie wykonywana operacja określa aktualną liczbę kroków do wykonania
                    Console.WriteLine(opis);
                else
                    Console.WriteLine($"{opis} ({progress} z {maxProgress})");
                // wartość zwracana w tym miejscu określa czy operacja pobierania ma zostać przerwana:
                return false;
            });
            Console.WriteLine($"Pobrano {wynik.Where(w => w.Sukces).Count()} dokumentów. Pobrano błędnie {wynik.Where(w => !w.Sukces).Count()} dokumentów.");
        }
    }
}
