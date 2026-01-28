using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using ObslugaLogistyki.Parametry;
using ObslugaLogistyki.Properties;
using System.Linq;

namespace ObslugaLogistyki.Pomocnicze
{
    public static class ObslugaLogistykiHelper
    {
        internal static bool CzyDokumentRezerwujeSamochodDostawczy(int idDokumentu, IDokumenty dokumenty)
        {
            var daneDokumentu = dokumenty.Dane.Wszystkie()
                .Where(d => d.Id == idDokumentu
                    && ((d is DokumentDS && (d as DokumentDS).AutomatyczneWZ.Any()) || (d is DokumentWZ && !(d as DokumentWZ).PowstalZHandlowego)))
                .Select(d => new
                {
                    NazwaSposobuDostawy = d.SposobDostawy.Nazwa,
                    CzyDS = d is DokumentDS,
                    CzyWZ = d is DokumentWZ,
                    SamochodDostawczy = d is DokumentDS ? (d as DokumentDS).PolaWlasneAdv2.Get<int?>(Resources.NazwaPolaWlasnego)
                        : d is DokumentWZ ? (d as DokumentWZ).PolaWlasneAdv2.Get<int?>(Resources.NazwaPolaWlasnego) : null
                })
                .ResolveExtensionProperties()
                .FirstOrDefault();
            return daneDokumentu != null
                && (daneDokumentu.CzyDS ? ParametryObslugi.Instance.RezerwujSamochodDostawczyNaDs : ParametryObslugi.Instance.RezerwujSamochodDostawczyNaWz)
                && CzyTransportFirmowy(daneDokumentu.NazwaSposobuDostawy) // tylko gdy na dokumencie jest wybrany transport firmowy
                && daneDokumentu.SamochodDostawczy.HasValue; // i na dokumencie wybrano samochód dostawczy
        }

        internal static bool CzyTransportFirmowy(Dokument dokument) => CzyTransportFirmowy(dokument.SposobDostawy?.Nazwa);

        internal static bool CzyTransportFirmowy(string nazwa) => nazwa?.ToLower()?.Contains("transport firmowy") == true;

        internal static bool SprawdzWarunkiWstepne(IKontekstZdarzeniaPoZapisieObiektu<IDokument> kontekst)
        {
            if (kontekst.StanZapisanegoObiektu != StanZapisanegoObiektu.Dodany) // rezerwujemy zasoby tylko dla nowododawanych dokumentów
                return false;
            if (kontekst.IdDanych == null || !(kontekst.IdDanych is int))
                return false;
            return true;
        }

        private static bool CzyObiektBiznesowyObslugiwany(IDokument obiektBiznesowy, TypObslugiwanegoDokumentu parametr) =>
            (obiektBiznesowy is IDokumentSprzedazy && parametr.HasFlag(TypObslugiwanegoDokumentu.Ds)) // dokument sprzedaży lub
            || (obiektBiznesowy is IZamowienieOdKlienta && parametr.HasFlag(TypObslugiwanegoDokumentu.Zk)) // lub zamówienie od klienta
            || (obiektBiznesowy is IWydanieZewnetrzne wzBo && !wzBo.Dane.PowstalZHandlowego && parametr.HasFlag(TypObslugiwanegoDokumentu.Wz)); // wydanie zewnętrzne NIEautomatyczne

        internal static bool SprawdzWarunkiWstepne(IKontekstZdarzeniaObiektu<IDokument> kontekst, TypObslugiwanegoDokumentu parametr, bool usuwanie = false)
        {
            if (usuwanie)
            {
                if (kontekst.StanObiektu != StanObiektu.Usuwany)
                    return false;
            }
            else
            {
                if (kontekst.StanObiektu == StanObiektu.Dezaktywowany
                    || kontekst.StanObiektu == StanObiektu.Usuwany)
                    return false;
            }
            if (!CzyObiektBiznesowyObslugiwany(kontekst.ObiektBiznesowy, parametr))
                return false;
            if (kontekst.ObiektBiznesowy.Dokument == null)
                return false;
            return true;
        }

        internal static void ZablokujGdyZarezerwowanySamochodDostawczy(Dokument dokument
            , ObslugaPolWlasnychLogistyki obslugaPolWlasnych
            , IKontekstBlokowalnegoZdarzenia kontekst
            , string komunikatBlokady)
        {
            int? wybranySamochodDostawczy = obslugaPolWlasnych.PobierzWybranySamochodDostawczy(dokument);
            if (wybranySamochodDostawczy.HasValue // wybrano samochód dostawczy
                && dokument.Dzialania.Any(dz => dz.RezerwacjeZasobow.Any(r => r.Zasob.Id == wybranySamochodDostawczy.Value))) // do dokumentu powstało działanie z zarezerwowanym samochodem dostawczym
            {
                kontekst.ZablokujZdarzenie(komunikatBlokady);
            }
        }
    }
}
