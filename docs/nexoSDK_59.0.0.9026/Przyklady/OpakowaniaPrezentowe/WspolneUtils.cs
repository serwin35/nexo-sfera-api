using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;

namespace OpakowaniaPrezentowe
{
    public static class WspolneUtils
    {
        public static bool CzyFlagaWKonfiguracjiUstawiona(
            IUchwyt uchwyt,
            Dokument dokument,
            string nazwaFlagi)
        {
            if (SprobujPobracWartoscPolaWlasnegoKonfiguracji(uchwyt, dokument, nazwaFlagi, out bool wartosc))
            {
                return wartosc;
            }
            else
            {
                return false;
            }
        }

        public static bool SprobujPobracWartoscPolaWlasnegoKonfiguracji<T>(
            IUchwyt uchwyt,
            Dokument dokument,
            string nazwaPola,
            out T wartosc)
        {
            wartosc = default;

            IKonfiguracjaObowiazujaca konfiguracjaDokumentu = PobierzKonfiguracjeDokumentu(uchwyt, dokument);
            if (konfiguracjaDokumentu.PolaWlasne == null)
            {
                return false;
            }

            object wartoscObj = konfiguracjaDokumentu.PolaWlasne.Get<object>(nazwaPola);
            if (wartoscObj is T tWartosc)
            {
                wartosc = tWartosc;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static IKonfiguracjaObowiazujaca PobierzKonfiguracjeDokumentu(
            IUchwyt uchwyt,
            Dokument dokument)
        {
            IKonfiguracje konfiguracje = uchwyt.PodajObiektTypu<IKonfiguracje>();

            return konfiguracje.PobierzParametryKonfiguracji(dokument);
        }

        public static bool CzyAsortymentNalezyDoGrupyOpakowanPrezentowych(
            IUchwyt uchwyt,
            Asortyment asortyment,
            DokumentZK montowaneZamowienie)
        {
            IZaawansowanePolaWlasne polaWlasne = uchwyt.PodajObiektTypu<IZaawansowanePolaWlasne>();
            if (!polaWlasne.SprobujPobracZaawansowanePoleWlasne<Konfiguracja>(KonfiguracjaPlugina.Instancja.PoleWlasne_Konfiguracja_GrupaOpakowanPrezentowych, out IZaawansowanePoleWlasne poleWlasne))
            {
                return false;
            }

            if (!poleWlasne.JestReferencjaDoSlownika)
            {
                return false;
            }

            if (!SprobujPobracWartoscPolaWlasnegoKonfiguracji(uchwyt, montowaneZamowienie, KonfiguracjaPlugina.Instancja.PoleWlasne_Konfiguracja_GrupaOpakowanPrezentowych, out int grupaOpakowanId))
            {
                return false;
            }

            return asortyment?.Grupa?.Id == grupaOpakowanId;
        }
    }
}