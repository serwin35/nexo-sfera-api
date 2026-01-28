using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using InsERT.Moria.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpakowaniaPrezentowe
{
    /// <summary>
    /// Dodaje na formatce dokumentu ZPM w menu Operacje nową operację "Edytuj dedykacje", która pozwala skontrolować i ewentualnie poprawić dedykacje na montowanych opakowaniach prezentowych.
    /// </summary>
    public class Operacja_Formatka_EdytujDedykacje : OperacjaWOknieObiektu<IZlecenieProdukcyjneMontowania>
    {
        internal const string OperacjaId = "Formatka_Edytuj_Dedykacje";

        public override string Identyfikator => OperacjaId;

        public override string Nazwa => OperacjeHelper.PodajNazwe(KonfiguracjaPlugina.Instancja.Operacja_Formatka_EdytujDedykacje);

        protected override string[] SciezkaWMenu => OperacjeHelper.PodajSciezkeWMenu(KonfiguracjaPlugina.Instancja.Operacja_Formatka_EdytujDedykacje);

        protected override void Wykonaj(
            IZlecenieProdukcyjneMontowania obiekt,
            IKontekstOknaObiektu kontekstOknaObiektu,
            IKontekstOperacji kontekstOperacji)
        {
            PozycjaKomplet pozycjaKompletu = obiekt.Dane.PozycjaKomplet;
            if (pozycjaKompletu == null)
            {
                Okna.PokazOknoZBledem(kontekstOperacji.Uchwyt, "Nie dodano pozycji kompletu.");

                return;
            }

            Queue<SpecyfikacjaPozycji> specyfikacje = new Queue<SpecyfikacjaPozycji>(pozycjaKompletu.SpecyfikacjePozycji);

            if (specyfikacje.Any(s => s.Ilosc != Math.Ceiling(s.Ilosc)))
            {
                Okna.PokazOknoZBledem(kontekstOperacji.Uchwyt, $"Nieprawidłowa ilość w partii. Skorzystaj z pola własnego '{KonfiguracjaPlugina.Instancja.PoleWlasne_Partia_Dedykacja}' w rozbiciu pozycji.");

                return;
            }

            IEnumerable<TekstowyParametrOperacji> parametry = UtworzListeDostepnychGrawerunkow(specyfikacje);
            IOknoParametrowOperacji okno = UtworzOkno(kontekstOperacji.Uchwyt, parametry);

            if (!okno.Pokaz())
            {
                return;
            }

            MontazKompletowZGrawerunkiemPlugin.PrzepiszGrawerunkiDoPartiiKompletu(
                obiekt,
                pozycjaKompletu,
                parametry.Select(p => p.Wartosc));
        }

        // Tworzy osobny parametr dla każdej jednej sztuki montowanego opakowania.
        private static IEnumerable<TekstowyParametrOperacji> UtworzListeDostepnychGrawerunkow(
            Queue<SpecyfikacjaPozycji> specyfikacje)
        {
            List<TekstowyParametrOperacji> parametry = new List<TekstowyParametrOperacji>();

            while (specyfikacje.Count > 0)
            {
                SpecyfikacjaPozycji aktualnaSpecyfikacja = specyfikacje.Dequeue();
                string aktualnyGrawerunek = aktualnaSpecyfikacja?.PolaWlasneAdv2?.Get<string>(KonfiguracjaPlugina.Instancja.PoleWlasne_Partia_Dedykacja);
                decimal aktualnaIlosc = 0m;

                while (aktualnaIlosc < aktualnaSpecyfikacja.Ilosc)
                {
                    parametry.Add(
                        new TekstowyParametrOperacji($"Dedykacja nr {parametry.Count + 1}")
                        {
                            Wartosc = aktualnyGrawerunek
                        });

                    aktualnaIlosc += 1m;
                }
            }

            return parametry;
        }

        private static IOknoParametrowOperacji UtworzOkno(
            IUchwyt uchwyt,
            IEnumerable<ParametrOperacji> parametry)
        {
            IOknoParametrowOperacji okno = uchwyt.PodajObiektTypu<IOknoParametrowOperacji>();

            okno.Tytul = "Edycja dedykacji";
            okno.Parametry.Dodaj(parametry.ToArray());

            return okno;
        }
    }
}