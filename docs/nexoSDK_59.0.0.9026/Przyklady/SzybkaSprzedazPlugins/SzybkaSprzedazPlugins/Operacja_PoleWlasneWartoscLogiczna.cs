using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie.Operacje;
using InsERT.Moria.SzybkaSprzedaz;

namespace SzybkaSprzedazPlugins
{
    public class Operacja_PoleWlasneWartoscLogiczna : OperacjaWOknieSzybkiejSprzedazy
    {
        private readonly IPolaWlasneAdv2AccessorFactory _polaWlasneAccessorFactory;

        public Operacja_PoleWlasneWartoscLogiczna(
            IPolaWlasneAdv2AccessorFactory polaWlasneAccessorFactory)
        {
            _polaWlasneAccessorFactory = polaWlasneAccessorFactory;
        }

        public override string Nazwa => "Pole własne: WartoscLogiczna";

        public override string Opis => "Operacja wprowadzania pola własnego logicznego";

        protected override void Wykonaj(IKontekstSzybkiejSprzedazy kontekstDanych, IKontekstOperacji kontekstOperacji)
        {
            var accessor = _polaWlasneAccessorFactory.Utworz(kontekstDanych.Dokument.Dokument);
            if (kontekstDanych.WybierzWartoscLogiczna("Pole własne: WartoscLogiczna", accessor.PobierzWartoscTypuLogicznego("WartoscLogiczna"), out var wybor))
            {
                accessor.UstawWartoscTypuLogicznego("WartoscLogiczna", wybor);
            }
        }
    }
}
