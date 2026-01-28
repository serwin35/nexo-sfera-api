using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using ObslugaLogistyki.Parametry;
using ObslugaLogistyki.Pomocnicze;
using ObslugaLogistyki.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ObslugaLogistyki.Blokady
{
    public class BlokadaZmianyZarezerwowanegoSamochoduDostawczegoPlugin : KlientSferyZdarzeniowej<IDokument>
    {
        private readonly ObslugaPolWlasnychLogistyki _obslugaPolWlasnych;

        public BlokadaZmianyZarezerwowanegoSamochoduDostawczegoPlugin(Func<IPolaWlasneAdv2AccessorFactory> polaWlasneAccessorFactory
            , Func<ISlownikiWlasne> slownikiWlasne
            , Func<IZaawansowanePolaWlasne> zaawansowanePolaWlasne)
        {
            _obslugaPolWlasnych = new ObslugaPolWlasnychLogistyki(polaWlasneAccessorFactory, slownikiWlasne, zaawansowanePolaWlasne);
        }

        public override void PrzedZmianaWlasciwosciObiektu(IKontekstZdarzeniaPrzedZmianaWlasciwosciObiektu<IDokument> kontekst)
        {
            if (!ObslugaLogistykiHelper.SprawdzWarunkiWstepne(kontekst, TypObslugiwanegoDokumentu.Zk | TypObslugiwanegoDokumentu.Wz | TypObslugiwanegoDokumentu.Ds))
                return;
            if (kontekst.StanObiektu == StanObiektu.Dodawany)
                return; // na dodawanych dokumentach na pewno nie ma jeszcze rezerwacji samochodu
            Dokument dokument = kontekst.ObiektBiznesowy.Dokument;
            if (dokument == null)
                throw new ArgumentException();

            if (!Regex.Match(kontekst.NazwaWlasciwosci, Resources.PoleWlasneIntegerRegex).Success) // nazwa pola w formacie Ixx gdzie x jest cyfrą
                return;

            IZaawansowanePoleWlasne samochodDostawczy = _obslugaPolWlasnych.PobierzPoleWlasneSamochodDostawczy(dokument);
            if (samochodDostawczy == null // nie zdefiniowano odpowiedniego pola własnego
                || !samochodDostawczy.Id.Equals(kontekst.NazwaWlasciwosci)) // zmieniono inne pole
                return;

            ObslugaLogistykiHelper.ZablokujGdyZarezerwowanySamochodDostawczy(dokument, _obslugaPolWlasnych, kontekst, Resources.BlokadaZmianySamochoduDostawczegoKomunikat);
        }
    }
}
