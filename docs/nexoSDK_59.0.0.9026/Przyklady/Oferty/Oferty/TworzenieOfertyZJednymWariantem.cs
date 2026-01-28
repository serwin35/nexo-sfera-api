using InsERT.Moria.Asortymenty;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System.Linq;

namespace Oferty
{
    internal class TworzenieOfertyZJednymWariantem : TworzenieOfertyBase
    {
        public TworzenieOfertyZJednymWariantem(Uchwyt sfera) : base(sfera)
        { }

        internal override string KomunikatRozpoczecia => "Rozpoczęto tworzenie oferty z jednym wariantem.";

        internal override void WypelnijDaneOferty(IOferta ofertaBO)
        {
            DodajPrzykladowePozycjeDokumentu(ofertaBO);
        }

        private void DodajPrzykladowePozycjeDokumentu(IOferta ofertaBO)
        {
            string[] symboleAsortymentu = _sfera.PodajObiektTypu<IAsortymenty>().Dane.Wszystkie()
                .Select(x => x.Symbol)
                .Take(3)
                .ToArray();

            AspektWariantu aspektWariantu = ofertaBO.Dane.Warianty.Single();

            foreach (string symbolAsortymentu in symboleAsortymentu)
            {
                PozycjaDokumentu pozycja = ofertaBO.Pozycje.Dodaj(symbolAsortymentu);
                ofertaBO.DodajPozycjeDoWariantu(aspektWariantu, pozycja);
            }
        }
    }
}