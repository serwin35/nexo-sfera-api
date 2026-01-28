using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;

namespace SferaZdarzeniowaPrzyklady.Subiekt
{
    public class WymaganaNazwaAsortymentu : KlientSferyZdarzeniowej<IAsortyment>
    {
        public override void PoZmianieWlasciwosciObiektu(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IAsortyment> kontekst)
        {
            if (kontekst.Dane is Asortyment asortyment
                && kontekst.NazwaWlasciwosci == nameof(Asortyment.Nazwa))
            {
                WalidujNazweAsortymentu(kontekst, asortyment);
            }
        }

        public override void PrzedZapisemObiektu(IKontekstZdarzeniaPrzedZapisemObiektu<IAsortyment> kontekst)
        {
            WalidujNazweAsortymentu(kontekst, kontekst.ObiektBiznesowy.Dane);
        }

        private void WalidujNazweAsortymentu(IKontekstZdarzeniaZWalidacja kontekst, Asortyment asortyment)
        {
            kontekst.UsunBlad(asortyment, nameof(asortyment.Nazwa));
            if (String.IsNullOrWhiteSpace(asortyment.Nazwa))
                kontekst.DodajBlad("Należy podać nazwę asortymentu.", asortyment, nameof(asortyment.Nazwa));
        }
    }
}