using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Sfera;
using InsERT.Mox.ObiektyBiznesowe;
using System;
using System.Linq;

namespace RealizacjeDokumentow.Przyklady
{
    public class RealizacjaZamowieniaOdKlientaDoDokumentuSprzedazy : RealizacjaBase
    {
        public RealizacjaZamowieniaOdKlientaDoDokumentuSprzedazy(Uchwyt sfera) : base(sfera) { }

        /// <summary>
        /// Dodaje jedno zamówienie od klienta z kilkoma pozycjami i realizuje do faktury VAT sprzedaży bez konsolidacji pozycji.
        /// </summary>
        public void ZrealizujJednoZamowienieDoFaktury_BezKonsolidacjiPozycji()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("ADMAR", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            Console.WriteLine("Realizacja zamówienia do faktury sprzedaży.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                faktura.Dane.Uwagi = "Dokument realizuje jedno zamówienie od klienta bez konsolidacji pozycji.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodaje jedno zamówienie od klienta z kilkoma pozycjami i realizuje do paragonu bez konsolidacji pozycji.
        /// </summary>
        public void ZrealizujJednoZamowienieDoParagonu_BezKonsolidacjiPozycji()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("KSAVON", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            Console.WriteLine("Realizacja zamówienia do paragonu.");
            using (IDokumentSprzedazy paragon = _sfera.DokumentySprzedazy().UtworzParagon())
            {
                paragon.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                paragon.Dane.Uwagi = "Dokument realizuje jedno zamówienie od klienta bez konsolidacji pozycji.";
                ZapiszDokumentSprzedazy(paragon);
            }
        }

        /// <summary>
        /// Dodaje jedno zamówienie od klienta z kilkoma pozycjami i realizuje do faktury VAT sprzedaży z konsolidacją pozycji (konsolidacja w jednostce miary).
        /// </summary>
        public void ZrealizujJednoZamowienieDoFaktury_ZKonsolidacjaPozycji()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("ALEGRO", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            Console.WriteLine("Realizacja zamówienia do faktury sprzedaży.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiary
                });
                faktura.Dane.Uwagi = "Dokument realizuje jedno zamówienie od klienta z konsolidacją pozycji.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia od klienta z kilkomia pozycjami i zbiorczo realizuje je do faktury VAT sprzedaży bez konsolidacji pozycji.
        /// </summary>
        public void ZrealizujWieleZamowienDoFaktury_BezKonsolidacjiPozycji()
        {
            DokumentZK zamowienie1 = DodajZamowienieOdKlienta("NOVUM", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZK zamowienie2 = DodajZamowienieOdKlienta("NOVUM", null, null, "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZK zamowienie3 = DodajZamowienieOdKlienta("NOVUM", null, null, "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Realizacja zamówień do faktury sprzedaży.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie1, zamowienie2, zamowienie3 }, zamowienie1, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                faktura.Dane.Uwagi = "Dokument realizuje wiele zamówień od klienta bez konsolidacji pozycji.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia od klienta z kilkomia pozycjami i zbiorczo realizuje je do faktury VAT sprzedaży z konsolidacją pozycji (konsolidacja bez względu na jednostkę miary).
        /// </summary>
        public void ZrealizujWieleZamowienDoFaktury_ZKonsolidacjaPozycji()
        {
            DokumentZK zamowienie1 = DodajZamowienieOdKlienta("ODEON", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZK zamowienie2 = DodajZamowienieOdKlienta("ODEON", null, null, "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZK zamowienie3 = DodajZamowienieOdKlienta("ODEON", null, null, "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Realizacja zamówień do faktury sprzedaży.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie1, zamowienie2, zamowienie3 }, zamowienie1, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary
                });
                faktura.Dane.Uwagi = "Dokument realizuje wiele zamówień od klienta z konsolidacją pozycji.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia od klienta z kilkomia pozycjami i zbiorczo realizuje je do paragonu imiennego z konsolidacją pozycji (konsolidacja bez względu na jednostkę miary).
        /// </summary>
        public void ZrealizujWieleZamowienDoParagonuImiennego_ZKonsolidacjaPozycji()
        {
            DokumentZK zamowienie1 = DodajZamowienieOdKlienta("KOZICKO", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZK zamowienie2 = DodajZamowienieOdKlienta("KOZICKO", null, null, "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZK zamowienie3 = DodajZamowienieOdKlienta("KOZICKO", null, null, "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Realizacja zamówień do paragonu imiennego.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzParagonImienny())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie1, zamowienie2, zamowienie3 }, zamowienie1, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary
                });
                faktura.Dane.Uwagi = "Dokument realizuje wiele zamówień od klienta z konsolidacją pozycji.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia od klienta z kilkomia pozycjami i realizuje je częściowo (po jednej pozycji z każdego zamówienia)  do faktury VAT sprzedaży bez konsolidacji pozycji.
        /// </summary>
        public void ZrealizujCzesciowoWieleZamowienDoFaktury_BezKonsolidacjiPozycji()
        {
            DokumentZK zamowienie1 = DodajZamowienieOdKlienta("GAMA", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZK zamowienie2 = DodajZamowienieOdKlienta("GAMA", null, null, "DZSO100", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZK zamowienie3 = DodajZamowienieOdKlienta("GAMA", null, null, "DZSO100", "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Częściowa realizacja zamówień do faktury sprzedaży.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie1.Pozycje.First(), zamowienie2.Pozycje.First(), zamowienie3.Pozycje.First() }, zamowienie1, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                faktura.Dane.Uwagi = "Dokument realizuje częściowo wiele zamówień od klienta bez konsolidacji pozycji.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia od klienta z kilkomia pozycjami i realizuje je częściowo (po jednej pozycji z każdego zamówienia) do faktury VAT sprzedaży z konsolidacją pozycji (konsolidacja bez względu na jednostkę miary).
        /// </summary>
        public void ZrealizujCzesciowoWieleZamowienDoFaktury_ZKonsolidacjaPozycji()
        {
            DokumentZK zamowienie1 = DodajZamowienieOdKlienta("KOPARSKI", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZK zamowienie2 = DodajZamowienieOdKlienta("KOPARSKI", null, null, "DZSO100", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZK zamowienie3 = DodajZamowienieOdKlienta("KOPARSKI", null, null, "DZSO100", "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Częściowa realizacja zamówień do faktury sprzedaży.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie1.Pozycje.First(), zamowienie2.Pozycje.First(), zamowienie3.Pozycje.First() }, zamowienie1, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary
                });
                faktura.Dane.Uwagi = "Dokument realizuje częściowo wiele zamówień od klienta z konsolidacją pozycji.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodaje trzy zamówienia i realizuje ich pozycje pojedynczo bez przepisywania danych nagłówkowych oraz płatności. Odpowiada to operacji "Pozycje zamówień do realizacji" w oknie dokumentu sprzedaży.
        /// </summary>
        public void ZrealizujWybranePozycjeZamowien()
        {
            DokumentZK zamowienie1 = DodajZamowienieOdKlienta("MAGNUM", null, null, "DZSO100", "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZK zamowienie2 = DodajZamowienieOdKlienta("MAGNUM", null, null, "DZSO100", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZK zamowienie3 = DodajZamowienieOdKlienta("MAGNUM", null, null, "DZSO100", "ZELALGI", "WOBLACK50", "WOBLACK70", "DZSO100");
            Console.WriteLine("Częściowa realizacja zamówień do faktury sprzedaży.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                // realizacja wybranych pozycji zamówień
                faktura.WypelnijNaPodstawieZK(zamowienie1.Pozycje.ElementAt(0)); // ZK1: DZSO100
                faktura.WypelnijNaPodstawieZK(zamowienie2.Pozycje.ElementAt(1)); // ZK2: WOBLACK100
                faktura.WypelnijNaPodstawieZK(zamowienie3.Pozycje.ElementAt(2)); // ZK3: WOBLACK50
                faktura.WypelnijNaPodstawieZK(zamowienie1.Pozycje.ElementAt(1)); // ZK1: PEWTK50
                faktura.WypelnijNaPodstawieZK(zamowienie2.Pozycje.ElementAt(2)); // ZK2: WOBLACK50
                faktura.WypelnijNaPodstawieZK(zamowienie3.Pozycje.ElementAt(3)); // ZK3: WOBLACK70
                faktura.Dane.Uwagi = "Dokument realizuje częściowo wiele zamówień bez przepisywania danych nagłówkowych oraz płatności.";
                faktura.Przelicz();
                ZapiszDokumentSprzedazy(faktura);
            }
            // odświeżenie danych zamówień:
            zamowienie1 = _sfera.ZamowieniaOdKlientow().Dane.Pierwszy(zk => zk.Id == zamowienie1.Id);
            zamowienie2 = _sfera.ZamowieniaOdKlientow().Dane.Pierwszy(zk => zk.Id == zamowienie2.Id);
            zamowienie3 = _sfera.ZamowieniaOdKlientow().Dane.Pierwszy(zk => zk.Id == zamowienie3.Id);
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                // realizacja pozostałych niezrealizowanych pozycji zamówień:
                foreach (PozycjaDokumentu niezrealizowana in zamowienie1.Pozycje.Concat(zamowienie2.Pozycje).Concat(zamowienie3.Pozycje).Where(p => p.StanRealizacjiZamowienia.ProcentowyStanRealizacji < 1m).ToArray())
                {
                    faktura.WypelnijNaPodstawieZK(niezrealizowana);
                }
                faktura.Dane.Uwagi = "Dokument realizuje częściowo wiele zamówień bez przepisywania danych nagłówkowych oraz płatności.";
                faktura.Przelicz();
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Ustawia dla wybranego klienta domyślną płatność odroczoną na 100%.
        /// Dodaje takie samo zamówienie z płatnością natychmiastowo w dwóch wersjach - z obsługą (generujące operacje finansowe) i bez obsługi płatności (bez generowania operacji finansowych).
        /// W przypadku realizacji zamówienia z obsługą płatności i włączonego przenoszenia płatności - płatności natychmiastowe przenoszą się do faktury jako przedpłaty.
        /// W przypadku realizacji zamówienia bez obsługi płatności i włączonego przenoszenia płatności - płatności natychmiastowe zamówienia przenoszą się do faktury jako płatności natychmiastowe.
        /// <see cref="ZrealizujZamowienieZPrzenoszeniemPlatnosciNatychmiastowych"/>
        /// <see cref="ZrealizujZamowienieBezPrzenoszeniaPlatnosciNatychmiastowych"/>
        /// </summary>
        public void RealizacjaZamowieniaZPlatnosciaNatychmiastowaDoFakturySprzedazy()
        {
            Ustaw100ProcentPlatnosciOdroczonejKlienta("PRUSK");
            ZmienObslugePlatnosci(KonfiguracjeDokumentow.ZamowienieOdKlienta_ID, true);
            ZrealizujZamowienieZPrzenoszeniemPlatnosciNatychmiastowych();
            ZrealizujZamowienieBezPrzenoszeniaPlatnosciNatychmiastowych();
            ZmienObslugePlatnosci(KonfiguracjeDokumentow.ZamowienieOdKlienta_ID, false);
            ZrealizujZamowienieZPrzenoszeniemPlatnosciNatychmiastowych();
            ZrealizujZamowienieBezPrzenoszeniaPlatnosciNatychmiastowych();
            ZmienObslugePlatnosci(KonfiguracjeDokumentow.ZamowienieOdKlienta_ID, true);
        }

        /// <summary>
        /// Tworzy zamówienie z płatnością natychmiastową (forma płatności - gotówka) z kilkoma pozycjami i realizuje je do faktury sprzedaży z przenoszeniem płatności natychmiastowych.
        /// W przypadku gdy zamówienie ma włączoną obsługę płatności (skutek finansowy) to płatności z zamówienia przeniosą się jako przedpłaty.
        /// W przypadku gdy zamówienie ma wyłączoną obsługę płatności (brak skutku finansowego) to płatności z zamówienia przeniosą się jako płatności natychmiastowe.
        /// Kwota pozostała do rozliczenia zostanie obsłużona poprzez płatności domyślne (dla klienta / stanowiska kasowego).
        /// </summary>
        public void ZrealizujZamowienieZPrzenoszeniemPlatnosciNatychmiastowych()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("PRUSK", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "POYAR01", "POYAR02", "POYAR03");
            Console.WriteLine("Realizacja zamówienia z przenoszeniem płatności natychmiastowej.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    PrzeniesNatychmiastowe = PrzenoszeniePlatnosciNatychmiastowych.Przepisz
                });
                faktura.Dane.Uwagi = $"Dokument realizuje jedno zamówienie od klienta ({(zamowienie.PosiadaAspektFinansowy.GetValueOrDefault(false) ? "z obsługą płatności" : "bez obsługi płatności")}) z przepisywaniem płatności natychmiastowych.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Tworzy zamówienie z płatnością natychmiastową (forma płatności - gotówka) z kilkoma pozycjami i realizuje je do faktury sprzedaży bez przenoszenia płatności natychmiastowych.
        /// Niezależnie od tego czy zamówienie ma włączoną obsługę płatności (skutek finansowy) czy nie faktura zostanie wystawiona z płatnościami domyślnymi (dla klienta / stanowiska kasowego).
        /// </summary>
        public void ZrealizujZamowienieBezPrzenoszeniaPlatnosciNatychmiastowych()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("PRUSK", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "POYAR01", "POYAR02", "POYAR03");
            Console.WriteLine("Realizacja zamówienia bez przenoszenia płatności natychmiastowej.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    PrzeniesNatychmiastowe = PrzenoszeniePlatnosciNatychmiastowych.Brak
                });
                faktura.Dane.Uwagi = $"Dokument realizuje jedno zamówienie od klienta ({(zamowienie.PosiadaAspektFinansowy.GetValueOrDefault(false) ? "z obsługą płatności" : "bez obsługi płatności")}) bez przepisywania płatności natychmiastowych.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }


        /// <summary>
        /// Ustawia dla wybranego klienta domyślną płatność odroczoną na 100%.
        /// Dodaje takie samo zamówienie z przedpłatą bankową w dwóch wersjach - z obsługą (generujące operacje finansowe) i bez obsługi płatności (bez generowania operacji finansowych).
        /// Bez względu na obsługę płatności na zamówieniu (skutek finansowy) przedpłaty przenoszą się do dokumentu sprzedaży jako przedpłaty (o ile ustawione to zostanie w parametrach grupowania).
        /// <see cref="ZrealizujZamowienieZPrzedplataZPrzenoszeniemPrzedplat"/>
        /// <see cref="ZrealizujZamowienieZPrzedplataBezPrzenoszeniaPrzedplat"/>
        /// </summary>
        public void RealizacjaZamowieniaZPrzedplataDoFakturySprzedazy()
        {
            Ustaw100ProcentPlatnosciOdroczonejKlienta("DOMOSAD");
            ZmienObslugePlatnosci(KonfiguracjeDokumentow.ZamowienieOdKlienta_ID, true);
            ZrealizujZamowienieZPrzedplataZPrzenoszeniemPrzedplat();
            ZrealizujZamowienieZPrzedplataBezPrzenoszeniaPrzedplat();
            ZmienObslugePlatnosci(KonfiguracjeDokumentow.ZamowienieOdKlienta_ID, false);
            ZrealizujZamowienieZPrzedplataZPrzenoszeniemPrzedplat();
            ZrealizujZamowienieZPrzedplataBezPrzenoszeniaPrzedplat();
            ZmienObslugePlatnosci(KonfiguracjeDokumentow.ZamowienieOdKlienta_ID, true);
        }

        /// <summary>
        /// Tworzy zamówienie z przedpłatą bankową. Bez względu na obsługę płatności (skutek finansowy) na zamówieniu przedpłata przenosi się na fakturę.
        /// </summary>
        public void ZrealizujZamowienieZPrzedplataZPrzenoszeniemPrzedplat()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("DOMOSAD", DodajPrzedplateBankowa("DOMOSAD"), null, "POYAR01", "POYAR02", "POYAR03", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            Console.WriteLine("Realizacja zamówienia z przenoszeniem przedpłaty.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    PrzeniesPrzedplaty = PrzenoszeniePrzedplat.Przepisz
                });
                faktura.Dane.Uwagi = $"Dokument realizuje jedno zamówienie od klienta ({(zamowienie.PosiadaAspektFinansowy.GetValueOrDefault(false) ? "z obsługą płatności" : "bez obsługi płatności")}) z przepisywaniem przedpłat.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Tworzy zamówienie z przedpłatą bankową. Przedpłata nie przenosi się na fakturę.
        /// </summary>
        public void ZrealizujZamowienieZPrzedplataBezPrzenoszeniaPrzedplat()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("DOMOSAD", DodajPrzedplateBankowa("DOMOSAD"), null, "POYAR01", "POYAR02", "POYAR03", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            Console.WriteLine("Realizacja zamówienia bez przenoszenia przedpłaty.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    PrzeniesPrzedplaty = PrzenoszeniePrzedplat.Brak
                });
                faktura.Dane.Uwagi = $"Dokument realizuje jedno zamówienie od klienta ({(zamowienie.PosiadaAspektFinansowy.GetValueOrDefault(false) ? "z obsługą płatności" : "bez obsługi płatności")}) bez przepisywania przedpłat.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Tworzy dwa zamówienia (jedno z przedpłatą bankową, drugie z płatnością natychmiastową) i realizuje je do faktury sprzedaży z przenoszeniem obu rodzajów płatności oraz konsolidacją pozycji w jednostce miary.
        /// </summary>
        public void ZrealizujZamowieniaZPrzenoszeniemPlatnosciIKonsolidacja()
        {
            DokumentZK zamowienieZPrzedplata = DodajZamowienieOdKlienta("SKLEP1", DodajPrzedplateBankowa("SKLEP1"), null, "POYAR01", "POYAR02", "POYAR03", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZK zamowienieZPlatnosciaNatychmiastowa = DodajZamowienieOdKlienta("SKLEP1", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "POYAR01", "POYAR02", "POYAR03", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            Console.WriteLine("Realizacja zamówienia z przenoszeniem płatności natychmiastowych i przedpłat.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienieZPrzedplata, zamowienieZPlatnosciaNatychmiastowa }, zamowienieZPrzedplata, new ParametryGrupowaniaDS()
                {
                    PrzeniesPrzedplaty = PrzenoszeniePrzedplat.Przepisz,
                    PrzeniesNatychmiastowe = PrzenoszeniePlatnosciNatychmiastowych.Przepisz,
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiary
                });
                faktura.Dane.Uwagi = $"Dokument realizuje dwa zamówienia od klienta (jedno z przedpłatą, drugie z płatnością natychmiastową) z przepisywaniem płatności i konsolidacją pozycji.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Tworzy dwa zamówienia (jedno z przedpłatą bankową, drugie z płatnością natychmiastową) i realizuje je do faktury zaliczkowej z przenoszeniem obu rodzajów płatności oraz konsolidacją pozycji w jednostce miary.
        /// W przypadku gdy zamówienie ma skutek finansowy to oba rodzaje płatności (przedpłata i płatność natychmiastowa) przenosi się do faktury zaliczkowej jako przedpłata.
        /// W przypadku gdy zamówienie nie ma skutu finansowego to przenosi się tylko przedpłata (płatności natychmiastowe bez skutku finansowego nie są przenoszone).
        /// </summary>
        public void ZrealizujZamowieniaZPlatnosciamiDoFakturyZaliczkowej()
        {
            DokumentZK zamowienieZPrzedplata = DodajZamowienieOdKlienta("GROM", DodajPrzedplateBankowa("GROM"), null, "POYAR01", "POYAR02", "POYAR03", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            DokumentZK zamowienieZPlatnosciaNatychmiastowa = DodajZamowienieOdKlienta("GROM", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "POYAR01", "POYAR02", "POYAR03", "WOBLACK100", "WOBLACK50", "WOBLACK70");
            Console.WriteLine("Realizacja zamówienia do faktury zaliczkowej z przenoszeniem płatności natychmiastowych i przedpłat.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureZaliczkowa())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienieZPrzedplata, zamowienieZPlatnosciaNatychmiastowa }, zamowienieZPrzedplata, new ParametryGrupowaniaDS()
                {
                    PrzeniesPrzedplaty = PrzenoszeniePrzedplat.Przepisz,
                    PrzeniesNatychmiastowe = PrzenoszeniePlatnosciNatychmiastowych.Przepisz,
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiary
                });
                faktura.Dane.Uwagi = $"Dokument realizuje dwa zamówienia od klienta (jedno z przedpłatą, drugie z płatnością natychmiastową) z przepisywaniem płatności i konsolidacją pozycji.";
                ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Tworzy zamówienie bez określania płatności i realizuje je do faktury zaliczkowej, na której zaliczka jest określana ręcznie (poprzez dodanie płatności, wpisanie na niej kwoty płatności co powoduje automatyczne rozpisanie tej kwoty na pozycje.
        /// </summary>
        public void ZrealizujZamowienieBezPlatnosciDoFakturyZaliczkowej()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("SUN", null, null, "DZSO100", "PEFLEUR15", "POYAR01", "PUYAR07", "WOPINKLACE");
            Console.WriteLine("Realizacja zamówienia do faktury zaliczkowej.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureZaliczkowa())
            {
                faktura.Platnosci.TrybPrzeliczania |= TrybPrzeliczaniaPlatnosci.PrzenoszeniePlatnosciNaZaliczke; // bez tego kwota z płatności nie będzie się przenosiła na zaliczkę
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS());
                faktura.Dane.Uwagi = $"Dokument realizuje zamówienie od klienta do faktury zaliczkowej z ręcznym ustawieniem kwoty zaliczki.";

                PlatnoscDokumentu platnosc = faktura.Platnosci.DodajPlatnoscNatychmiastowa(_sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN);
                // po ustawieniu kwoty płatności zostaje ona potraktowana jako zaliczka i rozpisana wg domyślnego algorytmu rozpisywania zaliczki na pozycje
                platnosc.KwotaDokumentu = 200m;

                ZapiszDokumentSprzedazy(faktura);
            }
        }

    }
}
