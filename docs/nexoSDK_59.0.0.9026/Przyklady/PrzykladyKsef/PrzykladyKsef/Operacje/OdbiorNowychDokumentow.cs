using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Komunikacja.DocumentsGateway;
using InsERT.Moria.Sfera;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrzykladyKsef.Operacje
{
    /// <summary>
    /// Przykład przedstawia sposób odbioru e-Faktur od ostatniego odbioru.
    /// </summary>
    public class OdbiorNowychDokumentow : BazowaOperacja
    {
        public OdbiorNowychDokumentow(Uchwyt sfera) : base(sfera) { }

        public override void Wykonaj()
        {
            OdbierzNajnowsze();
        }

        private void OdbierzNajnowsze()
        {
            IKoordynatorOdbioruEFaktur koordynatorOdbioru = _sfera.KoordynatorOdbioruEFaktur();
            // pobieranie wszystkich e-Faktur gdzie moja firma występuje jako sprzedawca, nabywca lub jako inny podmiot (w zależności od ustawień w parametrach KSeF)
            // od ostatniego odbioru:
            IEnumerable<IWynikSynchronizacjiDokumentu> wynik = koordynatorOdbioru.Pobierz((progress, maxProgress, opis, czyProgressNiedeterministyczny) =>
            {
                if (czyProgressNiedeterministyczny) // ten parametr określa czy aktualnie wykonywana operacja określa aktualną liczbę kroków do wykonania
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
