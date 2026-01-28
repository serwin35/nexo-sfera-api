using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie.Operacje;
using InsERT.Moria.SzybkaSprzedaz;

namespace SzybkaSprzedazPlugins
{
    public class Operacja_PoleWlasneSlownikSystemowy : OperacjaWOknieSzybkiejSprzedazy
    {
        private readonly IPolaWlasneAdv2AccessorFactory _polaWlasneAccessorFactory;

        public Operacja_PoleWlasneSlownikSystemowy(
            IPolaWlasneAdv2AccessorFactory polaWlasneAccessorFactory)
        {
            _polaWlasneAccessorFactory = polaWlasneAccessorFactory;
        }

        public override string Nazwa => "Pole własne: SlownikSystemowy";

        public override string Opis => "Operacja wprowadzania pola własnego ze słownika systemowego";

        protected override void Wykonaj(IKontekstSzybkiejSprzedazy kontekstDanych, IKontekstOperacji kontekstOperacji)
        {
            var accessor = _polaWlasneAccessorFactory.Utworz(kontekstDanych.Dokument.Dokument);
            var waluty = accessor.PobierzWartosciTypuSlownikSystemowyWalut("SlownikSystemowy");

            if (kontekstDanych.WyswietlListe("Pole własne: SlownikSystemowy", waluty, accessor.PobierzWartoscTypuSlownikSystemowyWalut("SlownikSystemowy"), out var wybor, v => v.Symbol))
            {
                accessor.UstawWartoscTypuSlownikSystemowyWalut("SlownikSystemowy", wybor.Id);
            }
        }
    }
}
