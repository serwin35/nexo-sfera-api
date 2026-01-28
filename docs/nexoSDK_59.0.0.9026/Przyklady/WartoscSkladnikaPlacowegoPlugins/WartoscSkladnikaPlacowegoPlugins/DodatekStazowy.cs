using InsERT.Moria.Kadry;
using InsERT.Moria.Kadry.Duze;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Place.Duze;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WartoscSkladnikaPlacowegoPlugins
{
    public class DodatekStazowy : IFunkcjaWyliczaniaWartosciSkladnikaWyplatyZDefinicjaZaleznosci
    {
        private const string _nazwaRodzajuStazu = "Staż do dodatku";
        private const string _nazwaSkladnikaZWynagrodzeniemZasadniczym = "Podstawa miesięczna";

        private readonly Func<IStazPracyUtils> _stazPracyUtilsLocator;
        private readonly IRodzajeStazuPracy _rodzajeStazuPracy;

        public DodatekStazowy(
            Func<IStazPracyUtils> stazPracyUtilsLocator,
            IRodzajeStazuPracy rodzajeStazuPracy)
        {
            _stazPracyUtilsLocator = stazPracyUtilsLocator
                ?? throw new ArgumentNullException(nameof(stazPracyUtilsLocator));
            _rodzajeStazuPracy = rodzajeStazuPracy
                ?? throw new ArgumentNullException(nameof(rodzajeStazuPracy));
        }

        public Guid Identyfikator => new Guid("912B8DC8-56AE-4351-9B2A-251BA85F7708");

        public string Nazwa => "Dodatek stażowy";

        public string Opis => "Funkcja wylicza wartość dodatku stażowego na podstawie własnego rodzaju stażu 'Staż do dodatku'. " +
            "Dodatek stażowy przysługuje po 5 latach w wysokości 5% podstawy miesięcznej. Każdy kolejny rok zwiększa wysokość dodatku o 1% aż do 20%.";

        public decimal WartoscNominalna(Wynagrodzenie wynagrodzenie, SkladnikPlacowy skladnikPlacowy)
        {
            // Jeśli nie zdefiniowano rodzaju stażu pracy, zwracamy zerową wartość.
            var rodzajStazuPracy = _rodzajeStazuPracy.Dane.Wszystkie().Where(x => x.Nazwa == _nazwaRodzajuStazu).FirstOrDefault();
            if (rodzajStazuPracy == null)
                return 0M;

            // Jeśli na wynagrodzeniu nie ma podstawy miesięcznej, również zwracamy zerową wartość.
            var podstawaMiesieczna = wynagrodzenie.WartosciSladnikowPlacowychWynagrodzenia
                .Where(x => x.SkladnikPlacowy.Nazwa == _nazwaSkladnikaZWynagrodzeniemZasadniczym)
                .OfType<WartoscSkladnikaPlacowegoWynagrodzeniaGr>()
                .FirstOrDefault();
            if (podstawaMiesieczna == null)
                return 0M;

            var miesiacWynagrodzenia = wynagrodzenie.ListaWynagrodzen.Miesiac;
            var koniecPoprzedniegoMiesiaca = new DateTime(miesiacWynagrodzenia.Year, miesiacWynagrodzenia.Month, 1).AddDays(-1);

            var lataPracy = _stazPracyUtilsLocator().WyliczStazPracy(
                wynagrodzenie.Umowa.Pracownik.PracownikGr,
                koniecPoprzedniegoMiesiaca,
                (TypUmowyPracowniczej)wynagrodzenie.Umowa.Typ,
                rodzajStazuPracy,
                true).Lata;

            if (lataPracy < 5)
                return 0M;

            var procentDodatku = Math.Min(lataPracy, 20) * 0.01M;

            return procentDodatku * podstawaMiesieczna.WartoscNominalna;
        }

        public IDostawcaPluginow Dostawca => new DostawcaPluginowInsERT();

        public IEnumerable<ZaleznoscOdSkladnika> ZaleznosciOdSkladnikow => new ZaleznoscOdSkladnika[]
        {
            new ZaleznoscOdSkladnika()
            {
                NazwaSkladnika = _nazwaSkladnikaZWynagrodzeniemZasadniczym,
                NazwaPola = nameof(WartoscSkladnikaPlacowegoWynagrodzeniaGr.WartoscNominalna)
            }
        };

        public IEnumerable<string> ZaleznosciOdPolWynagrodzenia => Enumerable.Empty<string>();
    }
}
