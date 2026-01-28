using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Wspolne;
using System;
using System.Collections.Generic;

namespace PrzykladyFunkcjiWyliczaniaKZD
{
    /// <summary>
    /// <para>Funkcja zakłada istnienie pola własnego zaawansowanego w kartotece asortymentu o nazwie 'Minimalna ilość zamówiona'.</para>
    /// <para>W polu własnym przechowywana jest minimalna ilość danego asortymentu, którą można zamówić u dostawcy.</para>
    /// <para>Działanie funkcji polega na powiększaniu ilości na pozycji kreatora zamówień do minimalnej ilości możliwej do zamówienia.</para>
    /// </summary>
    public class UwzglednianieMinimalnejZamawianejIlosci : IFunkcjaWyliczaniaKZD
    {
        private readonly IUchwyt _uchwyt;

        public UwzglednianieMinimalnejZamawianejIlosci(IUchwyt uchwyt)
        {
            _uchwyt = uchwyt ?? throw new ArgumentNullException(nameof(uchwyt));
        }

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("087909BA-CDAC-4CD8-9EFC-B9767A23B152");

        public string Nazwa => "Uwzględnij minimalną ilość zamówioną";

        public string Opis => $"Funkcja uwzględnia minimalną zamówioną ilość danej kartoteki asortymentu zapisaną w polu własnym o nazwie '{Resources.NazwaPolaWlasnego}'.";

        // Funkcja może być użyta w każdym przypadku:
        public IEnumerable<TypZdarzeniaDokumentowego> MiejscaUzycia => null;

        public ParametryFunkcjiWyliczaniaKZD UtworzParametry(IKontekstWyliczaniaKZD kontekst) => new ParametryFunkcjiWyliczaniaKZD()
        {
            MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary,
            ObslugaKompletow = ObslugaKompletowDlaKZD.ZamowSkladniki,
            ObslugaUslug = ObslugaUslugDlaKZD.ZamowMaterialy,
            WyborMagazynu = WyborMagazynuDlaKZD.ZKontekstu,
            WyborOddzialu = WyborOddzialuDlaKZD.ZKontekstu
        };

        public LogicznyWynikOperacji CzyMoznaUzyc(IKontekstWyliczaniaKZD kontekst)
        {
            IZaawansowanePolaWlasne zaawansowanePolaWlasne = _uchwyt.PodajObiektTypu<IZaawansowanePolaWlasne>();
            if (!zaawansowanePolaWlasne.ObslugujeZaawansowanePolaWlasne(typeof(Asortyment)))
            {
                return LogicznyWynikOperacji.Falsz(Resources.BrakObslugiPolWlasnych);
            }
            if (zaawansowanePolaWlasne.PobierzZaawansowanePoleWlasne(typeof(Asortyment), Resources.NazwaPolaWlasnego) == null)
            {
                return LogicznyWynikOperacji.Falsz(string.Format(Resources.BrakPolaWlasnego, Resources.NazwaPolaWlasnego));
            }
            return LogicznyWynikOperacji.Prawda();
        }

        public LogicznyWynikOperacji Wylicz(IEnumerable<IPozycjaMenedzeraZapotrzebowania> pozycje, IKontekstWyliczaniaKZD kontekst)
        {
            foreach (IPozycjaMenedzeraZapotrzebowania pozycja in pozycje)
            {
                IPolaWlasneAdv2Accessor polaWlasneAccessor = _uchwyt.UtworzPolaWlasneAdv2Accessor(pozycja.Asortyment, PolaWlasneAdv2AccessorFactoryNullHandlingKind.CreateReadonlyStub);
                // Wartość pola własnego z minimalną ilością na zamówieniu:
                decimal? minimalnaIlosc = polaWlasneAccessor.PobierzWartoscTypuLiczbaRzeczywista(Resources.NazwaPolaWlasnego);
                if (!minimalnaIlosc.HasValue)
                {
                    // Zakładamy, że każda kartoteka musi mieć uzupełnioną minimalną ilość zamówioną więc operację kończymy błędem:
                    return LogicznyWynikOperacji.Falsz($"Asortyment {pozycja.Asortyment.Symbol} nie posiada uzupełnionego pola własnego {Resources.NazwaPolaWlasnego}");
                }
                if (pozycja.Ilosc < minimalnaIlosc.Value)
                {
                    // Jeśli ilość wygenerowana na pozycji kreatora zamówień jest mniejsza od minimalnej ilości w polu własnym to poprawiamy.
                    pozycja.Ilosc = minimalnaIlosc.Value;
                }
            }
            return LogicznyWynikOperacji.Prawda();
        }
    }
}