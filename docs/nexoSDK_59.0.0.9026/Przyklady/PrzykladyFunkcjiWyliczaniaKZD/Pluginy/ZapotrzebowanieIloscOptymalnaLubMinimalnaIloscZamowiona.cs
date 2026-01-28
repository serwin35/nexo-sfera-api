using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Sfera;
using InsERT.Moria.Wspolne;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrzykladyFunkcjiWyliczaniaKZD
{
    /// <summary>
    /// <para>Funkcja zakłada istnienie pola własnego zaawansowanego w kartotece asortymentu o nazwie 'Minimalna ilość zamówiona'.</para>
    /// <para>W polu własnym przechowywana jest minimalna ilość danego asortymentu, którą można zamówić u dostawcy.</para>
    /// <para>Funkcja oblicza zapotrzebowanie przy użyciu domyślnej metody „Dla asortymentu poniżej stanu optymalnego”,
    /// a następnie porównuje wynik z minimalną ilością zapisaną w polu własnym kartoteki asortymentu. Do pozycji kreatora zamówień wpisywana jest większa z tych dwóch wartości.</para>
    /// </summary>
    public class ZapotrzebowanieIloscOptymalnaLubMinimalnaIloscZamowiona : IFunkcjaWyliczaniaKZD
    {
        private readonly IUchwyt _uchwyt;

        public ZapotrzebowanieIloscOptymalnaLubMinimalnaIloscZamowiona(IUchwyt uchwyt)
        {
            _uchwyt = uchwyt ?? throw new ArgumentNullException(nameof(uchwyt));
        }

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("AD0FCA50-8667-4F2D-8C8C-6E78FC288B37");

        public string Nazwa => "Zamów większą: wyliczone zapotrzebowanie/minimalna zamawiana ilość w kartotece asortymentu";

        public string Opis => $"Funkcja, która wylicza zapotrzebowanie wbudowaną metodą wyliczania zapotrzebowania 'Dla asortymentu poniżej stanu optymalnego' oraz uwzględnia ilość zapisaną w polu własnym '{Resources.NazwaPolaWlasnego}' z kartoteki asortymentu, następnie porównuje te wartości i zamawia większą.";

        // Funkcja może być użyta tylko przy przetwarzaniu asortymentów na widoku "Asortyment na wyczerpaniu":
        public IEnumerable<TypZdarzeniaDokumentowego> MiejscaUzycia
        {
            get
            {
                yield return TypZdarzeniaDokumentowego.UtworzenieZDNaPodstawieAsortymentow;
                yield return TypZdarzeniaDokumentowego.UtworzenieZDNaPodstawieAsortymentowPonizejStanuMinimalnego;
                yield return TypZdarzeniaDokumentowego.UtworzenieZDNaPodstawieAsortymentowPonizejStanuOptymalnego;
            }
        }

        public ParametryFunkcjiWyliczaniaKZD UtworzParametry(IKontekstWyliczaniaKZD kontekst) => new ParametryFunkcjiWyliczaniaKZD()
        {
            MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary,
            ObslugaKompletow = ObslugaKompletowDlaKZD.WstawKomplety,
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
            string nazwaMetodyKZD = "Dla asortymentu poniżej stanu optymalnego";

            // Pobranie odpowiedniej metody wyliczania zapotrzebowania
            var metodaKZD = _uchwyt.MetodyWyliczaniaKZD()
                                   .Dane.Wszystkie()
                                   .FirstOrDefault(m => m.Nazwa.Contains(nazwaMetodyKZD));

            // Komunikat o błędzie, kiedy metoda wyliczania zapotrzebowania nie została znaleziona
            if (metodaKZD == null)
                return LogicznyWynikOperacji.Falsz(string.Format(Resources.BrakMetodyWyliczania, nazwaMetodyKZD));

            // Wyliczenie zapotrzebowania dla wszystkich asortymentów na pozycjach kreatora zamówień do dostawców
            var zapotrzebowanieDictionary = kontekst.MenedzerZapotrzebowania.KalkulatorZapotrzebowania
                                                    .ListaZapotrzebowaniaAsortymentowWgMetodyWyliczania(metodaKZD, pozycje.Select(p => p.Asortyment.Id))
                                                    .ToDictionary(z => z.AsortymentID);

            foreach (IPozycjaMenedzeraZapotrzebowania pozycja in pozycje)
            {
                // Pomijamy pozycję menedżera, jeśli zapotrzebowanie dla asortymentu na tej pozycji nie zostało obliczone
                if (!zapotrzebowanieDictionary.TryGetValue(pozycja.Asortyment.Id, out var zapotrzebowanie))
                    continue;

                IPolaWlasneAdv2Accessor polaWlasneAccessor = _uchwyt.UtworzPolaWlasneAdv2Accessor(pozycja.Asortyment, PolaWlasneAdv2AccessorFactoryNullHandlingKind.CreateReadonlyStub);
                // Wartość pola własnego z minimalną ilością na zamówieniu:
                decimal? minimalnaIlosc = polaWlasneAccessor.PobierzWartoscTypuLiczbaRzeczywista(Resources.NazwaPolaWlasnego);

                if (!minimalnaIlosc.HasValue)
                {
                    // Zakładamy, że każda kartoteka musi mieć uzupełnioną minimalną ilość zamówioną więc operację kończymy błędem:
                    return LogicznyWynikOperacji.Falsz($"Asortyment {pozycja.Asortyment.Symbol} nie posiada uzupełnionego pola własnego {Resources.NazwaPolaWlasnego}");
                }

                var jednostkaPodstawowa = pozycja.Asortyment.PodstawowaJednostkaMiaryAsortymentu;
                var jednostkaDocelowa = pozycja.Jednostka;

                // Przeliczenie zapotrzebowania z podstawowej jednostki miary na jednostkę docelową
                decimal iloscZapotrzebowania = jednostkaPodstawowa.PrzeliczIloscNaJednostke(zapotrzebowanie.Zapotrzebowanie, jednostkaDocelowa);
                // Założenie, że w polu własnym przechowywana jest ilość w podstawowej jednostce miary dla asortymentu
                decimal iloscMinimalna = jednostkaPodstawowa.PrzeliczIloscNaJednostke(minimalnaIlosc.Value, jednostkaDocelowa);

                pozycja.Ilosc = Math.Max(iloscMinimalna, iloscZapotrzebowania);
            }
            return LogicznyWynikOperacji.Prawda();
        }
    }
}
