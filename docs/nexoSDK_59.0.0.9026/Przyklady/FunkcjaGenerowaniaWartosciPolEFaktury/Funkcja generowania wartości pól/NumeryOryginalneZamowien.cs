using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Runtime.InteropServices;

namespace KsefPluginy
{
    public class NumeryOryginalneZamowien : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private readonly Guid _id = new Guid("B69F1642-6C85-42D2-93BA-B450DE9035B2");

        public Guid Identyfikator => _id;

        public string Nazwa => "Numery i daty oryginalne zamówień";

        public string Opis => "Wypełnia sekcję Zamowienia w węźle WarunkiTransakcji danymi oryginalnych zamówień.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            string wartosc = null;
            ignorujPole = false;
            // sprawdzamy czy znajdujemy się w węźle dotyczącym umów
            if (kontekst.SciezkaPola.StartsWith("Fa\\WarunkiTransakcji\\Zamowienia")
                // nexo przekazuje w polu obiekt encję realizowanego zamówienia od klienta
                && kontekst.Obiekt is DokumentZK zamowienie)
            {
                switch (pole)
                {
                    case PolaEFaktury.NrZamowienia:
                        {
                            // jeśli numer oryginalny jest niewypełniony to je ignorujemy:
                            if (string.IsNullOrWhiteSpace(zamowienie.NumerZewnetrzny))
                                ignorujPole = true;
                            else
                                wartosc = zamowienie.NumerZewnetrzny;
                        }
                        break;
                    case PolaEFaktury.DataZamowienia:
                        {
                            // jeśli data oryginału jest niewypełnione to ją ignorujemy:
                            if (zamowienie.DataWydaniaWystawienia.HasValue)
                                wartosc = zamowienie.DataWydaniaWystawienia.Value.ToString("yyyy-MM-dd");
                            else
                                ignorujPole = true;
                        }
                        break;
                }
            }
            return wartosc;
        }
    }
}
