using InsERT.Moria.DeklaracjeZUS;
using InsERT.Moria.Rozszerzanie;

namespace SferaZdarzeniowaPrzyklady.Kadry
{
    public class BlokadaUsunieciaWyeksportowanejDeklaracjiZUS : KlientSferyZdarzeniowej<IDeklaracjaZUS>
    {
        public override void PrzedUsunieciemObiektu(IKontekstUsuwaniaObiektu<IDeklaracjaZUS> kontekst)
        {
            if (kontekst.ObiektBiznesowy.Dane.WyslanyDoPlatnika)
            {
                kontekst.ZablokujZdarzenie("Nie można usunąć deklaracji ZUS wysłanej do programu Płatnik.");
            }
        }
    }
}