using InsERT.Moria.Asortymenty;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System.Linq;

namespace RealizacjeDokumentow.Przyklady
{
    public class GenerowanieZamowienDoDostawcow : RealizacjaBase
    {
        public GenerowanieZamowienDoDostawcow(Uchwyt sfera) : base(sfera) { }

        /// <summary>
        /// Przetwarza zamówienia od klientów w zamówienia do dostawców przy pomocy kreatora zamówień do dostawców.
        /// </summary>
        public void GenerujZamowieniaDoDostawcowNaPodstawieZamowienOdKlientow()
        {
            DokumentZK zamowienie1 = DodajZamowienieOdKlienta("KOPARSKI", null, null, "DZSO100", "PEWTK50", "PUYAR07", "PEWTK50");
            DokumentZK zamowienie2 = DodajZamowienieOdKlienta("MAGNUM", null, null, "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZK zamowienie3 = DodajZamowienieOdKlienta("PRUSK", null, null, "DZSO100", "PEWTK50", "DZSO100", "PEWTK50");

            IPodmioty podmioty = _sfera.Podmioty();
            Podmiot hurtowaniaAla = podmioty.Dane.Pierwszy(p => p.Sygnatura.PelnaSygnatura == "ALA");
            Podmiot hurtowaniaErie = podmioty.Dane.Pierwszy(p => p.Sygnatura.PelnaSygnatura == "ERIE");
            Podmiot hurtowaniaOlek = podmioty.Dane.Pierwszy(p => p.Sygnatura.PelnaSygnatura == "OLEK");
            _sfera.Kontekst().UstawMagazyn(_sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == "MAG"));
            _sfera.Kontekst().UstawOddzial(_sfera.Centrale().Dane.Wszystkie().FirstOrDefault());
            IKreatoryZamowienDoDostawcow kreatoryZamowien = _sfera.KreatoryZamowienDoDostawcow();
            using (IKreatorZamowienDoDostawcow kreatorZamowien = kreatoryZamowien.UtworzNaPodstawiePozycjiZamowionych(new Dokument[] { zamowienie1, zamowienie2, zamowienie3 }
                , new ParametryPrzetwarzaniaZKWZD()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiary,
                    UwzglednijMontaz = true,
                    UwzglednijRealizacje = true
                }))
            {
                kreatorZamowien.MenedzerZapotrzebowania.DaneDokumentu.Uwagi = "Przetwarzanie zamówień na zlecenie do dostawcy.";
                foreach (IPozycjaMenedzeraZapotrzebowania pozycja in kreatorZamowien.MenedzerZapotrzebowania.Pozycje)
                {
                    Podmiot dostawca;
                    switch (pozycja.Asortyment.Symbol)
                    {
                        case "DZSO100":
                        case "PEWTK50":
                            dostawca = hurtowaniaAla;
                            break;
                        case "PUYAR07":
                            dostawca = hurtowaniaErie;
                            break;
                        default:
                            dostawca = hurtowaniaOlek;
                            break;
                    }
                    pozycja.Dostawca = dostawca.Aktualny;
                }
                if (!kreatorZamowien.Zapisz())
                {
                    kreatorZamowien.WypiszBledy();
                }
            }
        }

        /// <summary>
        /// Przetwarza niezrealizowane zlecenia produkcyjne montowania na zamówienia do dostawców składników kompletu.
        /// </summary>
        public void GenerujZamowieniaDoDostawcowNaPodstawieZlecenMontowania()
        {
            _sfera.Kontekst().UstawMagazyn(_sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == "MAG")).UstawOddzial(_sfera.Centrale().Dane.Wszystkie().FirstOrDefault());

            DokumentZPM zpm1 = DodajZlecenieProdukcyjne();
            DokumentZPM zpm2 = DodajZlecenieProdukcyjne();
            DokumentZPM zpm3 = DodajZlecenieProdukcyjne();
            IKreatoryZamowienDoDostawcow kreatoryZamowien = _sfera.KreatoryZamowienDoDostawcow();
            IPodmioty podmioty = _sfera.Podmioty();
            IAsortymenty asortymenty = _sfera.Asortymenty();

            // ustawienie domyślnych dostawców w składnikach kompletu ZESO20 żeby nie trzeba było ich ustawiać ręcznie w kreatorze zamówień:
            UstawDostawceAsortymentu(zpm1.PozycjaKomplet.AsortymentAktualny.SkladnikiKompletu.ElementAt(0).Skladnik, podmioty.Dane.Pierwszy(p => p.Sygnatura.PelnaSygnatura == "ALA"));
            UstawDostawceAsortymentu(zpm1.PozycjaKomplet.AsortymentAktualny.SkladnikiKompletu.ElementAt(1).Skladnik, podmioty.Dane.Pierwszy(p => p.Sygnatura.PelnaSygnatura == "ERIE"));

            using (IKreatorZamowienDoDostawcow kreatorZamowien = kreatoryZamowien.UtworzNaPodstawieZlecenMontowania(new[] { zpm1, zpm2, zpm3 }, MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiary))
            {
                kreatorZamowien.MenedzerZapotrzebowania.DaneDokumentu.Uwagi = "Przetwarzanie zleceń montowania na zamówienie do dostawcy.";
                if (!kreatorZamowien.Zapisz())
                {
                    kreatorZamowien.WypiszBledy();
                }
            }
        }

        /// <summary>
        /// Dodaje dwie nowe kartoteki asortymentu z ustawioną ilością optymalną i podstawowym dostawcą oraz kilka zamówień od klienta. Generowane są zamówienia do dostawców na podstawie ilości optymalnej oraz ilości na zamówieniach.
        /// </summary>
        public void GenerujZamowieniaDoDostawcowDlaAsortymentuPonizejIlosciOptymalnej()
        {
            IKreatoryZamowienDoDostawcow kreatoryZamowien = _sfera.KreatoryZamowienDoDostawcow();
            IPodmioty podmioty = _sfera.Podmioty();
            Asortyment asortyment1 = DodajAsortymentZDostawcaIIlosciaOptymalna(10m, podmioty.Dane.Pierwszy(p => p.Sygnatura.PelnaSygnatura == "ALA"));
            Asortyment asortyment2 = DodajAsortymentZDostawcaIIlosciaOptymalna(15m, podmioty.Dane.Pierwszy(p => p.Sygnatura.PelnaSygnatura == "ERIE"));
            _sfera.Kontekst().UstawMagazyn(_sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == "MAG")).UstawOddzial(_sfera.Centrale().Dane.Wszystkie().FirstOrDefault());
            _ = DodajZamowienieOdKlienta("KOPARSKI", null, null, asortyment1.Symbol);
            _ = DodajZamowienieOdKlienta("MAGNUM", null, null, asortyment2.Symbol);
            _ = DodajZamowienieOdKlienta("PRUSK", null, null, asortyment1.Symbol);
            _ = DodajZamowienieOdKlienta("ADMAR", null, null, asortyment2.Symbol);

            using (IKreatorZamowienDoDostawcow kreatorZamowien = kreatoryZamowien.UtworzNaPodstawieAsortymentuPonizejStanuOptymalnego())
            {
                kreatorZamowien.MenedzerZapotrzebowania.DaneDokumentu.Uwagi = "Generowanie zamówień do dostawcy na podstawie asortymentu poniżej stanu optymalnego.";
                if (!kreatorZamowien.Zapisz())
                {
                    kreatorZamowien.WypiszBledy();
                }
            }
        }

        /// <summary>
        /// Przetwarza zamówienie od klienta w zamówienie do dostawcy z konsolidacją pozycji.
        /// </summary>
        public void PrzetworzZKDoZD()
        {
            DokumentZK zamowienie1 = DodajZamowienieOdKlienta("KOPARSKI", null, null, "DZSO100", "PEWTK50", "PUYAR07", "PEWTK50");
            DokumentZK zamowienie2 = DodajZamowienieOdKlienta("MAGNUM", null, null, "PEWTK50", "PUYAR07", "DZSO100", "PEWTK50");
            DokumentZK zamowienie3 = DodajZamowienieOdKlienta("PRUSK", null, null, "DZSO100", "PEWTK50", "DZSO100", "PEWTK50");
            using (IZamowienieDoDostawcy zamowienieDoDostawcy = _sfera.ZamowieniaDoDostawcow().Utworz())
            {
                zamowienieDoDostawcy.WypelnijNaPodstawieZK(zamowienie1.Pozycje.Concat(zamowienie2.Pozycje).Concat(zamowienie3.Pozycje).ToList(), MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiary);
                zamowienieDoDostawcy.PodmiotyDokumentu.UstawDostawceWedlugSymbolu("ALA");
                if (!zamowienieDoDostawcy.Zapisz())
                {
                    zamowienieDoDostawcy.WypiszBledy();
                }
            }
        }
    }
}
