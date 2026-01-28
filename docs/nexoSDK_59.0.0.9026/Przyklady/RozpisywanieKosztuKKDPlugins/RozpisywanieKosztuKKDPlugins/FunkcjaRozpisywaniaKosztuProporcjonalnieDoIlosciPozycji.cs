using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RozpisywanieKosztuKKDPlugins
{
    public class FunkcjaRozpisywaniaKosztuProporcjonalnieDoIlosciPozycji : IFunkcjaRozpisywaniaKosztuDostawy
    {
        public Guid Identyfikator => new Guid("CF4982CA-F486-41FF-BD9A-2BE1A05E58F4");

        public string Nazwa => "Proporcjonalnie do ilości pozycji";

        public string Opis => "Funkcja rozpisująca koszt dostawy proporcjonalnie do ilości pozycji";

        public IDostawcaPluginow Dostawca { get; } = new DostawcaPluginow();

        public IEnumerable<KosztDostawyPozycji> Rozpisz(IEnumerable<PozycjaDokumentu> pozycje, decimal kosztDostawy, IKontekstKosztuDostawy kontekst)
        {
            var wartoscNetto = kosztDostawy;
            var listaPozycji = pozycje.ToList();
            var rozpisaneKoszty = new List<KosztDostawyPozycji>();

            if (listaPozycji.Count == 1)
            {
                rozpisaneKoszty.Add(new KosztDostawyPozycji(listaPozycji[0], wartoscNetto));
            }
            else
            {
                var sumaIlosciNaPozycjach = listaPozycji.Sum(a => (a as PozycjaKorekty)?.IloscRoznicaWJednostceBazowej ?? a.IloscWJednostceBazowej);
                if (sumaIlosciNaPozycjach == 0M)
                {
                    return Enumerable.Empty<KosztDostawyPozycji>();
                }

                decimal suma = 0M;
                foreach (var pozycja in listaPozycji)
                {
                    var iloscNaPozycji = (pozycja as PozycjaKorekty)?.IloscRoznicaWJednostceBazowej ?? pozycja.IloscWJednostceBazowej;
                    var kosztPozycji = decimal.Round(wartoscNetto * iloscNaPozycji / sumaIlosciNaPozycjach, kontekst.Waluta?.Precyzja ?? 3, MidpointRounding.AwayFromZero);

                    rozpisaneKoszty.Add(new KosztDostawyPozycji(pozycja, kosztPozycji));

                    suma += kosztPozycji;
                }

                var poprawka = wartoscNetto - suma;
                if (poprawka != 0M)
                {
                    rozpisaneKoszty.Last().Koszt += poprawka;
                }
            }

            return rozpisaneKoszty;
        }
    }
}
