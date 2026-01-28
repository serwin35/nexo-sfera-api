using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie.Operacje;
using InsERT.Moria.SzybkaSprzedaz;

namespace SzybkaSprzedazPlugins
{
    public class Operacja_PoleWlasneKwota : OperacjaWOknieSzybkiejSprzedazy
    {
        private readonly IPolaWlasneAdv2AccessorFactory _polaWlasneAccessorFactory;

        public Operacja_PoleWlasneKwota(
            IPolaWlasneAdv2AccessorFactory polaWlasneAccessorFactory)
        {
            _polaWlasneAccessorFactory = polaWlasneAccessorFactory;
        }

        public override string Nazwa => "Pole własne: Kwota";

        public override string Opis => "Operacja wprowadzania pola własnego kwotowego";

        protected override void Wykonaj(IKontekstSzybkiejSprzedazy kontekstDanych, IKontekstOperacji kontekstOperacji)
        {
            var accessor = _polaWlasneAccessorFactory.Utworz(kontekstDanych.Dokument.Dokument);
            if (kontekstDanych.WprowadzKwote("Pole własne: Kwota", out var wartosc, accessor.PobierzWartoscTypuLiczbaRzeczywista("Kwota")))
            {
                accessor.UstawWartoscTypuLiczbaRzeczywista("Kwota", wartosc);
            }
        }
    }
}
