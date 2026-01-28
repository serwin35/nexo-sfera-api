using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Rozszerzanie;
using System;

namespace KsefPluginy
{
    /// <summary>
    /// Funkcja ukrywa wszystkie wartości pól związanych z płatnościami.
    /// </summary>
    public class UkrywaniePlatnosci : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private Guid _id = new Guid("E5E16589-0872-42E8-AD55-F18A29832B43");

        public Guid Identyfikator => _id;

        public string Nazwa => "Ukrywanie sekcji płatności";

        public string Opis => "Funkcja ukrywa dane płatności w treści e-Faktury";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            ignorujPole = pole.Equals(PolaEFaktury.Zaplacono)
                        || pole.Equals(PolaEFaktury.DataZaplaty)
                        || pole.Equals(PolaEFaktury.ZnacznikZaplatyCzesciowej)
                        || pole.Equals(PolaEFaktury.KwotaZaplatyCzesciowej)
                        || pole.Equals(PolaEFaktury.DataZaplatyCzesciowej)
                        || pole.Equals(PolaEFaktury.FormaPlatnosci)
                        || pole.Equals(PolaEFaktury.PlatnoscInna)
                        || pole.Equals(PolaEFaktury.OpisPlatnosci)
                        || pole.Equals(PolaEFaktury.Termin)
                        || pole.Equals(PolaEFaktury.TerminOpis)
                        || pole.Equals(PolaEFaktury.Ilosc)
                        || pole.Equals(PolaEFaktury.Jednostka)
                        || pole.Equals(PolaEFaktury.ZdarzeniePoczatkowe)
                        || pole.Equals(PolaEFaktury.TerminPlatnosci)
                        || pole.Equals(PolaEFaktury.TerminPlatnosciOpis)
                        || pole.Equals(PolaEFaktury.ZaplataCzesciowa)
                        || pole.Equals(PolaEFaktury.OpisRachunku)
                        || pole.Equals(PolaEFaktury.RachunekWlasnyBanku)
                        || pole.Equals(PolaEFaktury.NazwaBanku)
                        || pole.Equals(PolaEFaktury.NrRB)
                        || pole.Equals(PolaEFaktury.NrRBPL)
                        || pole.Equals(PolaEFaktury.NrRBZagr)
                        || pole.Equals(PolaEFaktury.SWIFT)
                        || pole.Equals(PolaEFaktury.WarunkiSkonta)
                        || pole.Equals(PolaEFaktury.WysokoscSkonta);

            return null;
        }
    }
}
