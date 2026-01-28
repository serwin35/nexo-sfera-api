using InsERT.Moria.Asortymenty;
using InsERT.Moria.Klasyfikatory;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie.Operacje;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace KlasyfikatoryPlugins
{
    public class AtrybutKlasyfikacji_KodCN : AtrybutKlasyfikacji<Asortyment>
    {
        private readonly IAsortymenty _asortymenty;

        public AtrybutKlasyfikacji_KodCN(
            IAsortymenty asortymenty)
        {
            _asortymenty = asortymenty;
        }

        public override string Nazwa => "Kod CN";

        public override ZrodloDanychAtrybutuKlasyfikacji UtworzZrodloDanych(IKontekstKlasyfikacji kontekst)
        {
            var kodyCN = _asortymenty
                .Dane
                .Wszystkie()
                .Where(a => a.KodCN != null && a.KodCN != "")
                .Select(a => a.KodCN)
                .Distinct();

            return ZrodloDanychAtrybutuKlasyfikacji
                .Utworz(kodyCN.AsEnumerable().Select(k => new WartoscAtrybutuKlasyfikacji(k)));
        }

        protected override void Przypisz(Asortyment obiekt, IWirtualnyElementKlasyfikujacy element, IKontekstKlasyfikowanegoObiektu kontekst)
        {
            obiekt.KodCN = element.Wartosc;
        }

        protected override WynikOperacji SprawdzCzyMoznaPrzypisac(Asortyment obiekt, IWirtualnyElementKlasyfikujacy element, IKontekstKlasyfikowanegoObiektu kontekst)
        {
            if (string.Equals(obiekt.KodCN, element.Wartosc))
            {
                return WynikOperacji.ZakonczonaPowodzeniem();
            }

            return WynikOperacji.GotowaDoWykonania();
        }

        protected override Expression<Func<Asortyment, bool>> UtworzWyrazenieFiltrujace(IWirtualnyElementKlasyfikujacy element, IKontekstKlasyfikacji kontekst)
        {
            return asortyment => asortyment.KodCN == element.Wartosc;
        }
    }
}
