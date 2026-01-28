using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using System;

namespace SferaZdarzeniowaPrzyklady.Kadry
{
    public class UstawianieInicjalowPracownika : KlientSferyZdarzeniowej<IPodmiot>
    {
        private const string ALIAS_POLA_WLASNEGO = "Inicjały";

        public override void PoZmianieWlasciwosciObiektu(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IPodmiot> kontekst)
        {
            if (PoleWlasneInicjalyIstnieje(kontekst.Uchwyt)
                && kontekst.Dane is Osoba osoba
                && osoba.Pracownik != null
                && (kontekst.NazwaWlasciwosci == nameof(Osoba.Imie) || kontekst.NazwaWlasciwosci == nameof(Osoba.Nazwisko)))
            {
                osoba.Pracownik.PolaWlasneAdv2.Set(ALIAS_POLA_WLASNEGO, StworzInicjały(osoba.Imie, osoba.Nazwisko));
            }
        }

        private string StworzInicjały(string imie, string nazwisko)
        {
            if (String.IsNullOrWhiteSpace(imie) || String.IsNullOrWhiteSpace(nazwisko))
                return null;

            return imie.Substring(0, 1) + nazwisko.Substring(0, 1);
        }

        private bool? _poleWlasneInicjalyIstnieje;
        private bool PoleWlasneInicjalyIstnieje(IUchwyt uchwyt)
        {
            if (!_poleWlasneInicjalyIstnieje.HasValue)
            {
                var zaawansowanePolaWlasne = uchwyt.PodajObiektTypu<IZaawansowanePolaWlasne>();
                _poleWlasneInicjalyIstnieje = zaawansowanePolaWlasne.PosiadaZaawansowanePoleWlasne<Pracownik>(ALIAS_POLA_WLASNEGO);
            }

            return _poleWlasneInicjalyIstnieje.Value;
        }
    }
}