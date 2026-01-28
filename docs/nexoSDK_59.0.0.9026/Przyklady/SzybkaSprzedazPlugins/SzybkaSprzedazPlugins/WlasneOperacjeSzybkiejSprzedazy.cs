using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using System;
using System.Collections.Generic;

namespace SzybkaSprzedazPlugins
{
    public class WlasneOperacjeSzybkiejSprzedazy : IGrupaOperacji
    {
        private readonly IPolaWlasneAdv2AccessorFactory _polaWlasneAccessorFactory;

        public WlasneOperacjeSzybkiejSprzedazy(
            IPolaWlasneAdv2AccessorFactory polaWlasneAccessorFactory)
        {
            _polaWlasneAccessorFactory = polaWlasneAccessorFactory;
        }

        public IEnumerable<Operacja> Operacje
        {
            get
            {
                yield return new Operacja_PoleWlasneData(_polaWlasneAccessorFactory);
                yield return new Operacja_PoleWlasneKwota(_polaWlasneAccessorFactory);
                yield return new Operacja_PoleWlasneLiczbaCalkowita(_polaWlasneAccessorFactory);
                yield return new Operacja_PoleWlasneSlownikSystemowy(_polaWlasneAccessorFactory);
                yield return new Operacja_PoleWlasneSlownikWlasny(_polaWlasneAccessorFactory);
                yield return new Operacja_PoleWlasneSlownikWlasnySQL(_polaWlasneAccessorFactory);
                yield return new Operacja_PoleWlasneTekst(_polaWlasneAccessorFactory);
                yield return new Operacja_PoleWlasneWartoscLogiczna(_polaWlasneAccessorFactory);
            }
        }

        public Guid Identyfikator => new Guid("B6B4BAF5-CF6B-43CE-B260-30F315FA64BC");

        public string Nazwa => "Własne operacje interfejsu dotykowego POS";

        public string Opis => "Grupa własnych operacji w oknie interfejsu dotykowego POS";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();
    }
}
