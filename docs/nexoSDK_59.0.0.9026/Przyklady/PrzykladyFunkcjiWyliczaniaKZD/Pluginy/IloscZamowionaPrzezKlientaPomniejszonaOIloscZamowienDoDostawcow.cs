using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Sfera;
using InsERT.Moria.Wspolne;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrzykladyFunkcjiWyliczaniaKZD
{
    /// <summary>
    /// <para>Funkcja wylicza ilość zamówioną u dostawcy na podstawie:</para>
    /// <list type="number">
    /// <item>Ilości zamówionej przez klienta z realizowanych pozycji zamówień.</item>
    /// <item>Ilości aktualnie zamówionej u dostawców na niezrealizowanych zamówieniach.</item>
    /// </list>
    /// </summary>
    public class IloscZamowionaPrzezKlientaPomniejszonaOIloscZamowienDoDostawcow : IFunkcjaWyliczaniaKZD
    {
        private readonly IUchwyt _uchwyt;

        public IloscZamowionaPrzezKlientaPomniejszonaOIloscZamowienDoDostawcow(IUchwyt uchwyt)
        {
            _uchwyt = uchwyt ?? throw new ArgumentNullException(nameof(uchwyt));
        }

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("982078DA-A414-4422-ADFA-9AD92C5ADEFD");

        public string Nazwa => "Ilość zamówiona przez klienta pomniejszona o ilość zamówioną u dostawców";

        public string Opis => "Funkcja wylicza ilość pozycji kreatora zamówień na podstawie ilości zamówionej przez klienta pomniejszoną o ilość zamówioną u dostawców na niezrealizowanych zamówieniach.";

        // Funkcja może być użyta tylko przy przetwarzaniu zamówień od klientów:
        public IEnumerable<TypZdarzeniaDokumentowego> MiejscaUzycia
        {
            get
            {
                yield return TypZdarzeniaDokumentowego.UtworzenieZamowienDoDostawcowNaPodstawieZamowienOdKlientow;
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
            IEnumerable<int> asortymentyPozycji = pozycje.Select(p => p.Asortyment.Id).ToArray();
            IIlosciDoRealizacji ilosciDoRealizacji = _uchwyt.IlosciDoRealizacji();
            // Pobieramy ilości zamówione u dostawców:
            Dictionary<int, decimal> ilosciZamowioneUDostawcow = ilosciDoRealizacji
                .Dane
                .Wszystkie()
                .Where(i => i.TypDokumentuRealizowanego == (int)TypDokumentu.ZamowienieDoDostawcy && i.AsortymentId.HasValue && asortymentyPozycji.Contains(i.AsortymentId.Value))
                .GroupBy(i => i.AsortymentId.Value)
                .Select(g => new
                {
                    g.Key,
                    PozostalaIlosc = g.Select(i => i.PozostalaIlosc).DefaultIfEmpty(0m).Sum()
                })
                .ToDictionary(g => g.Key, g => g.PozostalaIlosc);

            foreach (IPozycjaMenedzeraZapotrzebowania pozycja in pozycje)
            {
                decimal iloscDoZamowienia = pozycja.PozycjeRealizowane.Select(p => p.Ilosc).DefaultIfEmpty(0m).Sum();
                if (ilosciZamowioneUDostawcow.TryGetValue(pozycja.Asortyment.Id, out decimal iloscZamowionaUDostawcow))
                {
                    // Pomniejszamy ilość z zamówień o ilość zamówioną u dostawców:
                    iloscDoZamowienia = Math.Max(0m, iloscDoZamowienia - iloscZamowionaUDostawcow);
                }
                if (pozycja.IloscWJednostceBazowej != iloscDoZamowienia)
                {
                    if (pozycja.Jednostka.Id != pozycja.Asortyment.PodstawowaJednostkaMiaryAsortymentu.Id)
                    {
                        pozycja.Jednostka = pozycja.Asortyment.PodstawowaJednostkaMiaryAsortymentu;
                    }
                    pozycja.Ilosc = iloscDoZamowienia;
                }
            }
            return LogicznyWynikOperacji.Prawda();
        }
    }
}