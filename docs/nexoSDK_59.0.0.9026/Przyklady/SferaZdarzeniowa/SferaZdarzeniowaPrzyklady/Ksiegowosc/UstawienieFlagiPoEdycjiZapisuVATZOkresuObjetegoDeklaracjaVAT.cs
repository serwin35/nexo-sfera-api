using InsERT.Moria.Deklaracje;
using InsERT.Moria.EwidencjaVAT;
using InsERT.Moria.Flagi;
using InsERT.Moria.KontrolaSkarbowa;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Linq;

namespace SferaZdarzeniowaPrzyklady.Ksiegowosc
{
    /// <summary>
    /// Plugin ustawiający czerwoną flagę po edycji zapisu VAT z okresu objętego deklaracją VAT lub wysyłką VAT
    /// </summary>
    public class UstawienieFlagiPoEdycjiZapisuVATZOkresuObjetegoDeklaracjaVAT : KlientSferyZdarzeniowej<IZapisWEwidencjiVAT>
    {
        private const string MIESIAC_NALICZENIA = "MiesiacNaliczenia";

        public override void PoInicjalizacjiObiektu(IKontekstInicjalizacjiObiektu<IZapisWEwidencjiVAT> kontekst)
        {
            if (kontekst.StanObiektu != StanObiektu.Dodawany)
            {
                kontekst.ZmienneKlienta[MIESIAC_NALICZENIA] = kontekst.ObiektBiznesowy.Dane.DaneDodatkowe.MiesiacNaliczenia;
            }
        }

        public override void PoZapisieObiektu(IKontekstZdarzeniaPoZapisieObiektu<IZapisWEwidencjiVAT> kontekst)
        {
            if (kontekst.IdDanych is int idZapisu)
            {
                var miesiacAktualny = MiesiacAktualny(kontekst.Uchwyt, idZapisu);
                if (miesiacAktualny.HasValue
                    && (IstniejeWysylkaVAT(kontekst.Uchwyt, miesiacAktualny.Value) || IstniejeDeklaracja(kontekst.Uchwyt, miesiacAktualny.Value)))
                {
                    UstawFlage(kontekst.Uchwyt, kontekst.TypDanych, kontekst.IdDanych);
                }
                else if (kontekst.StanZapisanegoObiektu == StanZapisanegoObiektu.Poprawiony)
                {
                    var miesiacOryginalny = MiesiacOryginalny(kontekst.ZmienneKlienta);
                    if (miesiacOryginalny.HasValue
                        && miesiacOryginalny != miesiacAktualny
                        && (IstniejeWysylkaVAT(kontekst.Uchwyt, miesiacOryginalny.Value) || IstniejeDeklaracja(kontekst.Uchwyt, miesiacOryginalny.Value)))
                    {
                        UstawFlage(kontekst.Uchwyt, kontekst.TypDanych, kontekst.IdDanych);
                    }
                }
            }
        }

        private DateTime? MiesiacAktualny(IUchwyt uchwyt, int idZapisu)
        {
            return uchwyt.PodajObiektTypu<IZapisyWEwidencjiVAT>().Dane.Wszystkie()
                .Where(x => x.Id == idZapisu)
                .Select(x => x.DaneDodatkowe.MiesiacNaliczenia)
                .FirstOrDefault();
        }

        private DateTime? MiesiacOryginalny(IZmienneKlientaSferyZdarzeniowej zmienneKlienta)
        {
            if (zmienneKlienta.SprobujPobracWartoscZmiennej<DateTime?>(MIESIAC_NALICZENIA, out DateTime? miesiacOryginalny))
                return miesiacOryginalny;

            return null;
        }

        private bool IstniejeWysylkaVAT(IUchwyt uchwyt, DateTime miesiacNaliczenia)
        {
            var jpk = uchwyt.PodajObiektTypu<IJednolitePlikiKontrolne>();
            return jpk.Dane.ZnajdzWysylkeVATRozliczeniowaWOkresie(miesiacNaliczenia.PoczatekMiesiaca(), miesiacNaliczenia.KoniecMiesiaca()).Any();
        }

        private bool IstniejeDeklaracja(IUchwyt uchwyt, DateTime miesiacNaliczenia)
        {
            var deklaracje = uchwyt.PodajObiektTypu<IDeklaracje>();
            return deklaracje.Dane.ZnajdzDeklaracjeVATWOkresie(miesiacNaliczenia.PoczatekMiesiaca(), miesiacNaliczenia.KoniecMiesiaca(), true).Any();
        }

        private void UstawFlage(IUchwyt uchwyt, Type typDanych, object id)
        {
            var flagiWlasne = uchwyt.PodajObiektTypu<IFlagiWlasne>();
            flagiWlasne.NadajFlage(CzerwonaFlagaId(uchwyt), typDanych, id);
        }

        private int? _czerwonaFlagaId;
        private int CzerwonaFlagaId(IUchwyt uchwyt)
        {
            if (!_czerwonaFlagaId.HasValue)
            {
                _czerwonaFlagaId = uchwyt.PodajObiektTypu<IFlagiWlasne>().DaneDomyslne.FW_Czerwona.Id;
            }

            return _czerwonaFlagaId.Value;
        }
    }
}