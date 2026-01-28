using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;
using System.Linq;

namespace RealizacjeDokumentow.Przyklady
{
    public class RealizacjaZamowieniaDoDostawcyDoDokumentuZakupu : RealizacjaBase
    {
        public RealizacjaZamowieniaDoDostawcyDoDokumentuZakupu(Uchwyt sfera) : base(sfera) { }

        /// <summary>
        /// Dodaje jedno zamówienie do dostawcy z kilkoma pozycjami i realizuje do faktury VAT zakupu bez konsolidacji pozycji.
        /// </summary>
        public void ZrealizujJednoZamowienieDoFaktury_BezKonsolidacjiPozycji()
        {
            DokumentZD zamowienie = DodajZamowienieDoDostawcy("ADMAR", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            Console.WriteLine("Realizacja zamówienia do faktury zakupu.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                faktura.WypelnijNaPodstawieZD(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDZ()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                faktura.Dane.Uwagi = "Dokument realizuje jedno zamówienie do dostawcy bez konsolidacji pozycji.";
                ZapiszDokumentZakupu(faktura);
            }
        }

        /// <summary>
        /// Dodaje jedno zamówienie do dostawcy z kilkoma pozycjami i realizuje do faktury VAT zakupu z konsolidacją pozycji (konsolidacja w jednostce miary).
        /// </summary>
        public void ZrealizujJednoZamowienieDoFaktury_ZKonsolidacjaPozycji()
        {
            DokumentZD zamowienie = DodajZamowienieDoDostawcy("ALEGRO", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            Console.WriteLine("Realizacja zamówienia do faktury zakupu.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                faktura.WypelnijNaPodstawieZD(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDZ()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiary
                });
                faktura.Dane.Uwagi = "Dokument realizuje jedno zamówienie od klienta z konsolidacją pozycji.";
                ZapiszDokumentZakupu(faktura);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia do dostawcy z kilkomia pozycjami i zbiorczo realizuje je do faktury VAT zakupu bez konsolidacji pozycji.
        /// </summary>
        public void ZrealizujWieleZamowienDoFaktury_BezKonsolidacjiPozycji()
        {
            DokumentZD zamowienie1 = DodajZamowienieDoDostawcy("NOVUM", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZD zamowienie2 = DodajZamowienieDoDostawcy("NOVUM", null, null, "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZD zamowienie3 = DodajZamowienieDoDostawcy("NOVUM", null, null, "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Realizacja zamówień do faktury zakupu.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                faktura.WypelnijNaPodstawieZD(new[] { zamowienie1, zamowienie2, zamowienie3 }, zamowienie1, new ParametryGrupowaniaDZ()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                faktura.Dane.Uwagi = "Dokument realizuje wiele zamówień do dostawcy bez konsolidacji pozycji.";
                ZapiszDokumentZakupu(faktura);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia od klienta z kilkomia pozycjami i zbiorczo realizuje je do faktury VAT sprzedaży z konsolidacją pozycji (konsolidacja bez względu na jednostkę miary).
        /// </summary>
        public void ZrealizujWieleZamowienDoFaktury_ZKonsolidacjaPozycji()
        {
            DokumentZD zamowienie1 = DodajZamowienieDoDostawcy("ODEON", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZD zamowienie2 = DodajZamowienieDoDostawcy("ODEON", null, null, "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZD zamowienie3 = DodajZamowienieDoDostawcy("ODEON", null, null, "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Realizacja zamówień do faktury zakupu.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                faktura.WypelnijNaPodstawieZD(new[] { zamowienie1, zamowienie2, zamowienie3 }, zamowienie1, new ParametryGrupowaniaDZ()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary
                });
                faktura.Dane.Uwagi = "Dokument realizuje wiele zamówień do dostawcy z konsolidacją pozycji.";
                ZapiszDokumentZakupu(faktura);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia do dostawcy z kilkomia pozycjami i zbiorczo realizuje je do faktury zakupu RR z konsolidacją pozycji (konsolidacja bez względu na jednostkę miary).
        /// </summary>
        public void ZrealizujWieleZamowienDoFakturyRR_ZKonsolidacjaPozycji()
        {
            DokumentZD zamowienie1 = DodajZamowienieDoDostawcy("KOZICKO", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZD zamowienie2 = DodajZamowienieDoDostawcy("KOZICKO", null, null, "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZD zamowienie3 = DodajZamowienieDoDostawcy("KOZICKO", null, null, "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Realizacja zamówień do faktury zakupu RR.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupuRR())
            {
                faktura.WypelnijNaPodstawieZD(new[] { zamowienie1, zamowienie2, zamowienie3 }, zamowienie1, new ParametryGrupowaniaDZ()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary
                });
                faktura.Dane.Uwagi = "Dokument realizuje wiele zamówień do dostawcy z konsolidacją pozycji.";
                ZapiszDokumentZakupu(faktura);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia do dostawcy z kilkomia pozycjami i realizuje je częściowo (po jednej pozycji z każdego zamówienia)  do faktury VAT zakupu bez konsolidacji pozycji.
        /// </summary>
        public void ZrealizujCzesciowoWieleZamowienDoFaktury_BezKonsolidacjiPozycji()
        {
            DokumentZD zamowienie1 = DodajZamowienieDoDostawcy("GAMA", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZD zamowienie2 = DodajZamowienieDoDostawcy("GAMA", null, null, "DZSO100", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZD zamowienie3 = DodajZamowienieDoDostawcy("GAMA", null, null, "DZSO100", "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Częściowa realizacja zamówień do faktury zakupu.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                faktura.WypelnijNaPodstawieZD(new[] { zamowienie1.Pozycje.First(), zamowienie2.Pozycje.First(), zamowienie3.Pozycje.First() }, zamowienie1, new ParametryGrupowaniaDZ()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                faktura.Dane.Uwagi = "Dokument realizuje częściowo wiele zamówień do dostawcy bez konsolidacji pozycji.";
                ZapiszDokumentZakupu(faktura);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia do dostawcy z kilkoma pozycjami i realizuje je częściowo (po jednej pozycji z każdego zamówienia) do faktury VAT zakupu z konsolidacją pozycji (konsolidacja bez względu na jednostkę miary).
        /// </summary>
        public void ZrealizujCzesciowoWieleZamowienDoFaktury_ZKonsolidacjaPozycji()
        {
            DokumentZD zamowienie1 = DodajZamowienieDoDostawcy("KOPARSKI", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZD zamowienie2 = DodajZamowienieDoDostawcy("KOPARSKI", null, null, "DZSO100", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZD zamowienie3 = DodajZamowienieDoDostawcy("KOPARSKI", null, null, "DZSO100", "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Częściowa realizacja zamówień do faktury zakupu.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                faktura.WypelnijNaPodstawieZD(new[] { zamowienie1.Pozycje.First(), zamowienie2.Pozycje.First(), zamowienie3.Pozycje.First() }, zamowienie1, new ParametryGrupowaniaDZ()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary
                });
                faktura.Dane.Uwagi = "Dokument realizuje częściowo wiele zamówień do dostawcy z konsolidacją pozycji.";
                ZapiszDokumentZakupu(faktura);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia i realizuje ich pozycje pojedynczo bez przepisywania danych nagłówkowych oraz płatności. Odpowiada to operacji "Pozycje zamówień do realizacji" w oknie dokumentu zakupu.
        /// </summary>
        public void ZrealizujWybranePozycjeZamowien()
        {
            DokumentZD zamowienie1 = DodajZamowienieDoDostawcy("MAGNUM", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZD zamowienie2 = DodajZamowienieDoDostawcy("MAGNUM", null, null, "DZSO100", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZD zamowienie3 = DodajZamowienieDoDostawcy("MAGNUM", null, null, "DZSO100", "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Częściowa realizacja zamówień do faktury zakupu.");
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                // realizacja wybranych pozycji zamówień
                faktura.WypelnijNaPodstawieZD(zamowienie1.Pozycje.ElementAt(0)); // ZK1: DZSO100
                faktura.WypelnijNaPodstawieZD(zamowienie2.Pozycje.ElementAt(1)); // ZK2: WOBLACK100
                faktura.WypelnijNaPodstawieZD(zamowienie3.Pozycje.ElementAt(2)); // ZK3: WOBLACK50
                faktura.WypelnijNaPodstawieZD(zamowienie1.Pozycje.ElementAt(1)); // ZK1: PEWTK50
                faktura.WypelnijNaPodstawieZD(zamowienie2.Pozycje.ElementAt(2)); // ZK2: WOBLACK50
                faktura.WypelnijNaPodstawieZD(zamowienie3.Pozycje.ElementAt(3)); // ZK3: WOBLACK70
                faktura.Dane.Uwagi = "Dokument realizuje częściowo wiele zamówień bez przepisywania danych nagłówkowych oraz płatności.";
                faktura.Przelicz();
                ZapiszDokumentZakupu(faktura);
            }
            // odświeżenie danych zamówień:
            zamowienie1 = _sfera.ZamowieniaDoDostawcow().Dane.Pierwszy(zk => zk.Id == zamowienie1.Id);
            zamowienie2 = _sfera.ZamowieniaDoDostawcow().Dane.Pierwszy(zk => zk.Id == zamowienie2.Id);
            zamowienie3 = _sfera.ZamowieniaDoDostawcow().Dane.Pierwszy(zk => zk.Id == zamowienie3.Id);
            using (IDokumentZakupu faktura = _sfera.DokumentyZakupu().UtworzFaktureZakupu())
            {
                // realizacja pozostałych niezrealizowanych pozycji zamówień:
                foreach (PozycjaDokumentu niezrealizowana in zamowienie1.Pozycje.Concat(zamowienie2.Pozycje).Concat(zamowienie3.Pozycje).Where(p => p.IloscDoRealizacji != null && p.IloscDoRealizacji.PozostalaIlosc > 0m).ToArray())
                {
                    faktura.WypelnijNaPodstawieZD(niezrealizowana);
                }
                faktura.Dane.Uwagi = "Dokument realizuje częściowo wiele zamówień bez przepisywania danych nagłówkowych oraz płatności.";
                faktura.Przelicz();
                ZapiszDokumentZakupu(faktura);
            }
        }       

    }
}
