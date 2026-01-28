using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;
using System.Linq;

namespace RealizacjeDokumentow.Przyklady
{
    public class LancuchyZaliczek : RealizacjaBase
    {
        public LancuchyZaliczek(Uchwyt sfera) : base(sfera) { }

        /// <summary>
        /// Dodaje zamówienie od klienta z płatnością natychmiastową, realizuje do faktury zaliczkowej i wystawia kolejną fakturę zaliczkową w łańcuchu.
        /// </summary>
        public void KolejnaFakturaZaliczkowa()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("ARTUR", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "DZFOREVER", "PEFLEUR15", "POYAR01", "WOBLACK100");
            Console.WriteLine("Realizacja zamówienia do faktury zaliczkowej.");
            DokumentDS fakturaZaliczkowa;
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
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureZaliczkowa())
            {
                faktura.Platnosci.TrybPrzeliczania |= TrybPrzeliczaniaPlatnosci.PrzenoszeniePlatnosciNaZaliczke; // bez tego kwota z płatności nie będzie się przenosiła na zaliczkę
                faktura.ObslugaDokumentuZaliczkowego.WypelnijNaPodstawiePoprzedniejZaliczki(fakturaZaliczkowa);
                PlatnoscDokumentu platnosc = faktura.Platnosci.DodajPlatnoscNatychmiastowa(_sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN);
                // po ustawieniu kwoty płatności zostaje ona potraktowana jako zaliczka i rozpisana wg domyślnego algorytmu rozpisywania zaliczki na pozycje
                platnosc.KwotaDokumentu = 50m;
                faktura.Dane.Uwagi = "To jest kolejna zaliczka na kwotę 50PLN.";
                _ = ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodaje zamówienie od klienta z płatnością natychmiastową, realizuje do faktury zaliczkowej i wystawia fakturę zaliczkową końcową.
        /// </summary>
        public void FakturaKoncowa()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("ARTUR", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "DZFOREVER", "WOBLACK70", "PEFLEUR15", "POYAR01", "WOBLACK100");
            Console.WriteLine("Realizacja zamówienia do faktury zaliczkowej.");
            DokumentDS fakturaZaliczkowa;
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
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureZaliczkowa())
            {
                faktura.ObslugaDokumentuZaliczkowego.ZamknijNaPodstawieZaliczkowej(fakturaZaliczkowa);
                faktura.Dane.Uwagi = "To jest faktura zaliczkowa końcowa.";
                faktura.Przelicz();
                faktura.Platnosci.DodajPlatnosciDomyslne();
                _ = ZapiszDokumentSprzedazy(faktura);
            }
        }

        /// <summary>
        /// Dodaje zamówienie od klienta, realizuje do faktury zaliczkowej i dokonuje całościowego zwrotu zaliczki na korekcie faktury zaliczkowej.
        /// </summary>
        public void ZwrotZaliczki()
        {
            DokumentZK zamowienie = DodajZamowienieOdKlienta("KOLOROWA", null, _sfera.FormyPlatnosci().DaneDomyslne.GotowkaPLN, "ZELAQUA", "WOBLACK70", "PEFLEUR15", "POYAR01", "WOBLACK100", "PEWTK50");
            Console.WriteLine("Realizacja zamówienia do faktury zaliczkowej.");
            DokumentDS fakturaZaliczkowa;
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
            using (IKorektaDokumentuSprzedazy korekta = _sfera.KorektyDokumentowSprzedazy().UtworzKorekteFakturyZaliczkowej(fakturaZaliczkowa))
            {
                foreach (PozycjaKorekty pozycja in korekta.Dane.Pozycje.OfType<PozycjaKorekty>())
                {
                    // nie można zwrócić więcej niż wartość wydana
                    pozycja.AspektZaliczkiPozycji.Zaliczka.Brutto = Math.Round(pozycja.AspektZaliczkiPozycji.PozycjaKontraktuZaliczki.ZaliczkaSkonsumowana, pozycja.Dokument.Waluta.Precyzja, MidpointRounding.AwayFromZero);
                }
                korekta.ObslugaDokumentuZaliczkowego.SumujZaliczkiPozycji();
                korekta.Przelicz();
                korekta.Platnosci.DodajPlatnosciDomyslne();
                korekta.Dane.Uwagi = "Całościowy zwrot zaliczki.";
                _ = ZapiszKorekteDokumentuSprzedazy(korekta);
            }
        }

    }
}
