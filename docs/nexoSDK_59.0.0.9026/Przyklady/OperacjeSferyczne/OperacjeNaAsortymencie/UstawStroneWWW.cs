using InsERT.Moria.Asortymenty;
using InsERT.Moria.Listy;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using InsERT.Moria.Sfera;
using System.Collections.Generic;
using System.Linq;

namespace OperacjeSferyczne.OperacjeNaAsortymencie
{
    public class UstawStroneWWW : OperacjaNaLiscieDanych<Asortyment, int>
    {
        private const string Akcja_UstawStroneWWW = "Ustaw stronę WWW";
        private const string Akcja_UsunStroneWWW = "Usuń stronę WWW";

        public override string Nazwa
            => "Ustaw stronę WWW";

        protected override string[] SciezkaWMenu
            => new[] { "Zbiorczo", "Opis" };

        protected override void Wykonaj(
            IReadOnlyCollection<int> identyfikatoryWybranychElementow,
            IKontekstListyDanych kontekstListyDanych,
            IKontekstOperacji kontekstOperacji)
        {
            if (!ZapytajOStroneWWWDoUstawienia(kontekstOperacji.Uchwyt, out string stronaWWW))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(stronaWWW)
                && !ZapytajCzyNaPewnoUsunacStroneWWW(kontekstOperacji.Uchwyt))
            {
                return;
            }

            List<Asortyment> wybraneAsortymenty = PobierzAsortymenty(kontekstOperacji.Uchwyt, identyfikatoryWybranychElementow);

            WykonajOperacjeZbiorcza(kontekstOperacji.Uchwyt, stronaWWW, wybraneAsortymenty);
        }

        private bool ZapytajOStroneWWWDoUstawienia(
            IUchwyt uchwyt,
            out string stronaWWW)
        {
            IOknoParametrowOperacji oknoParametrow = uchwyt.PodajObiektTypu<IOknoParametrowOperacji>();
            oknoParametrow.Tytul = Nazwa;

            ParametrWyboruWartosci<string> parametrAkcja
                = new ParametrWyboruWartosci<string>(
                    "Akcja",
                    new[]
                    {
                        Akcja_UstawStroneWWW,
                        Akcja_UsunStroneWWW
                    });

            TekstowyParametrOperacji parametrStronaWWW
                = new TekstowyParametrOperacji(
                    "Adres");

            parametrStronaWWW.UstawWidocznoscZaleznaOdWartosciInnegoParametru(
                parametrAkcja,
                v => v == Akcja_UstawStroneWWW); ;

            oknoParametrow.Parametry.Dodaj(
                parametrAkcja,
                parametrStronaWWW);

            if (oknoParametrow.Pokaz())
            {
                if (parametrAkcja.Wartosc == Akcja_UstawStroneWWW)
                {
                    stronaWWW = parametrStronaWWW.Wartosc;
                }
                else
                {
                    stronaWWW = string.Empty;
                }

                return true;
            }
            else
            {
                stronaWWW = null;

                return false;
            }
        }

        private static bool ZapytajCzyNaPewnoUsunacStroneWWW(
            IUchwyt uchwyt)
        {
            return Okna.PokazOknoZPytaniem(
                uchwyt,
                "Czy na pewno chcesz usunąć stronę WWW z wybranych asortymentów?");
        }

        private void WykonajOperacjeZbiorcza(
            IUchwyt uchwyt,
            string stronaWWW,
            IEnumerable<Asortyment> wybraneAsortymenty)
        {
            IOknoOperacjiZbiorczej oknoOperacjiZbiorczej = uchwyt.PodajObiektTypu<IOknoOperacjiZbiorczej>();
            oknoOperacjiZbiorczej.Tytul = Nazwa;

            oknoOperacjiZbiorczej.Pokaz(
                wybraneAsortymenty,
                a => UstawStroneWWWAsortymentu(uchwyt, a, stronaWWW));
        }

        private static List<Asortyment> PobierzAsortymenty(
            IUchwyt uchwyt,
            IEnumerable<int> identyfikatoryAsortymentow)
        {
            return uchwyt
                .PodajObiektTypu<IAsortymenty>()
                .Dane
                .Wszystkie()
                .Where(a => identyfikatoryAsortymentow.Contains(a.Id))
                .ToList();
        }

        private static WynikOperacji UstawStroneWWWAsortymentu(
            IUchwyt uchwyt,
            Asortyment asortyment,
            string stronaWWW)
        {
            IAsortymenty asortymenty = uchwyt.PodajObiektTypu<IAsortymenty>();

            using (IAsortyment asortymentBO = asortymenty.Znajdz(asortyment))
            {
                asortymentBO.Dane.StronaWWW = stronaWWW ?? string.Empty;

                if (asortymentBO.Zapisz())
                {
                    return WynikOperacji.ZakonczonaPowodzeniem();
                }
                else
                {
                    return asortymentBO.PobierzKomunikatyBledow().ToWynikOperacji();
                }
            }
        }
    }
}
