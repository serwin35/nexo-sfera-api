using AutomatyzacjaPrzyklady.Rozszerzenia;
using InsERT.Moria;
using InsERT.Moria.Komunikacja;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Wspolne;
using InsERT.Moria.Wspolne.RegulyAutomatyzacji;
using InsERT.Mox.Formatting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutomatyzacjaPrzyklady
{
    /// <summary>
    /// Czynność dostępna w Harmonogramie w Automatach. Automatycznie wysyła SMS o zbliżającym się terminie badań pracownika.
    /// </summary>
    public class CzynnoscPrzypomnijOKoncuBadan : ICzynnoscRegulyAutomatyzacji
    {
        private readonly Func<IWiadomosciSMS> _wiadomosciSMS;
        private readonly Func<IDataSystemowa> _dataSystemowa;

        public CzynnoscPrzypomnijOKoncuBadan(
            Func<IWiadomosciSMS> wiadomosciSMS,
            Func<IDataSystemowa> dataSystemowa
            )
        {
            _wiadomosciSMS = wiadomosciSMS;
            _dataSystemowa = dataSystemowa;
        }
        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("80D6CDBE-D752-41A4-A51F-9D2517CCCCC2");

        public string Nazwa => "Przypomnij o końcu badań lekarskich";

        public string Opis => "Wysyła wiadomości SMS do pracowników 30 dni przed końcem badań lekarskich";

        public MiejsceWykorzystaniaObiektuAutomatyzacji MiejsceWykorzystania => MiejsceWykorzystaniaObiektuAutomatyzacji.Harmonogram;

        public IEnumerable<IDefinicjaParametruCzynnosciRegulyAutomatyzacji> WymaganeParametry => new IDefinicjaParametruCzynnosciRegulyAutomatyzacji[]
        {
            ParametryAutomatow.Czynnosc.Obiekt<Badanie>("Badanie"),
            ParametryAutomatow.Czynnosc.LiczbaCalkowita("Liczba ostatnich dni", wartoscDomyslna: 30),
            ParametryAutomatow.Czynnosc.Obiekt<SzablonSMS>("Szablon"),
            ParametryAutomatow.Czynnosc.WartoscWyliczeniowa<WysylkaNatychmiastowaWiadomosci>("Wyślij natychmiastowo")
        };

        public DescriptiveBoolean Waliduj(IKontekstWykonywaniaCzynnosciRegulyAutomatyzacji kontekst)
        {
            return kontekst.WybraneParametry.Count == 4
                && kontekst.WybraneParametry[0] is Badanie
                && kontekst.WybraneParametry[1] is int
                && kontekst.WybraneParametry[2] is SzablonSMS
                && kontekst.WybraneParametry[3] is WysylkaNatychmiastowaWiadomosci;
        }

        public DescriptiveBoolean Wykonaj(IKontekstWykonywaniaCzynnosciRegulyAutomatyzacji kontekst)
        {
            #region Ekstraktowanie parametrów

            Badanie wybraneBadanie = kontekst.WybraneParametry[0] as Badanie;
            if (wybraneBadanie == null)
            {
                return DescriptiveBoolean.Error("Wybrane badanie nie istnieje.");
            }

            int liczbaDniDoKonca = (int)kontekst.WybraneParametry[1];
            DateTime limitTerminu = _dataSystemowa().AktualnyCzas().Date.AddDays(liczbaDniDoKonca);

            SzablonSMS wybranySzablonSMS = kontekst.WybraneParametry[2] as SzablonSMS;
            if (wybranySzablonSMS == null)
            {
                return DescriptiveBoolean.Error("Wybrany szablon SMS nie istnieje.");
            }

            bool wysylkaNatychmiastowa = (WysylkaNatychmiastowaWiadomosci)kontekst.WybraneParametry[3] == WysylkaNatychmiastowaWiadomosci.Tak;

            #endregion

            var konczaceSieBadania = PodajKonczaceSieBadania(wybraneBadanie, limitTerminu);
            bool czySukces = WyslijWiadomosciOKoncuTerminuBadania(konczaceSieBadania, wybranySzablonSMS, wysylkaNatychmiastowa, out string komunikatZakonczenia);

            return new DescriptiveBoolean()
            {
                Value = czySukces,
                Description = komunikatZakonczenia
            };
        }

        private IEnumerable<BadanieData> PodajKonczaceSieBadania(Badanie sprawdzaneBadanie, DateTime limitTerminu)
        {
            // dane o terminach badań wszystkich pracowników dla wybranego rodzaju badania
            var datyBadan = sprawdzaneBadanie.BadaniaDaty;

            var konczaceSieBadania = datyBadan.GroupBy(
                                        b => b.PracownikGr.Id,
                                        b => b,
                                        (key, bs) => bs.OrderByDescending(b => b.Do ?? DateTime.MaxValue).FirstOrDefault()
                                     ).Where(b => !b.Bezterminowo && b.Do <= limitTerminu);

            var badaniaPrzedKoncemUmowy = from badanie in konczaceSieBadania
                                          let ostatniDzienUmowyPracowniczej = badanie.PracownikGr.Pracownik.UmowyPracownicze
                                                                             .Select(d => d.OstatniDzienObowiazywania ?? DateTime.MaxValue)
                                                                             .DefaultIfEmpty(DateTime.MinValue)
                                                                             .OrderByDescending(x => x)
                                                                             .FirstOrDefault()
                                          where badanie.Do < ostatniDzienUmowyPracowniczej
                                          select badanie;

            return badaniaPrzedKoncemUmowy;
        }

        private bool WyslijWiadomosciOKoncuTerminuBadania(IEnumerable<BadanieData> konczaceSieBadania, SzablonSMS szablonSMS, bool wysylkaNatychmiastowa, out string komunikatZakonczenia)
        {
            StringBuilder sb = new StringBuilder();

            bool wyslanoWszystkie = true;
            foreach (var badanie in konczaceSieBadania)
            {
                var podmiotPracownika = badanie.PracownikGr.Pracownik.Osoba.Podmiot;

                using (IWiadomoscSMS nowaWiadomoscSMS = _wiadomosciSMS().Utworz())
                {
                    nowaWiadomoscSMS.UstawPodmiot(podmiotPracownika);
                    nowaWiadomoscSMS.WypelnijNaPodstawie(szablonSMS);
                    nowaWiadomoscSMS.Dane.Status = wysylkaNatychmiastowa
                                                   ? (byte)StatusWiadomosciSMS.OczekujeNaWysylke
                                                   : (byte)StatusWiadomosciSMS.Robocza;
                    if (nowaWiadomoscSMS.Zapisz())
                    {
                        sb.AppendLine($"Wysłano wiadomość SMS do pracownika {podmiotPracownika.NazwaSkrocona}.");
                    }
                    else
                    {
                        sb.AppendLine($"Nie można zapisać wiadomości SMS dla pracownika {podmiotPracownika.NazwaSkrocona}:");
                        sb.AppendLine(nowaWiadomoscSMS.PodajBledy());
                        wyslanoWszystkie = false;
                    }
                }
            }

            komunikatZakonczenia = sb.ToString();
            return wyslanoWszystkie;
        }
        public enum WysylkaNatychmiastowaWiadomosci
        {
            [Nazwa("Tak")]
            Tak,

            [Nazwa("Nie")]
            Nie
        }
    }
}
