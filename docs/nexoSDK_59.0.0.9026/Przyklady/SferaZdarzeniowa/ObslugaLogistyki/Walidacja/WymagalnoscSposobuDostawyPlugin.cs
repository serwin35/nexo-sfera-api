using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using ObslugaLogistyki.Parametry;
using ObslugaLogistyki.Pomocnicze;
using ObslugaLogistyki.Properties;

namespace ObslugaLogistyki.Walidacja
{
    public class WymagalnoscSposobuDostawyPlugin : KlientSferyZdarzeniowej<IDokument>
    {
        public WymagalnoscSposobuDostawyPlugin() { }

        public override void PrzedZapisemObiektu(IKontekstZdarzeniaPrzedZapisemObiektu<IDokument> kontekst)
        {
            if (!ObslugaLogistykiHelper.SprawdzWarunkiWstepne(kontekst, ParametryObslugi.Instance.WymagajSposobuDostawy))
                return;
            if (kontekst.ObiektBiznesowy.Dokument.SposobDostawy == null)
                UstawBladWymagalnosciSposobuDostawy(kontekst.ObiektBiznesowy.Dokument, kontekst);
        }

        public override void PoZmianieWlasciwosciObiektu(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IDokument> kontekst)
        {
            if (!ObslugaLogistykiHelper.SprawdzWarunkiWstepne(kontekst, ParametryObslugi.Instance.WymagajSposobuDostawy))
                return;
            if (!kontekst.NazwaWlasciwosci.Equals(nameof(Dokument.SposobDostawy)))
                return;
            if (kontekst.ObiektBiznesowy.Dokument.SposobDostawy == null)
                UstawBladWymagalnosciSposobuDostawy(kontekst.ObiektBiznesowy.Dokument, kontekst);
        }

        private void UstawBladWymagalnosciSposobuDostawy(Dokument dokument, IKontekstZdarzeniaZWalidacja kontekst) =>
            kontekst.DodajBlad(Resources.SposobDostawyWymaganyKomunikat, dokument, nameof(Dokument.SposobDostawy));

    }
}
