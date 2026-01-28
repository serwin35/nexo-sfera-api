using InsERT.Moria.Asortymenty;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System.Collections.Generic;
using System.Linq;

namespace Oferty
{
    internal class TworzenieOfertyZDwomaWariantami : TworzenieOfertyBase
    {
        public TworzenieOfertyZDwomaWariantami(Uchwyt sfera) : base(sfera)
        { }

        internal override string KomunikatRozpoczecia => "Rozpoczęto tworzenie oferty z dwoma wariantami.";

        internal override void WypelnijDaneOferty(IOferta ofertaBO)
        {
            var przykladoweSymboleAsortymentu = _sfera.PodajObiektTypu<IAsortymenty>().Dane.Wszystkie()
                .Select(x => x.Symbol)
                .Take(4)
                .ToArray();

            var domyslnyAspektWariantu = ofertaBO.Dane.Warianty.Single();
            WypelnijAspektWariantu(ofertaBO, domyslnyAspektWariantu, przykladoweSymboleAsortymentu.Take(2));

            var drugiAspektWariantu = ofertaBO.DodajNowyWariant("Wariant 2");
            WypelnijAspektWariantu(ofertaBO, drugiAspektWariantu, przykladoweSymboleAsortymentu.Skip(2));
        }

        private void WypelnijAspektWariantu(IOferta ofertaBO, AspektWariantu aspektWariantu, IEnumerable<string> symboleAsortymentu)
        {
            foreach (string symbolAsortymentu in symboleAsortymentu)
            {
                PozycjaDokumentu pozycja = ofertaBO.Pozycje.Dodaj(symbolAsortymentu);
                ofertaBO.DodajPozycjeDoWariantu(aspektWariantu, pozycja);
            }
        }
    }
}