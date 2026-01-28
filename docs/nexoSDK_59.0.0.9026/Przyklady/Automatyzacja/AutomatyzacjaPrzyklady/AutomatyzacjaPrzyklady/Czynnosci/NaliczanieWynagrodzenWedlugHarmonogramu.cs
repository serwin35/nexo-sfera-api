using InsERT.Moria;
using InsERT.Moria.Bank;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Place.Duze;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Sfera;
using InsERT.Moria.Wspolne;
using InsERT.Moria.Wspolne.RegulyAutomatyzacji;
using InsERT.Mox.Formatting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomatyzacjaPrzyklady
{
    /// <summary>
    /// Czynność dostępna w Harmonogramie w Automatach. Automatycznie nalicza wynagrodzenia z obecnego miesiąca.
    /// </summary>
    public class NaliczanieWynagrodzenWedlugHarmonogramu : ICzynnoscRegulyAutomatyzacji
    {
        private readonly Func<IMenadzerNaliczaniaWynagrodzen> _menadzerNaliczaniaWynagrodzen;
        private readonly Func<IWyplaty> _wyplaty;
        private readonly Func<IRachunkiDoUmowPracowniczych> _rachunkiDoUmowPracowniczych;

        public NaliczanieWynagrodzenWedlugHarmonogramu(
            Func<IMenadzerNaliczaniaWynagrodzen> menadzerNaliczaniaWynagrodzen,
            Func<IWyplaty> wyplaty,
            Func<IRachunkiDoUmowPracowniczych> rachunkiDoUmowPracowniczych
            )
        {
            _menadzerNaliczaniaWynagrodzen = menadzerNaliczaniaWynagrodzen;
            _wyplaty = wyplaty;
            _rachunkiDoUmowPracowniczych = rachunkiDoUmowPracowniczych;
        }

        public MiejsceWykorzystaniaObiektuAutomatyzacji MiejsceWykorzystania => MiejsceWykorzystaniaObiektuAutomatyzacji.Harmonogram;

        public IEnumerable<IDefinicjaParametruCzynnosciRegulyAutomatyzacji> WymaganeParametry => new[]
        {
            ParametryAutomatow.Czynnosc.Obiekt<DefinicjaListyPlac>("Lista płac", multiwybor: true)
        };

        public Guid Identyfikator => new Guid("432AB439-C4CD-416F-A021-0DFAB4F40201");

        public string Nazwa => "Nalicz wynagrodzenia";

        public string Opis => "Czynność naliczająca wynagrodzenia";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public DescriptiveBoolean Waliduj(IKontekstWykonywaniaCzynnosciRegulyAutomatyzacji kontekst)
        {
            return kontekst.WybraneParametry.Count() == 1;
        }

        public DescriptiveBoolean Wykonaj(IKontekstWykonywaniaCzynnosciRegulyAutomatyzacji kontekst)
        {
            IEnumerable<DefinicjaListyPlac> definicjeListPlac = ((IEnumerable<object>)kontekst.WybraneParametry[0]).OfType<DefinicjaListyPlac>();

            if (!definicjeListPlac.Any())
            {
                return DescriptiveBoolean.Error("Nie wybrano listy płac.");
            }

            var naliczoneWynagrodzenia = NaliczWynagrodzenia(kontekst.ZaplanowanaDataWykonania, definicjeListPlac, out string opisRezultatu);

            if (naliczoneWynagrodzenia.Any())
            {
                return DescriptiveBoolean.Success(opisRezultatu);
            }
            else
            {
                return DescriptiveBoolean.Error(opisRezultatu);
            }
        }

        private IEnumerable<Wynagrodzenie> NaliczWynagrodzenia(DateTime miesiac, IEnumerable<DefinicjaListyPlac> definicjeListPlac, out string opisRezultatu)
        {
            opisRezultatu = null;
            StringBuilder stringBuilder = new StringBuilder();

            using (var menadzer = _menadzerNaliczaniaWynagrodzen())
            {
                menadzer.Inicjalizuj();
                menadzer.SposobNaliczenia = SposobNaliczeniaWynagrodzenia.Automat;
                menadzer.Miesiac = miesiac;
                menadzer.DefinicjeListPlacNaliczaneCzesciowo.Clear();
                menadzer.DefinicjeListRachunkowNaliczaneCzesciowo.Clear();
                menadzer.DefinicjeListPlac.Clear();

                foreach (var definicjaListyPlac in definicjeListPlac)
                {
                    menadzer.DefinicjeListPlac.Add(definicjaListyPlac);
                }

                var identyfikatoryNaliczonychWynagrodzen = new List<int>();

                NaliczonoWynagrodzenieEventHandler handler = (s, e) =>
                {
                    if (!e.Wynik)
                    {
                        stringBuilder.AppendLine(e.Wynik.Description);
                    }
                    else if (typeof(Wynagrodzenie).IsAssignableFrom(e.Typ))
                    {
                        identyfikatoryNaliczonychWynagrodzen.Add((int)e.Id);
                    }
                };

                menadzer.NaliczonoWynagrodzenieEventHandler += handler;
                menadzer.NaliczWynagrodzenia();
                menadzer.NaliczonoWynagrodzenieEventHandler -= handler;

                var naliczoneWynagrodzenia = _wyplaty().Dane.Wszystkie(x => identyfikatoryNaliczonychWynagrodzen.Contains(x.Id)).ToList<Wynagrodzenie>()
                    .Concat(_rachunkiDoUmowPracowniczych().Dane.Wszystkie(x => identyfikatoryNaliczonychWynagrodzen.Contains(x.Id)));

                foreach (var naliczonaWyplata in naliczoneWynagrodzenia)
                {
                    stringBuilder.AppendLine($"Naliczono wynagrodzenie dla: {naliczonaWyplata.Podmiot.NazwaSkrocona}");
                }

                opisRezultatu = stringBuilder.ToString();

                return naliczoneWynagrodzenia;
            }
        }
    }
}
