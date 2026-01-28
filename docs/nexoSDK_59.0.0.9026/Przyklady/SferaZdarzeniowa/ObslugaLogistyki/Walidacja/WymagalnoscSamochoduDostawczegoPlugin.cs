using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using InsERT.Mox.DataAccess.EntityFramework;
using ObslugaLogistyki.Parametry;
using ObslugaLogistyki.Pomocnicze;
using ObslugaLogistyki.Properties;
using System;
using System.Text.RegularExpressions;

namespace ObslugaLogistyki.Walidacja
{
    public class WymagalnoscSamochoduDostawczegoPlugin : KlientSferyZdarzeniowej<IDokument>
    {
        private readonly ObslugaPolWlasnychLogistyki _obslugaPolWlasnych;

        public WymagalnoscSamochoduDostawczegoPlugin(Func<IPolaWlasneAdv2AccessorFactory> polaWlasneAccessorFactory
            , Func<ISlownikiWlasne> slownikiWlasne
            , Func<IZaawansowanePolaWlasne> zaawansowanePolaWlasne)
        {
            _obslugaPolWlasnych = new ObslugaPolWlasnychLogistyki(polaWlasneAccessorFactory, slownikiWlasne, zaawansowanePolaWlasne);
        }
        public override void PrzedZapisemObiektu(IKontekstZdarzeniaPrzedZapisemObiektu<IDokument> kontekst)
        {
            if (!ObslugaLogistykiHelper.SprawdzWarunkiWstepne(kontekst, ParametryObslugi.Instance.SprawdzajWymagalnoscSamochoduDostawczego))
                return;
            Dokument dokument = kontekst.ObiektBiznesowy.Dokument;
            if (dokument == null)
                throw new ArgumentException();

            IZaawansowanePoleWlasne samochodDostawczy = _obslugaPolWlasnych.PobierzPoleWlasneSamochodDostawczy(dokument);
            if (samochodDostawczy == null) // nie zdefiniowano odpowiedniego pola własnego
                return;

            WalidujWybranySamochodDostawczy(dokument
                , dokument is DokumentDS ds ? ds.PolaWlasneAdv2 : dokument is DokumentWZ wz ? (object)wz.PolaWlasneAdv2 : dokument is DokumentZK zk ? (object)zk.PolaWlasneAdv2 : (object)dokument
                , samochodDostawczy.Id
                , kontekst);
        }

        public override void PoZmianieWlasciwosciObiektu(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IDokument> kontekst)
        {
            if (!ObslugaLogistykiHelper.SprawdzWarunkiWstepne(kontekst, ParametryObslugi.Instance.SprawdzajWymagalnoscSamochoduDostawczego))
                return;
            Dokument dokument = kontekst.ObiektBiznesowy.Dokument;
            if (dokument == null)
                throw new ArgumentException();

            if (!Regex.Match(kontekst.NazwaWlasciwosci, Resources.PoleWlasneIntegerRegex).Success) // nazwa pola w formacie Ixx gdzie x jest cyfrą
                return;

            IZaawansowanePoleWlasne samochodDostawczy = _obslugaPolWlasnych.PobierzPoleWlasneSamochodDostawczy(dokument);
            if (samochodDostawczy == null // nie zdefiniowano odpowiedniego pola własnego
                || !samochodDostawczy.Id.Equals(kontekst.NazwaWlasciwosci)) // zmieniono inne pole
                return;

            WalidujWybranySamochodDostawczy(dokument, kontekst.Dane, kontekst.NazwaWlasciwosci, kontekst);
        }

        private void WalidujWybranySamochodDostawczy(Dokument dokument, object dane, string pole, IKontekstZdarzeniaZWalidacja kontekst)
        {
            bool transportFirmowy = ObslugaLogistykiHelper.CzyTransportFirmowy(dokument);
            if (_obslugaPolWlasnych.PobierzWybranySamochodDostawczy(dokument) == null) // jeśli nie wybrano samochodu dostawczego
            {
                if (transportFirmowy) // jeśli na dokumencie jest wybrany transport firmowy -> błąd
                    kontekst.DodajBlad(string.Format(Resources.WymaganySamochodDostawczyKomunikat, dokument.SposobDostawy.Nazwa), dane as EntityDataObjectBase, pole);
                else
                    kontekst.UsunBlad(dane as EntityDataObjectBase, pole);
            }
            else
            {
                if (transportFirmowy)
                    kontekst.UsunOstrzezenie(dane as EntityDataObjectBase, pole);
                else // jeśli na dokumencie jest coś innego niż transport firmowy -> ostrzeżenie
                    kontekst.DodajOstrzezenie(Resources.SamochodDostawczyNiewymaganyKomunikat, dane as EntityDataObjectBase, pole);
            }
        }
    }
}
