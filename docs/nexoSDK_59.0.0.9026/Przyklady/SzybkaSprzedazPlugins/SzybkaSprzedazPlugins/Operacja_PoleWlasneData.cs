using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie.Operacje;
using InsERT.Moria.SzybkaSprzedaz;

namespace SzybkaSprzedazPlugins
{
    public class Operacja_PoleWlasneData : OperacjaWOknieSzybkiejSprzedazy
    {
        private readonly IPolaWlasneAdv2AccessorFactory _polaWlasneAccessorFactory;

        public Operacja_PoleWlasneData(
            IPolaWlasneAdv2AccessorFactory polaWlasneAccessorFactory)
        {
            _polaWlasneAccessorFactory = polaWlasneAccessorFactory;
        }

        public override string Nazwa => "Pole własne: Data";

        public override string Opis => "Operacja wprowadzania pola własnego datowego";

        protected override void Wykonaj(IKontekstSzybkiejSprzedazy kontekstDanych, IKontekstOperacji kontekstOperacji)
        {
            var accessor = _polaWlasneAccessorFactory.Utworz(kontekstDanych.Dokument.Dokument);
            if (kontekstDanych.WprowadzDate("Pole własne: Data", out var wartosc, accessor.PobierzWartoscTypuData("Data")))
            {
                accessor.UstawWartoscTypuData("Data", wartosc);
            }
        }
    }
}
