using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie.Operacje;
using InsERT.Moria.SzybkaSprzedaz;

namespace SzybkaSprzedazPlugins
{
    public class Operacja_PoleWlasneSlownikWlasny : OperacjaWOknieSzybkiejSprzedazy
    {
        private readonly IPolaWlasneAdv2AccessorFactory _polaWlasneAccessorFactory;

        public Operacja_PoleWlasneSlownikWlasny(
            IPolaWlasneAdv2AccessorFactory polaWlasneAccessorFactory)
        {
            _polaWlasneAccessorFactory = polaWlasneAccessorFactory;
        }

        public override string Nazwa => "Pole własne: SlownikWlasny";

        public override string Opis => "Operacja wprowadzania pola własnego ze słownika własnego";

        protected override void Wykonaj(IKontekstSzybkiejSprzedazy kontekstDanych, IKontekstOperacji kontekstOperacji)
        {
            var accessor = _polaWlasneAccessorFactory.Utworz(kontekstDanych.Dokument.Dokument);
            var elementy = accessor.PobierzWartosciTypuSlownikWlasny("SlownikWlasny");

            if (kontekstDanych.WyswietlListe("Pole własne: SlownikWlasny", elementy, accessor.PobierzWartoscTypuSlownikWlasny("SlownikWlasny"), out var wybor, v => v.Wartosc))
            {
                accessor.UstawWartoscTypuSlownikWlasny("SlownikWlasny", wybor.Klucz);
            }
        }
    }
}
