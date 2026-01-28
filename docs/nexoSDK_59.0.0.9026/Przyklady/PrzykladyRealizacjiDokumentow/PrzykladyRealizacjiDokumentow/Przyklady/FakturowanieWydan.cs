using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;
using System.Linq;

namespace RealizacjeDokumentow.Przyklady
{
    public class FakturowanieWydan : RealizacjaBase
    {
        public FakturowanieWydan(Uchwyt sfera) : base(sfera) { }

        /// <summary>
        /// Dodanie wydania zewnętrznego z kilkoma pozycjami i zafakturowanie go bez konsolidacji pozycji.
        /// </summary>
        public void FakturowanieJednegoWydaniaBezKonsolidacji()
        {
            DokumentWZ wydanie = DodajWydanieZewnetrzne("BEAUTY", null, null, "DZSO100", "PEFLEUR15", "PEWTK50", "DZSO100", "PEFLEUR15", "PEWTK50");
            Console.WriteLine("Fakturowanie wydania zewnętrznego bez konsolidacji pozycji.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieWZ(new[] { wydanie }, wydanie, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                faktura.Dane.Uwagi = $"Dokument fakturuje wydanie zewnętrzne bez konsolidacji pozycji.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodanie wydania zewnętrznego z kilkoma pozycjami i zafakturowanie go z konsolidacją pozycji.
        /// </summary>
        public void FakturowanieJednegoWydaniaZKonsolidacja()
        {
            DokumentWZ wydanie = DodajWydanieZewnetrzne("BEAUTY", null, null, "DZSO100", "PEFLEUR15", "PEWTK50", "DZSO100", "PEFLEUR15", "PEWTK50");
            Console.WriteLine("Fakturowanie wydania zewnętrznego z konsolidacją pozycji.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieWZ(new[] { wydanie }, wydanie, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiary
                });
                faktura.Dane.Uwagi = $"Dokument fakturuje wydanie zewnętrzne z konsolidacja pozycji.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodanie trzech wydań zewnętrznych z różnymi pozycjami i wystawienie do nich faktury sprzedaży z konsolidacją pozycji.
        /// </summary>
        public void FakturowanieWieluWydanZKonsolidacja()
        {
            DokumentWZ wydanie1 = DodajWydanieZewnetrzne("SALON", null, null, "DZSO100", "PEFLEUR15", "PEWTK50", "DZSO100", "PEFLEUR15", "WOBLACK50");
            DokumentWZ wydanie2 = DodajWydanieZewnetrzne("SALON", null, null, "DZSO100", "PEFLEUR15", "WOBLACK100", "DZSO100", "PEFLEUR15", "PEWTK50");
            DokumentWZ wydanie3 = DodajWydanieZewnetrzne("SALON", null, null, "DZSO100", "WOBLACK50", "PEWTK50", "WOBLACK100", "PEFLEUR15", "PEWTK50");
            Console.WriteLine("Fakturowanie wydań zewnętrznych z konsolidacją pozycji.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieWZ(new[] { wydanie1, wydanie2, wydanie3 }, wydanie2, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiaryICenie
                });
                faktura.Dane.Uwagi = $"Dokument fakturuje wiele wydań zewnętrznych z konsolidacja pozycji.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodanie trzech wydań zewnętrznych z różnymi pozycjami i wystawienie do nich paragonu imiennego z konsolidacją pozycji.
        /// </summary>
        public void WystawienieParagonuImiennegoDoWydanZKonsolidacja()
        {
            DokumentWZ wydanie1 = DodajWydanieZewnetrzne("ALEX", null, null, "ZELAQUA", "PEFLEUR15", "PEWTK50", "DZSO100", "PEFLEUR15", "WOBLACK50");
            DokumentWZ wydanie2 = DodajWydanieZewnetrzne("ALEX", null, null, "DZSO100", "PEFLEUR15", "WOBLACK100", "DZSO100", "PEFLEUR15", "PEWTK50");
            DokumentWZ wydanie3 = DodajWydanieZewnetrzne("ALEX", null, null, "DZSO100", "WOBLACK50", "PEWTK50", "WOBLACK100", "ZELAQUA", "PEWTK50");
            Console.WriteLine("Wystawianie paragonu imiennego do wydań zewnętrznych.");
            using (IDokumentSprzedazy paragon = _sfera.DokumentySprzedazy().UtworzParagonImienny())
            {
                paragon.WypelnijNaPodstawieWZ(new[] { wydanie1, wydanie2, wydanie3 }, wydanie3, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiaryICenie
                });
                paragon.Dane.Uwagi = $"Dokument wystawiony do wielu wydań zewnętrznych z konsolidacja pozycji.";
                ZapiszDokumentSprzedazy(paragon);
            }
        }

        /// <summary>
        /// Dodanie trzech wydań zewnętrznych z różnymi pozycjami, przedpłatą bankową i płatnością natychmiastową i wystawienie do nich faktury sprzedaży z przenoszeniem płatności oraz przedpłat.
        /// </summary>
        public void FakturowanieWieluWydanZPrzenoszeniemPlatnosci()
        {
            DokumentWZ wydanie1 = DodajWydanieZewnetrzne("ALEGRO", DodajPrzedplateBankowa("ALEGRO"), null, "ZELAQUA", "PEFLEUR15", "PEWTK50", "DZSO100", "PEFLEUR15", "WOBLACK50");
            DokumentWZ wydanie2 = DodajWydanieZewnetrzne("ALEGRO", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "DZSO100", "PEFLEUR15", "WOBLACK100", "DZSO100", "PEFLEUR15", "PEWTK50");
            DokumentWZ wydanie3 = DodajWydanieZewnetrzne("ALEGRO", null, null, "DZSO100", "WOBLACK50", "PEWTK50", "WOBLACK100", "ZELAQUA", "PEWTK50");
            Console.WriteLine("Fakturowanie wydań zewnętrznych z przenoszeniem płatności.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieWZ(new[] { wydanie1, wydanie2, wydanie3 }, wydanie2, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiaryICenie,
                    PrzeniesNatychmiastowe = PrzenoszeniePlatnosciNatychmiastowych.Przepisz,
                    PrzeniesPrzedplaty = PrzenoszeniePrzedplat.Przepisz
                });
                faktura.Dane.Uwagi = $"Dokument fakturuje wiele wydań zewnętrznych z konsolidacja pozycji, przenoszeniem przedpłat oraz płatności natychmiastowych.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodanie trzech wydań zewnętrznych z różnymi pozycjami, przedpłatą bankową i płatnością natychmiastową i wystawienie do nich faktury sprzedaży bez przenoszenia płatności, przedpłat oraz bez konsolidacji pozycji.
        /// </summary>
        public void FakturowanieWieluWydanBezPrzenoszeniaPlatnosci()
        {
            DokumentWZ wydanie1 = DodajWydanieZewnetrzne("ALEGRO", DodajPrzedplateBankowa("ALEGRO"), null, "ZELAQUA", "PEFLEUR15", "PEWTK50", "DZSO100", "PEFLEUR15", "WOBLACK50");
            DokumentWZ wydanie2 = DodajWydanieZewnetrzne("ALEGRO", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "DZSO100", "PEFLEUR15", "WOBLACK100", "DZSO100", "PEFLEUR15", "PEWTK50");
            DokumentWZ wydanie3 = DodajWydanieZewnetrzne("ALEGRO", null, null, "DZSO100", "WOBLACK50", "PEWTK50", "WOBLACK100", "ZELAQUA", "PEWTK50");
            Console.WriteLine("Fakturowanie wydań zewnętrznych bez przenoszenia płatności.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieWZ(new[] { wydanie1, wydanie2, wydanie3 }, wydanie2, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji,
                    PrzeniesNatychmiastowe = PrzenoszeniePlatnosciNatychmiastowych.Brak,
                    PrzeniesPrzedplaty = PrzenoszeniePrzedplat.Brak
                });
                faktura.Dane.Uwagi = $"Dokument fakturuje wiele wydań zewnętrznych bez konsolidacji pozycji, przenoszenia płatności oraz przedpłat.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodaje zamówienie od klienta z kilkoma pozycjami oraz płatnością natychmiastową, realizuje zamówienie do faktury zaliczkowej z przenoszeniem płatności natychmiastowych,
        /// dodaje wydanie zewnętrzne na ilość zaliczkowaną oraz fakturuje je zerową fakturą zaliczkową do wydań.
        /// </summary>
        public void FakturowanieWydaniaZaliczkowego()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("KOBRA", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "DZFOREVER", "PEFLEUR15", "POYAR01", "WOBLACK100");
            Console.WriteLine("Realizacja zamówienia do faktury zaliczkowej.");
            DokumentDS fakturaZaliczkowa;
            DokumentWZ wydanieZaliczkowe;
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureZaliczkowa())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary,
                    PrzeniesNatychmiastowe = PrzenoszeniePlatnosciNatychmiastowych.Przepisz,
                    PrzeniesPrzedplaty = PrzenoszeniePrzedplat.Przepisz
                });
                faktura.Dane.Uwagi = $"Dokument realizuje zamówienie od klienta z płatnością natychmiastową.";
                fakturaZaliczkowa = ZapiszDokumentSprzedazy(faktura);
            }
            using (IWydanieZewnetrzne wydanieZewnetrzne = _sfera.WydaniaZewnetrzne().UtworzWydanieZewnetrzne())
            {
                wydanieZewnetrzne.WypelnijNaPodstawieDokumentuZaliczkowego(fakturaZaliczkowa.Pozycje, true);
                wydanieZewnetrzne.Przelicz();
                wydanieZaliczkowe = ZapiszWydanieZewnetrzne(wydanieZewnetrzne);
            }
            using (IDokumentSprzedazy fakturaDoWydania = _sfera.DokumentySprzedazy().UtworzFaktureZaliczkowa())
            {
                fakturaDoWydania.WypelnijNaPodstawieWZ(new[] { wydanieZaliczkowe }, wydanieZaliczkowe, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji // nie można wystawiać faktur zaliczkowych do wydań z konsolidacją pozycji
                });
                fakturaDoWydania.Dane.Uwagi = "Dokument fakturuje wydanie zaliczkowe na ilość zaliczkowaną.";
                _ = ZapiszDokumentSprzedazy(fakturaDoWydania);
            }
        }

        /// <summary>
        /// Dodaje zamówienie od klienta na kilka takich samych pozycji z płatnością natychmiastową, realizuje dodane zamówienie do faktury zaliczkowej z przeniesieniem płatności natychmiastowych,
        /// dodaje trzy wydania zewnętrzne do faktury zaliczkowej na ilość przewyższającą ilość zaliczkowaną, a następnie fakturuje wydania bez konsolidacji poprzez fakturę zaliczkową do wydań.
        /// </summary>
        public void FakturowanieWieluWydanZaliczkowych()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("ODEON", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "DZFOREVER", "DZFOREVER", "DZFOREVER", "DZFOREVER", "DZFOREVER", "DZFOREVER", "DZFOREVER", "DZFOREVER");
            Console.WriteLine("Realizacja zamówienia do faktury zaliczkowej.");
            DokumentDS fakturaZaliczkowa;
            DokumentWZ wydanieZaliczkowe1, wydanieZaliczkowe2, wydanieZaliczkowe3;
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureZaliczkowa())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji,
                    PrzeniesNatychmiastowe = PrzenoszeniePlatnosciNatychmiastowych.Przepisz,
                    PrzeniesPrzedplaty = PrzenoszeniePrzedplat.Przepisz
                });
                faktura.Dane.Uwagi = $"Dokument realizuje zamówienie od klienta z płatnością natychmiastową.";
                fakturaZaliczkowa = ZapiszDokumentSprzedazy(faktura);
            }
            using (IWydanieZewnetrzne wydanieZewnetrzne = _sfera.WydaniaZewnetrzne().UtworzWydanieZewnetrzne())
            {
                wydanieZewnetrzne.WypelnijNaPodstawieDokumentuZaliczkowego(fakturaZaliczkowa.Pozycje.Where(p => p.LP == 1).ToList(), true);
                wydanieZewnetrzne.Dane.Pozycje.Single().Ilosc = 1m;
                wydanieZewnetrzne.Przelicz();
                wydanieZewnetrzne.Dane.Uwagi = "Wydanie pozycji zaliczki LP 1";
                wydanieZaliczkowe1 = ZapiszWydanieZewnetrzne(wydanieZewnetrzne);
            }
            using (IWydanieZewnetrzne wydanieZewnetrzne = _sfera.WydaniaZewnetrzne().UtworzWydanieZewnetrzne())
            {
                wydanieZewnetrzne.WypelnijNaPodstawieDokumentuZaliczkowego(fakturaZaliczkowa.Pozycje.Where(p => p.LP == 2).ToList(), true);
                wydanieZewnetrzne.Dane.Pozycje.Single().Ilosc = 1m;
                wydanieZewnetrzne.Przelicz();
                wydanieZewnetrzne.Dane.Uwagi = "Wydanie pozycji zaliczki LP 2";
                wydanieZaliczkowe2 = ZapiszWydanieZewnetrzne(wydanieZewnetrzne);
            }
            using (IWydanieZewnetrzne wydanieZewnetrzne = _sfera.WydaniaZewnetrzne().UtworzWydanieZewnetrzne())
            {
                wydanieZewnetrzne.WypelnijNaPodstawieDokumentuZaliczkowego(fakturaZaliczkowa.Pozycje.Where(p => p.LP == 3).ToList(), true);
                wydanieZewnetrzne.Dane.Pozycje.Single().Ilosc = 1m;
                wydanieZewnetrzne.Przelicz();
                wydanieZewnetrzne.Dane.Uwagi = "Wydanie pozycji zaliczki LP 3";
                wydanieZaliczkowe3 = ZapiszWydanieZewnetrzne(wydanieZewnetrzne);
            }
            using (IDokumentSprzedazy fakturaDoWydan = _sfera.DokumentySprzedazy().UtworzFaktureZaliczkowa())
            {
                fakturaDoWydan.WypelnijNaPodstawieWZ(new[] { wydanieZaliczkowe1, wydanieZaliczkowe2, wydanieZaliczkowe3 }, wydanieZaliczkowe1, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji // nie można wystawiać faktur zaliczkowych do wydań z konsolidacją pozycji
                });
                fakturaDoWydan.Dane.Uwagi = "Dokument fakturuje wiele wydań zaliczkowych.";
                _ = ZapiszDokumentSprzedazy(fakturaDoWydan);
            }
        }


    }
}
