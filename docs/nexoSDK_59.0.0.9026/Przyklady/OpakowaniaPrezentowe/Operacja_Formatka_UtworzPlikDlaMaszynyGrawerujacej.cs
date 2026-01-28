using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using System.Collections.Generic;
using System.IO;

namespace OpakowaniaPrezentowe
{
    /// <summary>
    /// Dodaje na formatce dokumentu ZPM w menu Operacje nową operację "Utwórz plik maszyny grawerującej", która pozwala na utworzenie pliku z informacjami o grawerunkach.
    /// </summary>
    public class Operacja_Formatka_UtworzPlikDlaMaszynyGrawerujacej : OperacjaWOknieObiektu<IZlecenieProdukcyjneMontowania>
    {
        public override string Identyfikator => "Formatka_Utworz_Plik";

        public override string Nazwa => OperacjeHelper.PodajNazwe(KonfiguracjaPlugina.Instancja.Operacja_Formatka_UtworzPlikMaszynyGrawerujacej);

        protected override string[] SciezkaWMenu => OperacjeHelper.PodajSciezkeWMenu(KonfiguracjaPlugina.Instancja.Operacja_Formatka_UtworzPlikMaszynyGrawerujacej);

        protected override bool SprawdzCzyMoznaWykonac(IZlecenieProdukcyjneMontowania obiekt, IKontekstOknaObiektu kontekstOknaObiektu, IKontekstOperacji kontekstOperacji)
        {
            return true;
        }

        protected override void Wykonaj(
            IZlecenieProdukcyjneMontowania obiekt,
            IKontekstOknaObiektu kontekstOknaObiektu,
            IKontekstOperacji kontekstOperacji)
        {
            List<string> dedykacje = OdczytajDedykacje(obiekt);

            if (dedykacje.Count == 0
                && ZapytajCzyEdytowacDedykacje(kontekstOknaObiektu)
                && WykonajOperacjeEdycjiDedykacji(kontekstOperacji))
            {
                dedykacje = OdczytajDedykacje(obiekt);
            }

            if (dedykacje.Count == 0)
            {
                return;
            }

            for (int i = 0; i < dedykacje.Count; i++)
            {
                string nazwaPliku = OperacjeHelper.PodajNazwePlikuSVGDlaDedykacji(obiekt.Dane, i + 1);
                string dane = new SerializatorGrawerunkow().Serializuj(dedykacje[i]);

                OperacjeHelper.ZapewnijFolder(nazwaPliku);

                File.WriteAllText(nazwaPliku, dane);
            }

            PokazOknoZInformacjaOSukcecie(kontekstOperacji.Uchwyt);
        }

        private static List<string> OdczytajDedykacje(
            IZlecenieProdukcyjneMontowania zlecenie)
        {
            List<string> dedykacje = new List<string>();

            PozycjaKomplet pozycjaKompletu = zlecenie.Dane.PozycjaKomplet;

            if (pozycjaKompletu != null)
            {
                foreach (SpecyfikacjaPozycji specyfikacja in pozycjaKompletu.SpecyfikacjePozycji)
                {
                    string dedykacja = OdczytajGrawerunek(specyfikacja);

                    if (!string.IsNullOrWhiteSpace(dedykacja))
                    {
                        dedykacje.Add(dedykacja);
                    }
                }
            }

            return dedykacje;
        }

        private static bool ZapytajCzyEdytowacDedykacje(
            IKontekstOknaObiektu kontekst)
        {
            if (kontekst.Tryb == TrybOknaObiektu.Edycja)
            {
                return Okna.PokazOknoZPytaniem(
                    kontekst.Uchwyt,
                    "Nie zostały wprowadzone żadne dedykacje. Czy chcesz je teraz wprowadzić?");
            }
            else
            {
                Okna.PokazOknoZInformacja(
                    kontekst.Uchwyt,
                    "Nie zostały wprowadzone żadne dedykacje.");

                return false;
            }
        }

        private static void PokazOknoZInformacjaOSukcecie(
            IUchwyt uchwyt)
        {
            Okna.PokazOknoZInformacja(
                uchwyt,
                $"Pliki dla maszyny grawerującej zostały zapisane w:\r\n{KonfiguracjaPlugina.Instancja.SciezkaZapisuSVG}");
        }

        private static bool WykonajOperacjeEdycjiDedykacji(
            IKontekstOperacji kontekstOperacji)
        {
            WynikOperacji wynikOperacji =
                kontekstOperacji.WykonajOperacje(
                    new IdentyfikatorOperacji(
                        OperacjeOpakowanPrezentowych.IdentyfikatorGrupy,
                        Operacja_Formatka_EdytujDedykacje.OperacjaId));

            if (wynikOperacji.Status == StatusOperacji.ZakonczonaNiepowodzeniem)
            {
                Okna.PokazOknoZBledem(kontekstOperacji.Uchwyt, wynikOperacji.Komunikat ?? "Operacja nie powiodła się.");
            }

            return wynikOperacji.Status == StatusOperacji.ZakonczonaPowodzeniem;
        }

        private static string OdczytajGrawerunek(
            SpecyfikacjaPozycji specyfikacja)
        {
            return specyfikacja
                .PolaWlasneAdv2?
                .Get<string>(KonfiguracjaPlugina.Instancja.PoleWlasne_Partia_Dedykacja);
        }
    }
}