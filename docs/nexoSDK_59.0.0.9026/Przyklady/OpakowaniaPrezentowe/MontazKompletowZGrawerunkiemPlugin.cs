using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Dokumenty.Logistyka.Zdarzenia;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System.Collections.Generic;
using System.Linq;

namespace OpakowaniaPrezentowe
{
    /// <summary>
    /// Odczytuje treść grawerunków z pola własnego "Wiadomość do sprzedającego" na zamówieniu i przenosi je do pól własnych "Grawerunek" w partiach tworzonych kompletów.
    /// </summary>
    public class MontazKompletowZGrawerunkiemPlugin : KlientSferyZdarzeniowejDokumentu<IZlecenieProdukcyjneMontowania>
    {
        public override void PoWypelnieniuNaPodstawieInnegoDokumentu(
            IKontekstZdarzeniaPoWypelnieniuNaPodstawieInnegoDokumentu<IZlecenieProdukcyjneMontowania> kontekst)
        {
            if (!CzyMontowanieZamowionegoKompletu(kontekst, out DokumentZK montowaneZamowienie))
            {
                return;
            }

            PozycjaKomplet pozycjaKompletu = PodajPozycjeKompletu();

            if (!SprobujPobracWiadomoscDoSprzedajacego(montowaneZamowienie, out string wiadomoscDoSprzedajacego))
            {
                return;
            }

            PrzepiszGrawerunkiDoPartiiKompletu(
                kontekst.ObiektBiznesowy,
                pozycjaKompletu,
                OdczytajGrawerunkiZWiadomosciDoSprzedajacego(wiadomoscDoSprzedajacego));

            PozycjaKomplet PodajPozycjeKompletu()
            {
                return kontekst
                    .ObiektBiznesowy
                    .Dane
                    .PozycjaKomplet;
            }
        }

        private static bool CzyMontowanieZamowionegoKompletu(
            IKontekstZdarzeniaPoWypelnieniuNaPodstawieInnegoDokumentu<IZlecenieProdukcyjneMontowania> kontekst,
            out DokumentZK montowaneZamowienie)
        {
            montowaneZamowienie = null;

            if (kontekst.DokumentyZrodlowe.Count() == 1)
            {
                montowaneZamowienie = kontekst.DokumentyZrodlowe.Single() as DokumentZK;
            }

            return montowaneZamowienie != null;
        }

        private static bool SprobujPobracWiadomoscDoSprzedajacego(
            DokumentZK montowaneZamowienie,
            out string wiadomoscDoSprzedajacego)
        {
            wiadomoscDoSprzedajacego = montowaneZamowienie
                .PolaWlasneAdv2?
                .Get<string>(KonfiguracjaPlugina.Instancja.PoleWlasne_DokumentZK_WiadomoscDoSprzedajacego);

            return !string.IsNullOrWhiteSpace(wiadomoscDoSprzedajacego);
        }

        private static IEnumerable<string> OdczytajGrawerunkiZWiadomosciDoSprzedajacego(
            string wiadomoscDoSprzedajacego)
        {
            return wiadomoscDoSprzedajacego
                .Split(',')
                .Select(g => g.Trim())
                .ToArray();
        }

        internal static void PrzepiszGrawerunkiDoPartiiKompletu(
            IZlecenieProdukcyjneMontowania zlecenieBO,
            PozycjaKomplet pozycjaKompletu,
            IEnumerable<string> grawerunki)
        {
            IRozbiciePozycjiPrzyjeciowe rozbiciePozycji = RozpocznijRozbicie();

            try
            {
                WyczyscRozbicie();

                decimal pozostalaIlosc = pozycjaKompletu.IloscWJednostceBazowej;
                foreach (string grawerunek in grawerunki)
                {
                    if (pozostalaIlosc < 1m)
                    {
                        break;
                    }

                    DodajPartieZGrawerunkiem(grawerunek);

                    pozostalaIlosc -= 1m;
                }

                rozbiciePozycji.ZakonczRozbicie();
            }
            catch
            {
                rozbiciePozycji.AnulujRozbicie();

                throw;
            }

            IRozbiciePozycjiPrzyjeciowe RozpocznijRozbicie()
            {
                return zlecenieBO
                    .ObslugaRozbiciaPozycji
                    .RozpocznijRozbiciePrzyjeciowe(pozycjaKompletu);
            }

            void WyczyscRozbicie()
            {
                foreach (IPozycjaRozbiciaPrzyjeciowa pozycjaRozbicia in rozbiciePozycji.Pozycje.ToList())
                {
                    rozbiciePozycji.UsunPozycje(pozycjaRozbicia);
                }
            }

            void DodajPartieZGrawerunkiem(string grawerunek)
            {
                IPozycjaRozbiciaPrzyjeciowa pozycjaRozbicia = rozbiciePozycji.DodajPozycje();
                pozycjaRozbicia.Ilosc = 1m;
                pozycjaRozbicia.PolaWlasne.Set(KonfiguracjaPlugina.Instancja.PoleWlasne_Partia_Dedykacja, grawerunek);
            }
        }
    }
}