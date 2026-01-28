using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;

namespace RealizacjeDokumentow.Przyklady
{
    public class FakturowaniePrzyjec : RealizacjaBase
    {
        public FakturowaniePrzyjec(Uchwyt sfera) : base(sfera) { }

        /// <summary>
        /// Dodanie przyjęcia zewnętrznego z kilkoma pozycjami i zafakturowanie go bez konsolidacji pozycji.
        /// </summary>
        public void FakturowanieJednegoPrzyjeciaBezKonsolidacji()
        {
            DokumentPZ przyjecie = DodajPrzyjecieZewnetrzne("BEAUTY", null, null, "DZSO100", "PEFLEUR15", "PEWTK50", "DZSO100", "PEFLEUR15", "PEWTK50");
            Console.WriteLine("Fakturowanie przyjęcia zewnętrznego bez konsolidacji pozycji.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                faktura.WypelnijNaPodstawiePZ(new[] { przyjecie }, przyjecie, new ParametryGrupowaniaDZ()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                faktura.Dane.Uwagi = $"Dokument fakturuje przyjęcie zewnętrzne bez konsolidacji pozycji.";
                ZapiszDokumentZakupu(faktura);
            }
        }

        /// <summary>
        /// Dodanie przyjęcia zewnętrznego z kilkoma pozycjami i zafakturowanie go z konsolidacją pozycji.
        /// </summary>
        public void FakturowanieJednegoPrzyjeciaZKonsolidacja()
        {
            DokumentPZ przyjecie = DodajPrzyjecieZewnetrzne("BEAUTY", null, null, "DZSO100", "PEFLEUR15", "PEWTK50", "DZSO100", "PEFLEUR15", "PEWTK50");
            Console.WriteLine("Fakturowanie przyjęcia zewnętrznego z konsolidacją pozycji.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                faktura.WypelnijNaPodstawiePZ(new[] { przyjecie }, przyjecie, new ParametryGrupowaniaDZ()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiary
                });
                faktura.Dane.Uwagi = $"Dokument fakturuje przyjęcie zewnętrzne z konsolidacja pozycji.";
                ZapiszDokumentZakupu(faktura);
            }
        }

        /// <summary>
        /// Dodanie trzech przyjęć zewnętrznych z różnymi pozycjami i wystawienie do nich faktury zakupu z konsolidacją pozycji.
        /// </summary>
        public void FakturowanieWieluPrzyjecZKonsolidacja()
        {
            DokumentPZ przyjecie1 = DodajPrzyjecieZewnetrzne("SALON", null, null, "DZSO100", "PEFLEUR15", "PEWTK50", "DZSO100", "PEFLEUR15", "WOBLACK50");
            DokumentPZ przyjecie2 = DodajPrzyjecieZewnetrzne("SALON", null, null, "DZSO100", "PEFLEUR15", "WOBLACK100", "DZSO100", "PEFLEUR15", "PEWTK50");
            DokumentPZ przyjecie3 = DodajPrzyjecieZewnetrzne("SALON", null, null, "DZSO100", "WOBLACK50", "PEWTK50", "WOBLACK100", "PEFLEUR15", "PEWTK50");
            Console.WriteLine("Fakturowanie przyjęć zewnętrznych z konsolidacją pozycji.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                faktura.WypelnijNaPodstawiePZ(new[] { przyjecie1, przyjecie2, przyjecie3 }, przyjecie2, new ParametryGrupowaniaDZ()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiaryICenie
                });
                faktura.Dane.Uwagi = $"Dokument fakturuje wiele przyjęć zewnętrznych z konsolidacja pozycji.";
                ZapiszDokumentZakupu(faktura);
            }
        }

        /// <summary>
        /// Dodanie trzech przyjęć zewnętrznych z różnymi pozycjami, przedpłatą bankową i płatnością natychmiastową i wystawienie do nich faktury zakupu z przenoszeniem płatności oraz przedpłat.
        /// </summary>
        public void FakturowanieWieluPrzyjecZPrzenoszeniemPlatnosci()
        {
            DokumentPZ przyjecie1 = DodajPrzyjecieZewnetrzne("ALEGRO", DodajPrzedplateBankowa("ALEGRO", wplyw: false), null, "ZELAQUA", "PEFLEUR15", "PEWTK50", "DZSO100", "PEFLEUR15", "WOBLACK50");
            DokumentPZ przyjecie2 = DodajPrzyjecieZewnetrzne("ALEGRO", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "DZSO100", "PEFLEUR15", "WOBLACK100", "DZSO100", "PEFLEUR15", "PEWTK50");
            DokumentPZ przyjecie3 = DodajPrzyjecieZewnetrzne("ALEGRO", null, null, "DZSO100", "WOBLACK50", "PEWTK50", "WOBLACK100", "ZELAQUA", "PEWTK50");
            Console.WriteLine("Fakturowanie wydań zewnętrznych z przenoszeniem płatności.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                faktura.WypelnijNaPodstawiePZ(new[] { przyjecie1, przyjecie2, przyjecie3 }, przyjecie2, new ParametryGrupowaniaDZ()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiaryICenie,
                    PrzeniesNatychmiastowe = PrzenoszeniePlatnosciNatychmiastowych.Przepisz,
                    PrzeniesPrzedplaty = PrzenoszeniePrzedplat.Przepisz
                });
                faktura.Dane.Uwagi = $"Dokument fakturuje wiele przyjęć zewnętrznych z konsolidacja pozycji, przenoszeniem przedpłat oraz płatności natychmiastowych.";
                ZapiszDokumentZakupu(faktura);
            }
        }

        /// <summary>
        /// Dodanie trzech przyjęć zewnętrznych z różnymi pozycjami, przedpłatą bankową i płatnością natychmiastową i wystawienie do nich faktury sprzedaży bez przenoszenia płatności, przedpłat oraz bez konsolidacji pozycji.
        /// </summary>
        public void FakturowanieWieluPrzyjecBezPrzenoszeniaPlatnosci()
        {
            DokumentPZ przyjecie1 = DodajPrzyjecieZewnetrzne("ALEGRO", DodajPrzedplateBankowa("ALEGRO", wplyw: false), null, "ZELAQUA", "PEFLEUR15", "PEWTK50", "DZSO100", "PEFLEUR15", "WOBLACK50");
            DokumentPZ przyjecie2 = DodajPrzyjecieZewnetrzne("ALEGRO", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "DZSO100", "PEFLEUR15", "WOBLACK100", "DZSO100", "PEFLEUR15", "PEWTK50");
            DokumentPZ przyjecie3 = DodajPrzyjecieZewnetrzne("ALEGRO", null, null, "DZSO100", "WOBLACK50", "PEWTK50", "WOBLACK100", "ZELAQUA", "PEWTK50");
            Console.WriteLine("Fakturowanie wydań zewnętrznych bez przenoszenia płatności.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                faktura.WypelnijNaPodstawiePZ(new[] { przyjecie1, przyjecie2, przyjecie3 }, przyjecie2, new ParametryGrupowaniaDZ()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji,
                    PrzeniesNatychmiastowe = PrzenoszeniePlatnosciNatychmiastowych.Brak,
                    PrzeniesPrzedplaty = PrzenoszeniePrzedplat.Brak
                });
                faktura.Dane.Uwagi = $"Dokument fakturuje wiele przyjęć zewnętrznych bez konsolidacji pozycji, przenoszenia płatności oraz przedpłat.";
                ZapiszDokumentZakupu(faktura);
            }
        }


    }
}
