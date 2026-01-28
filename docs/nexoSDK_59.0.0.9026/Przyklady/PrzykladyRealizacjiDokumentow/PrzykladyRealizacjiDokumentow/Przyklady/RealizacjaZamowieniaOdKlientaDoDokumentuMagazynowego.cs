using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;
using System.Linq;

namespace RealizacjeDokumentow.Przyklady
{
    public class RealizacjaZamowieniaOdKlientaDoDokumentuMagazynowego : RealizacjaBase
    {
        public RealizacjaZamowieniaOdKlientaDoDokumentuMagazynowego(Uchwyt sfera) : base(sfera) { }

        /// <summary>
        /// Dodaje jedno zamówienie od klienta z kilkoma pozycjami i realizuje do wydania zewnętrznego bez konsolidacji pozycji.
        /// </summary>
        public void ZrealizujJednoZamowienieDoWydania_BezKonsolidacjiPozycji()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("ADMAR", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            Console.WriteLine("Realizacja zamówienia do wydania zewnętrznego.");
            using (IWydanieZewnetrzne wydanie = _sfera.WydaniaZewnetrzne().UtworzWydanieZewnetrzne())
            {
                wydanie.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaPodstawowe()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                wydanie.Dane.Uwagi = "Dokument realizuje jedno zamówienie od klienta bez konsolidacji pozycji.";
                ZapiszWydanieZewnetrzne(wydanie);
            }
        }


        /// <summary>
        /// Dodaje trzy zamówienia od klienta z kilkomia pozycjami i zbiorczo realizuje je do wydania zewnętrznego z konsolidacją pozycji (konsolidacja bez względu na jednostkę miary).
        /// </summary>
        public void ZrealizujWieleZamowienDoWydania_ZKonsolidacjaPozycji()
        {
            DokumentZK zamowienie1 = DodajZamowienieOdKlienta("ODEON", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZK zamowienie2 = DodajZamowienieOdKlienta("ODEON", null, null, "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZK zamowienie3 = DodajZamowienieOdKlienta("ODEON", null, null, "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Realizacja zamówień do wydania zewnętrznego.");
            using (IWydanieZewnetrzne wydanie = _sfera.WydaniaZewnetrzne().UtworzWydanieZewnetrzne())
            {
                wydanie.WypelnijNaPodstawieZK(new[] { zamowienie1, zamowienie2, zamowienie3 }, zamowienie1, new ParametryGrupowaniaPodstawowe()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary
                });
                wydanie.Dane.Uwagi = "Dokument realizuje wiele zamówień od klienta z konsolidacją pozycji.";
                ZapiszWydanieZewnetrzne(wydanie);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia i realizuje ich pozycje pojedynczo bez przepisywania danych nagłówkowych oraz płatności. Odpowiada to operacji "Pozycje zamówień do realizacji" w oknie wydania zewnętrznego.
        /// </summary>
        public void ZrealizujWybranePozycjeZamowien()
        {
            DokumentZK zamowienie1 = DodajZamowienieOdKlienta("MAGNUM", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZK zamowienie2 = DodajZamowienieOdKlienta("MAGNUM", null, null, "DZSO100", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZK zamowienie3 = DodajZamowienieOdKlienta("MAGNUM", null, null, "DZSO100", "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Częściowa realizacja zamówień do wydania zewnętrznego.");
            using (IWydanieZewnetrzne wydanie = _sfera.WydaniaZewnetrzne().UtworzWydanieZewnetrzne())
            {
                // realizacja wybranych pozycji zamówień
                wydanie.WypelnijNaPodstawieZK(zamowienie1.Pozycje.ElementAt(0)); // ZK1: DZSO100
                wydanie.WypelnijNaPodstawieZK(zamowienie2.Pozycje.ElementAt(1)); // ZK2: WOBLACK100
                wydanie.WypelnijNaPodstawieZK(zamowienie3.Pozycje.ElementAt(2)); // ZK3: WOBLACK50
                wydanie.WypelnijNaPodstawieZK(zamowienie1.Pozycje.ElementAt(1)); // ZK1: PEWTK50
                wydanie.WypelnijNaPodstawieZK(zamowienie2.Pozycje.ElementAt(2)); // ZK2: WOBLACK50
                wydanie.WypelnijNaPodstawieZK(zamowienie3.Pozycje.ElementAt(3)); // ZK3: WOBLACK70
                wydanie.Dane.Uwagi = "Dokument realizuje częściowo wiele zamówień bez przepisywania danych nagłówkowych oraz płatności.";
                wydanie.Przelicz();
                ZapiszWydanieZewnetrzne(wydanie);
            }
            // odświeżenie danych zamówień:
            zamowienie1 = _sfera.ZamowieniaOdKlientow().Dane.Pierwszy(zk => zk.Id == zamowienie1.Id);
            zamowienie2 = _sfera.ZamowieniaOdKlientow().Dane.Pierwszy(zk => zk.Id == zamowienie2.Id);
            zamowienie3 = _sfera.ZamowieniaOdKlientow().Dane.Pierwszy(zk => zk.Id == zamowienie3.Id);
            using (IWydanieZewnetrzne wydanie = _sfera.WydaniaZewnetrzne().UtworzWydanieZewnetrzne())
            {
                // realizacja pozostałych niezrealizowanych pozycji zamówień:
                foreach (PozycjaDokumentu niezrealizowana in zamowienie1.Pozycje.Concat(zamowienie2.Pozycje).Concat(zamowienie3.Pozycje).Where(p => p.StanRealizacjiZamowienia.ProcentowyStanRealizacji < 1m).ToArray())
                {
                    wydanie.WypelnijNaPodstawieZK(niezrealizowana);
                }
                wydanie.Dane.Uwagi = "Dokument realizuje częściowo wiele zamówień bez przepisywania danych nagłówkowych oraz płatności.";
                wydanie.Przelicz();
                ZapiszWydanieZewnetrzne(wydanie);
            }
        }

        /// <summary>
        /// Dodaje zamówienie i realizuje je dokumentem rozchodu wewnętrznego.
        /// </summary>
        public void ZrealizujJednoZamowienieDoRozchoduWewnetrznego_BezKonsolidacji()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("NOVUM", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            Console.WriteLine("Realizacja zamówienia do rozchodu wewnętrznego.");
            using (IRozchodWewnetrzny rozchod = _sfera.RozchodyWewnetrzne().Utworz())
            {
                rozchod.WypelnijNaPodstawieZK(zamowienie.Pozycje, zamowienie, new ParametryGrupowaniaPodstawowe()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                rozchod.Dane.Uwagi = "Dokument realizuje jedno zamówienie od klienta bez konsolidacji pozycji.";
                ZapiszRozchodWewnetrzny(rozchod);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia i zbiorczo realizuje je dokumentem rozchodu wewnętrznego z konsolidacją pozycji.
        /// </summary>
        public void ZrealizujWieleZamowienDoRozchoduWewnetrznego_ZKonsolidacja()
        {
            DokumentZK zamowienie1 = DodajZamowienieOdKlienta("ADMAR", null, null, "DZSO100", "PEWTK50", "PUYAR07", "PEWTK50");
            DokumentZK zamowienie2 = DodajZamowienieOdKlienta("ADMAR", null, null, "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZK zamowienie3 = DodajZamowienieOdKlienta("ADMAR", null, null, "DZSO100", "PEWTK50", "DZSO100", "PEWTK50");
            Console.WriteLine("Realizacja zamówień do rozchodu wewnętrznego.");
            using (IRozchodWewnetrzny rozchod = _sfera.RozchodyWewnetrzne().Utworz())
            {
                rozchod.WypelnijNaPodstawieZK(zamowienie1.Pozycje.Concat(zamowienie2.Pozycje).Concat(zamowienie3.Pozycje), zamowienie2, new ParametryGrupowaniaPodstawowe()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary
                });
                rozchod.Dane.Uwagi = "Dokument realizuje wiele zamówień od klienta z konsolidacją pozycji bez względu na jednostkę miary.";
                ZapiszRozchodWewnetrzny(rozchod);
            }
        }

    }
}
