using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Wspolne;
using System;
using System.Collections.Generic;

namespace PrzykladyFunkcjiWyliczaniaKZD
{
    /// <summary>
    /// <para>Funkcja zakłada, że składniki do montażu kompletów zawsze zamawiane są w ilościach całkowitych.</para>
    /// <para>Działanie funkcji polega na zaokrąglaniu 'w górę' ilości na pozycjach kreatora zamówień.</para>
    /// </summary>
    public class ZaokraglanieIlosciZamawianychSkladnikow : IFunkcjaWyliczaniaKZD
    {
        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("5214311D-E520-407D-BBAF-312CE96327B3");

        public string Nazwa => "Zaokrąglij ilości zamawianych składników";

        public string Opis => "Funkcja zaokrągla ilości zamówionych składników montażu do liczby całkowitej.";

        // Funkcja może być użyta tylko przy przetwarzaniu zleceń montażu:
        public IEnumerable<TypZdarzeniaDokumentowego> MiejscaUzycia
        {
            get
            {
                yield return TypZdarzeniaDokumentowego.UtworzenieZamowienDoDostawcowNaPodstawieZlecenMontowania;
            }
        }

        public ParametryFunkcjiWyliczaniaKZD UtworzParametry(IKontekstWyliczaniaKZD kontekst) => new ParametryFunkcjiWyliczaniaKZD()
        {
            MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary,
            ObslugaKompletow = ObslugaKompletowDlaKZD.ZamowSkladniki,
            ObslugaUslug = ObslugaUslugDlaKZD.ZamowMaterialy,
            WyborMagazynu = WyborMagazynuDlaKZD.ZKontekstu,
            WyborOddzialu = WyborOddzialuDlaKZD.ZKontekstu
        };

        public LogicznyWynikOperacji CzyMoznaUzyc(IKontekstWyliczaniaKZD kontekst) => LogicznyWynikOperacji.Prawda();

        public LogicznyWynikOperacji Wylicz(IEnumerable<IPozycjaMenedzeraZapotrzebowania> pozycje, IKontekstWyliczaniaKZD kontekst)
        {
            foreach (IPozycjaMenedzeraZapotrzebowania pozycja in pozycje)
            {
                // Zaokrąglij w górę do liczby całkowitej:
                decimal iloscZaokraglona = Math.Ceiling(pozycja.Ilosc);
                if (pozycja.Ilosc != iloscZaokraglona)
                {
                    pozycja.Ilosc = iloscZaokraglona;
                }
            }
            return LogicznyWynikOperacji.Prawda();
        }
    }
}