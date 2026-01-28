using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Sfera;
using InsERT.Moria.Wspolne;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrzykladyFunkcjiWyliczaniaKZD
{
    /// <summary>
    /// <para>Funkcja oblicza zapotrzebowanie przy użyciu domyślnej metody „Dla asortymentu poniżej stanu optymalnego”,
    /// a następnie porównuje wynik z domyślną ilością zakupu zapisaną w kartotece asortymentu uwzględniając jednostki miary. Do pozycji kreatora zamówień wpisywana jest większa z tych dwóch wartości.</para>
    /// </summary>
    public class ZapotrzebowanieIloscOptymalnaLubIloscJednostkaPrzyZakupie : IFunkcjaWyliczaniaKZD
    {
        private readonly IUchwyt _uchwyt;

        public ZapotrzebowanieIloscOptymalnaLubIloscJednostkaPrzyZakupie(IUchwyt uchwyt)
        {
            _uchwyt = uchwyt ?? throw new ArgumentNullException(nameof(uchwyt));
        }

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("271946F6-D6FE-4D68-8F13-603773A21D2B");

        public string Nazwa => "Zamów większą: wyliczone zapotrzebowanie/domyślna ilość przy zakupie";

        public string Opis => "Funkcja, która wylicza zapotrzebowanie wbudowaną metodą wyliczania zapotrzebowania 'Dla asortymentu poniżej stanu optymalnego' oraz przelicza ilość dla jednostki przy zakupie z kartoteki asortymentu, następnie porównuje te wartości i zamawia większą.";

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

        public LogicznyWynikOperacji CzyMoznaUzyc(IKontekstWyliczaniaKZD kontekst) => LogicznyWynikOperacji.Prawda();

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

                var asortyment = pozycja.Asortyment;
                var jednostkaZakupu = asortyment.JednostkaZakupu;
                var jednostkaPodstawowa = asortyment.PodstawowaJednostkaMiaryAsortymentu;
                var jednostkaDocelowa = pozycja.Jednostka;

                // Przeliczenie zapotrzebowania z podstawowej jednostki miary na jednostkę docelową
                decimal ilosc = jednostkaPodstawowa.PrzeliczIloscNaJednostke(zapotrzebowanie.Zapotrzebowanie, jednostkaDocelowa);

                // Jeżeli istnieje jednostka zakupu, sprawdzamy czy domyślna ilość zakupu jest większa od zapotrzebowania
                if (jednostkaZakupu != null && asortyment.DomyslnaIloscZakupu > 0m)
                {
                    var domyslnaIlosc = jednostkaZakupu.PrzeliczIloscNaJednostke(asortyment.DomyslnaIloscZakupu, jednostkaDocelowa);
                    ilosc = Math.Max(domyslnaIlosc, ilosc);
                }

                // Przypisanie odpowiedniej wartości do pozycji kreatora zamówień do dostawców
                pozycja.Ilosc = ilosc;
            }
            return LogicznyWynikOperacji.Prawda();
        }
    }
}
