using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie.Operacje;
using InsERT.Moria.SzybkaSprzedaz;

namespace SzybkaSprzedazPlugins
{
    public class Operacja_PoleWlasneSlownikWlasnySQL : OperacjaWOknieSzybkiejSprzedazy
    {
        private readonly IPolaWlasneAdv2AccessorFactory _polaWlasneAccessorFactory;

        public Operacja_PoleWlasneSlownikWlasnySQL(
            IPolaWlasneAdv2AccessorFactory polaWlasneAccessorFactory)
        {
            _polaWlasneAccessorFactory = polaWlasneAccessorFactory;
        }

        public override string Nazwa => "Pole własne: SlownikWlasnySQL";

        public override string Opis => "Operacja wprowadzania pola własnego ze słownika własnego SQL";

        protected override void Wykonaj(IKontekstSzybkiejSprzedazy kontekstDanych, IKontekstOperacji kontekstOperacji)
        {
            var accessor = _polaWlasneAccessorFactory.Utworz(kontekstDanych.Dokument.Dokument);
            var elementy = accessor.PobierzWartosciTypuSlownikWlasnySqlByGuid("SlownikWlasnySQL");

            if (kontekstDanych.WyswietlListe("Pole własne: SlownikWlasnySQL", elementy, accessor.PobierzWartoscTypuSlownikWlasnySqlByGuid("SlownikWlasnySQL"), out var wybor, v => v.Wartosc))
            {
                accessor.UstawWartoscTypuSlownikWlasnySqlByGuid("SlownikWlasnySQL", wybor.Klucz);
            }
        }
    }
}
