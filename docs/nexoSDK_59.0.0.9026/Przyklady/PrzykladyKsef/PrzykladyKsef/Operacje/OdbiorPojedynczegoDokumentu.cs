using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Sfera;
using System;

namespace PrzykladyKsef.Operacje
{
    /// <summary>
    /// W tym przykładzie przedstawiona jest procedura pobierania pojedynczej e-Faktury na podstawie zadanego numeru KSeF.
    /// </summary>
    public class OdbiorPojedynczegoDokumentu : BazowaOperacja
    {
        public OdbiorPojedynczegoDokumentu(Uchwyt sfera) : base(sfera) { }

        public override void Wykonaj()
        {
            OdbierzNaPodstawieKsefId("2273161387-20220413-CC7F45-60087E-97");
        }

        private void OdbierzNaPodstawieKsefId(string ksefId)
        {
            IKoordynatorOdbioruEFaktur koordynatorOdbioru = _sfera.KoordynatorOdbioruEFaktur();
            IWynikWczytywaniaEFaktury wynik = koordynatorOdbioru.Pobierz(ksefId);
            if (wynik.Sukces)
                Console.WriteLine($"Poprawnie pobrano pojedynczy dokument: {wynik.NumerKSeF}.");
            else
                Console.WriteLine($"Podczas pobierania pojedynczego dokumentu wystąpiły błędy: {string.Join(Environment.NewLine, wynik.Bledy)}");
        }
    }
}
