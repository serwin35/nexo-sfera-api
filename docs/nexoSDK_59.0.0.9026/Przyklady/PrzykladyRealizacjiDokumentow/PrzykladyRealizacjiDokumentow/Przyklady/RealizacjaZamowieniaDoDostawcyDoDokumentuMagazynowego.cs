using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;
using System.Linq;

namespace RealizacjeDokumentow.Przyklady
{
    public class RealizacjaZamowieniaDoDostawcyDoDokumentuMagazynowego : RealizacjaBase
    {
        public RealizacjaZamowieniaDoDostawcyDoDokumentuMagazynowego(Uchwyt sfera) : base(sfera) { }

        /// <summary>
        /// Dodaje jedno zamówienie do dostawcy z kilkoma pozycjami i realizuje do przyjęcia zewnętrznego bez konsolidacji pozycji.
        /// </summary>
        public void ZrealizujJednoZamowienieDoPrzyjecia_BezKonsolidacjiPozycji()
        {
            DokumentZD zamowienie = DodajZamowienieDoDostawcy("ADMAR", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            Console.WriteLine("Realizacja zamówienia do przyjęcia zewnętrznego.");
            using (IPrzyjecieZewnetrzne przyjecie = _sfera.PrzyjeciaZewnetrzne().UtworzPrzyjecieZewnetrzne())
            {
                przyjecie.WypelnijNaPodstawieZD(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaPodstawowe()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                przyjecie.Dane.Uwagi = "Dokument realizuje jedno zamówienie do dostawcy bez konsolidacji pozycji.";
                ZapiszPrzyjecieZewnetrzne(przyjecie);
            }
        }


        /// <summary>
        /// Dodaje trzy zamówienia od klienta z kilkomia pozycjami i zbiorczo realizuje je do wydania zewnętrznego z konsolidacją pozycji (konsolidacja bez względu na jednostkę miary).
        /// </summary>
        public void ZrealizujWieleZamowienDoPrzyjecia_ZKonsolidacjaPozycji()
        {
            DokumentZD zamowienie1 = DodajZamowienieDoDostawcy("ODEON", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZD zamowienie2 = DodajZamowienieDoDostawcy("ODEON", null, null, "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZD zamowienie3 = DodajZamowienieDoDostawcy("ODEON", null, null, "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Realizacja zamówień do przyjęcia zewnętrznego.");
            using (IPrzyjecieZewnetrzne przyjecie = _sfera.PrzyjeciaZewnetrzne().UtworzPrzyjecieZewnetrzne())
            {
                przyjecie.WypelnijNaPodstawieZD(new[] { zamowienie1, zamowienie2, zamowienie3 }, zamowienie1, new ParametryGrupowaniaPodstawowe()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary
                });
                przyjecie.Dane.Uwagi = "Dokument realizuje wiele zamówień do dostawcy z konsolidacją pozycji.";
                ZapiszPrzyjecieZewnetrzne(przyjecie);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia i realizuje ich pozycje pojedynczo bez przepisywania danych nagłówkowych oraz płatności. Odpowiada to operacji "Pozycje zleceń do realizacji" w oknie przyjęcia zewnętrznego.
        /// </summary>
        public void ZrealizujWybranePozycjeZamowien()
        {
            DokumentZD zamowienie1 = DodajZamowienieDoDostawcy("MAGNUM", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZD zamowienie2 = DodajZamowienieDoDostawcy("MAGNUM", null, null, "DZSO100", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZD zamowienie3 = DodajZamowienieDoDostawcy("MAGNUM", null, null, "DZSO100", "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Częściowa realizacja zamówień do przyjęcia zewnętrznego.");
            using (IPrzyjecieZewnetrzne przyjecie = _sfera.PrzyjeciaZewnetrzne().UtworzPrzyjecieZewnetrzne())
            {
                // realizacja wybranych pozycji zamówień
                przyjecie.WypelnijNaPodstawieZD(zamowienie1.Pozycje.ElementAt(0)); // ZK1: DZSO100
                przyjecie.WypelnijNaPodstawieZD(zamowienie2.Pozycje.ElementAt(1)); // ZK2: WOBLACK100
                przyjecie.WypelnijNaPodstawieZD(zamowienie3.Pozycje.ElementAt(2)); // ZK3: WOBLACK50
                przyjecie.WypelnijNaPodstawieZD(zamowienie1.Pozycje.ElementAt(1)); // ZK1: PEWTK50
                przyjecie.WypelnijNaPodstawieZD(zamowienie2.Pozycje.ElementAt(2)); // ZK2: WOBLACK50
                przyjecie.WypelnijNaPodstawieZD(zamowienie3.Pozycje.ElementAt(3)); // ZK3: WOBLACK70
                przyjecie.Dane.Uwagi = "Dokument realizuje częściowo wiele zamówień bez przepisywania danych nagłówkowych oraz płatności.";
                przyjecie.Przelicz();
                ZapiszPrzyjecieZewnetrzne(przyjecie);
            }
            // odświeżenie danych zamówień:
            zamowienie1 = _sfera.ZamowieniaDoDostawcow().Dane.Pierwszy(zk => zk.Id == zamowienie1.Id);
            zamowienie2 = _sfera.ZamowieniaDoDostawcow().Dane.Pierwszy(zk => zk.Id == zamowienie2.Id);
            zamowienie3 = _sfera.ZamowieniaDoDostawcow().Dane.Pierwszy(zk => zk.Id == zamowienie3.Id);
            using (IPrzyjecieZewnetrzne przyjecie = _sfera.PrzyjeciaZewnetrzne().UtworzPrzyjecieZewnetrzne())
            {
                // realizacja pozostałych niezrealizowanych pozycji zamówień:
                foreach (PozycjaDokumentu niezrealizowana in zamowienie1.Pozycje.Concat(zamowienie2.Pozycje).Concat(zamowienie3.Pozycje).Where(p => p.IloscDoRealizacji != null && p.IloscDoRealizacji.PozostalaIlosc > 0m).ToArray())
                {
                    przyjecie.WypelnijNaPodstawieZD(niezrealizowana);
                }
                przyjecie.Dane.Uwagi = "Dokument realizuje częściowo wiele zamówień bez przepisywania danych nagłówkowych oraz płatności.";
                przyjecie.Przelicz();
                ZapiszPrzyjecieZewnetrzne(przyjecie);
            }
        }

        /// <summary>
        /// Dodaje zamówienie i realizuje je dokumentem przychodu wewnętrznego.
        /// </summary>
        public void ZrealizujJednoZamowienieDoPrzychoduWewnetrznego_BezKonsolidacji()
        {
            DokumentZD zamowienie = DodajZamowienieDoDostawcy("NOVUM", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            Console.WriteLine("Realizacja zamówienia do przychodu wewnętrznego.");
            using (IPrzychodWewnetrzny przychod = _sfera.PrzychodyWewnetrzne().Utworz())
            {
                przychod.WypelnijNaPodstawieZD(zamowienie.Pozycje, zamowienie, new ParametryGrupowaniaPodstawowe()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                przychod.Dane.Uwagi = "Dokument realizuje jedno zamówienie od klienta bez konsolidacji pozycji.";
                ZapiszPrzychodWewnetrzny(przychod);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia i realizuje je dokumentem przychodu wewnętrznego z konsolidacją pozycji.
        /// </summary>
        public void ZrealizujWieleZamowienDoPrzychoduWewnetrznego_ZKonsolidacja()
        {
            DokumentZD zamowienie1 = DodajZamowienieDoDostawcy("ADMAR", null, null, "DZSO100", "PEWTK50", "PUYAR07", "PEWTK50");
            DokumentZD zamowienie2 = DodajZamowienieDoDostawcy("ADMAR", null, null, "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZD zamowienie3 = DodajZamowienieDoDostawcy("ADMAR", null, null, "DZSO100", "PEWTK50", "DZSO100", "PEWTK50");
            Console.WriteLine("Realizacja zamówień do przychodu wewnętrznego.");
            using (IPrzychodWewnetrzny przychod = _sfera.PrzychodyWewnetrzne().Utworz())
            {
                przychod.WypelnijNaPodstawieZD(zamowienie1.Pozycje.Concat(zamowienie2.Pozycje).Concat(zamowienie3.Pozycje), zamowienie2, new ParametryGrupowaniaPodstawowe()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary
                });
                przychod.Dane.Uwagi = "Dokument realizuje wiele zamówień do dostawców z konsolidacją pozycji bez względu na jednostkę miary.";
                ZapiszPrzychodWewnetrzny(przychod);
            }
        }

    }
}
