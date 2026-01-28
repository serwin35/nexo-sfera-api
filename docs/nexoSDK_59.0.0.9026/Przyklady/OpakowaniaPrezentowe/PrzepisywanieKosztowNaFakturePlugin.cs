using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Dokumenty.Logistyka.Zdarzenia;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System.Linq;
using static OpakowaniaPrezentowe.WspolneUtils;

namespace OpakowaniaPrezentowe
{
    /// <summary>
    /// Przepisuje koszty montażu opakowania prezentowego do pola własnego pozycji na fakturze.
    /// </summary>
    public class PrzepisywanieKosztowNaFakturePlugin : KlientSferyZdarzeniowejDokumentu<IDokumentSprzedazy>
    {
        public override void PrzedWypelnieniemNaPodstawieInnegoDokumentu(
            IKontekstZdarzeniaPrzedWypelnieniemNaPodstawieInnegoDokumentu<IDokumentSprzedazy> kontekst)
        {
            foreach (PozycjaDokumentu pozycjaZrodlowa in kontekst.PozycjeZrodlowe)
            {
                if (CzyPozycjaKompletu(pozycjaZrodlowa)
                    && !CzyPozycjaSkompletowana(pozycjaZrodlowa)
                    && CzyPrzepisywacKosztyOpakowan(kontekst.Uchwyt, pozycjaZrodlowa.Dokument))
                {
                    kontekst.ZablokujZdarzenie($"Nie można zrealizaować zamówienia {pozycjaZrodlowa.Dokument.NumerWewnetrzny.PelnaSygnatura}, ponieważ zawiera ono opakowania prezentowe, które nie zostały skompletowane.");
                }
            }
        }

        private static bool CzyPozycjaSkompletowana(PozycjaDokumentu pozycja)
        {
            return pozycja.StanRealizacjiZamowienia.SkompletowanaIlosc == pozycja.IloscWJednostceBazowej;
        }

        public override void PoWypelnieniuNaPodstawieInnegoDokumentu(
            IKontekstZdarzeniaPoWypelnieniuNaPodstawieInnegoDokumentu<IDokumentSprzedazy> kontekst)
        {
            foreach (IPozycjaUtworzonaNaPodstawieInnejPozycji utworzonaPozycja in kontekst.UtworzonePozycje)
            {
                if (CzyPozycjaKompletu(utworzonaPozycja.UtworzonaPozycja)
                    && CzyPozycjaRelizujeZamowienie(utworzonaPozycja))
                {
                    PrzepiszKosztOpakowaniaPrezentowego(kontekst.Uchwyt, utworzonaPozycja);
                }
            }
        }

        private static bool CzyPozycjaKompletu(
            PozycjaDokumentu pozycja)
        {
            return pozycja
                .RodzajAsortymentu
                .CzyKomplet();
        }

        private static bool CzyPozycjaRelizujeZamowienie(
            IPozycjaUtworzonaNaPodstawieInnejPozycji pozycja)
        {
            return pozycja
                .PozycjeZrodlowe
                .All(p => p.PozycjaZrodlowa.Dokument is DokumentZK);
        }

        private static void PrzepiszKosztOpakowaniaPrezentowego(
            IUchwyt uchwyt,
            IPozycjaUtworzonaNaPodstawieInnejPozycji utworzonaPozycja)
        {
            if (utworzonaPozycja.UtworzonaPozycja.PolaWlasneAdv2 == null)
            {
                return;
            }

            decimal kosztOpakowan = 0m;

            foreach (IPrzeniesionaPozycja pozycjaZrodlowa in utworzonaPozycja.PozycjeZrodlowe)
            {
                if (CzyPrzepisywacKosztyOpakowan(uchwyt, pozycjaZrodlowa.PozycjaZrodlowa.Dokument))
                {
                    kosztOpakowan += PobierzKosztOpakowaniaPrezentowego(uchwyt, pozycjaZrodlowa);
                }
            }

            if (kosztOpakowan != 0m)
            {
                utworzonaPozycja.UtworzonaPozycja.PolaWlasneAdv2.Set(
                    KonfiguracjaPlugina.Instancja.PoleWlasne_PozycjaDokumentu_KosztOpakowan,
                    kosztOpakowan);
            }
        }

        private static bool CzyPrzepisywacKosztyOpakowan(
            IUchwyt uchwyt,
            Dokument dokument)
        {
            return CzyFlagaWKonfiguracjiUstawiona(
                uchwyt,
                dokument,
                KonfiguracjaPlugina.Instancja.PoleWlasne_Konfiguracja_PrzepiszKosztyOpakowanPrzyRealizacji);
        }

        private static decimal PobierzKosztOpakowaniaPrezentowego(
            IUchwyt uchwyt,
            IPrzeniesionaPozycja pozycjaZrodlowa)
        {
            return pozycjaZrodlowa
                .PozycjaZrodlowa
                .ZnajdzRealizacjeRealizujace(TypDokumentu.ZlecenieProdukcyjneMontowania)
                .Where(r => r.TypDokumentuRealizowanego == (int)TypDokumentu.ZamowienieOdKlienta)
                .Sum(PoliczKosztMagazynowyMontazu);

            decimal PoliczKosztMagazynowyMontazu(RealizacjaPozycji realizacja)
            {
                if (!(realizacja.PozycjaRealizujaca is PozycjaKomplet komplet))
                {
                    return 0m;
                }

                decimal kosztMagazynowy = 0m;

                foreach (PozycjaSkladnik skladnik in komplet.PozycjeSkladnik)
                {
                    if (CzyAsortymentNalezyDoGrupyOpakowanPrezentowych(uchwyt, skladnik.AsortymentAktualny, (DokumentZK)realizacja.PozycjaRealizowana.Dokument))
                    {
                        kosztMagazynowy += skladnik.KosztMagazynowy;
                    }
                }

                return kosztMagazynowy;
            }
        }
    }
}