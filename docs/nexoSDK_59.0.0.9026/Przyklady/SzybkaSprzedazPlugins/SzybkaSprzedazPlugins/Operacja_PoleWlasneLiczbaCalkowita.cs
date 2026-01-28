using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie.Operacje;
using InsERT.Moria.SzybkaSprzedaz;

namespace SzybkaSprzedazPlugins
{
    public class Operacja_PoleWlasneLiczbaCalkowita : OperacjaWOknieSzybkiejSprzedazy
    {
        private readonly IPolaWlasneAdv2AccessorFactory _polaWlasneAccessorFactory;

        public Operacja_PoleWlasneLiczbaCalkowita(
            IPolaWlasneAdv2AccessorFactory polaWlasneAccessorFactory)
        {
            _polaWlasneAccessorFactory = polaWlasneAccessorFactory;
        }

        public override string Nazwa => "Pole własne: LiczbaCalkowita";

        public override string Opis => "Operacja wprowadzania pola własnego całkowitoliczbowego";

        protected override void Wykonaj(IKontekstSzybkiejSprzedazy kontekstDanych, IKontekstOperacji kontekstOperacji)
        {
            var accessor = _polaWlasneAccessorFactory.Utworz(kontekstDanych.Dokument.Dokument);
            if (kontekstDanych.WprowadzLiczbeCalkowita("Pole własne: LiczbaCalkowita", out var wartosc, accessor.PobierzWartoscTypuLiczbaCalkowita("LiczbaCalkowita")))
            {
                accessor.UstawWartoscTypuLiczbaCalkowita("LiczbaCalkowita", wartosc);
            }
        }
    }
}
