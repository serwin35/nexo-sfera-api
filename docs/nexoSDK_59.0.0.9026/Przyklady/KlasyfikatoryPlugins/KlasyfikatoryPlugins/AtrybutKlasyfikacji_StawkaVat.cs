using InsERT.Moria.Klasyfikatory;
using InsERT.Moria.ModelDanych;
using System;
using System.Linq.Expressions;

namespace KlasyfikatoryPlugins
{
    public class AtrybutKlasyfikacji_StawkaVat : AtrybutKlasyfikacji<Asortyment>
    {
        public override string Nazwa => "Stawka Vat";

        public override ZrodloDanychAtrybutuKlasyfikacji UtworzZrodloDanych(IKontekstKlasyfikacji kontekst)
            => kontekst.UtworzZrodloDanychDlaTypu(typeof(StawkaVat));

        protected override Expression<Func<Asortyment, bool>> UtworzWyrazenieFiltrujace(IWirtualnyElementKlasyfikujacy element, IKontekstKlasyfikacji kontekst)
        {
            if (!Guid.TryParse(element.Wartosc, out var id))
            {
                return null;
            }

            return asortyment => asortyment.StawkaVatKupno.Id == id
                              || asortyment.StawkaVatSprzedaz.Id == id;
        }
    }
}
