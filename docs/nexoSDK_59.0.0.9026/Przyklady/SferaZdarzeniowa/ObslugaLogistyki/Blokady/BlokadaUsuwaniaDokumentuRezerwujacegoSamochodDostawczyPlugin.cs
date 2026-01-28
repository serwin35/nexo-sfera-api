using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using ObslugaLogistyki.Parametry;
using ObslugaLogistyki.Pomocnicze;
using ObslugaLogistyki.Properties;
using System;

namespace ObslugaLogistyki.Blokady
{
    public class BlokadaUsuwaniaDokumentuRezerwujacegoSamochodDostawczyPlugin : KlientSferyZdarzeniowej<IDokument>
    {
        private readonly ObslugaPolWlasnychLogistyki _obslugaPolWlasnych;

        public BlokadaUsuwaniaDokumentuRezerwujacegoSamochodDostawczyPlugin(Func<IPolaWlasneAdv2AccessorFactory> polaWlasneAccessorFactory
            , Func<ISlownikiWlasne> slownikiWlasne
            , Func<IZaawansowanePolaWlasne> zaawansowanePolaWlasne)
        {
            _obslugaPolWlasnych = new ObslugaPolWlasnychLogistyki(polaWlasneAccessorFactory, slownikiWlasne, zaawansowanePolaWlasne);
        }

        public override void PrzedUsunieciemObiektu(IKontekstUsuwaniaObiektu<IDokument> kontekst)
        {
            if (!ObslugaLogistykiHelper.SprawdzWarunkiWstepne(kontekst
                , TypObslugiwanegoDokumentu.Zk | TypObslugiwanegoDokumentu.Wz | TypObslugiwanegoDokumentu.Ds
                , true))
                return;
            Dokument dokument = kontekst.ObiektBiznesowy.Dokument;
            if (dokument == null)
                throw new ArgumentException();

            IZaawansowanePoleWlasne samochodDostawczy = _obslugaPolWlasnych.PobierzPoleWlasneSamochodDostawczy(dokument);
            if (samochodDostawczy == null) // nie zdefiniowano odpowiedniego pola własnego
                return;

            ObslugaLogistykiHelper.ZablokujGdyZarezerwowanySamochodDostawczy(dokument, _obslugaPolWlasnych, kontekst, Resources.BlokadaUsuwaniaDokumentuRezerwujacegoSamochodDostawczyKomunikat);
        }
    }
}
