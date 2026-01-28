using AutomatyzacjaPrzyklady.Rozszerzenia;
using InsERT.Moria;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Komunikacja;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozrachunki;
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
    /// Czynność dostępna w Harmonogramie w Automatach. Automatycznie wysyła SMS o zbliżającym się terminie płatności dla nieopłaconych rozrachunków.
    /// </summary>
    public class CzynnoscWyslijSMSONieoplaconymRozrachunku : ICzynnoscRegulyAutomatyzacji
    {
        private readonly Func<IRozrachunki> _rozrachunki;
        private readonly Func<IWiadomosciSMS> _wiadomosciSMS;

        public CzynnoscWyslijSMSONieoplaconymRozrachunku(
            Func<IRozrachunki> rozrachunki,
            Func<IWiadomosciSMS> wiadomosciSMS)
        {
            _rozrachunki = rozrachunki;
            _wiadomosciSMS = wiadomosciSMS;
        }

        public MiejsceWykorzystaniaObiektuAutomatyzacji MiejsceWykorzystania => MiejsceWykorzystaniaObiektuAutomatyzacji.Harmonogram;

        public IEnumerable<IDefinicjaParametruCzynnosciRegulyAutomatyzacji> WymaganeParametry => new List<IDefinicjaParametruCzynnosciRegulyAutomatyzacji>()
        {
            ParametryAutomatow.Czynnosc.LiczbaCalkowita("Ile dni wcześniej wysłać SMS", wartoscDomyslna: 1),
            ParametryAutomatow.Czynnosc.Obiekt<SzablonSMS>("Szablon"),
            ParametryAutomatow.Czynnosc.WartoscWyliczeniowa<WysylkaNatychmiastowaWiadomosci>("Wyślij natychmiastowo")
        };
        public Guid Identyfikator => new Guid("D23A3330-73AF-4619-97C0-6D58736A360F");

        public string Nazwa => "Wyślij SMS o zbliżającym się terminie płatności";

        public string Opis => "Wysyła SMS do klientów o zbliżającym się terminie płatności dla nieopłaconych rozrachunków.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();
        public DescriptiveBoolean Waliduj(IKontekstWykonywaniaCzynnosciRegulyAutomatyzacji kontekst)
        {
            if ((int)kontekst.WybraneParametry[0] < 0)
            {
                return DescriptiveBoolean.Error("Liczba dni nie może być wartością ujemną.");
            }
            if ((SzablonSMS)kontekst.WybraneParametry[1] == null)
            {
                return DescriptiveBoolean.Error("Wybrany szablon SMS nie istnieje.");
            }
            return DescriptiveBoolean.True;
        }

        public DescriptiveBoolean Wykonaj(IKontekstWykonywaniaCzynnosciRegulyAutomatyzacji kontekst)
        {
            TimeSpan liczbaDniPrzedTerminem = TimeSpan.FromDays((int)kontekst.WybraneParametry[0]);
            DateTime jutrzejszaData = DateTime.Today.AddDays(1);
            DateTime najpozniejszaDataTerminu = jutrzejszaData + liczbaDniPrzedTerminem;

            var nieoplaconeRozrachunki = _rozrachunki().Dane.Wszystkie().Where(r => r.KwotaPozostala > 0
                                                                             && r.TerminPlatnosci != null
                                                                             && r.TerminPlatnosci < najpozniejszaDataTerminu);
            if (!nieoplaconeRozrachunki.Any())
            {
                return DescriptiveBoolean.Success("Nie znaleziono nieopłaconych rozrachunków.");
            }

            SzablonSMS wybranySzablonSMS = (SzablonSMS)kontekst.WybraneParametry[1];
            WysylkaNatychmiastowaWiadomosci wysylkaNatychmiastowa = (WysylkaNatychmiastowaWiadomosci)kontekst.WybraneParametry[2];

            StringBuilder log = new StringBuilder();
            int liczbaWyslanychWiadomosci = 0;

            foreach (Rozrachunek rozrachunek in nieoplaconeRozrachunki)
            {
                Podmiot adresatPrzypomnienia = rozrachunek.Podmiot;

                using (IWiadomoscSMS nowaWiadomoscSMS = _wiadomosciSMS().Utworz())
                {
                    nowaWiadomoscSMS.UstawPodmiot(adresatPrzypomnienia);
                    nowaWiadomoscSMS.Dane.Dokument = rozrachunek.Dokument;
                    nowaWiadomoscSMS.WypelnijNaPodstawie(wybranySzablonSMS);

                    if (wysylkaNatychmiastowa == WysylkaNatychmiastowaWiadomosci.Tak)
                    {
                        nowaWiadomoscSMS.Dane.Status = (byte)StatusWiadomosciSMS.OczekujeNaWysylke;
                    }
                    else if (wysylkaNatychmiastowa == WysylkaNatychmiastowaWiadomosci.Nie)
                    {
                        nowaWiadomoscSMS.Dane.Status = (byte)StatusWiadomosciSMS.Robocza;
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("Niepoprawna wartość argumentu " + nameof(wysylkaNatychmiastowa));
                    }

                    if (!nowaWiadomoscSMS.Zapisz())
                    {
                        log.AppendLine($"Nie można zapisać wiadomości SMS do rozrachunku z dnia {rozrachunek.DataPowstania}{(rozrachunek.Dokument != null ? " do dokumentu " + rozrachunek.Dokument.NumerWewnetrzny.PelnaSygnatura : string.Empty)}:");
                        log.AppendLine(nowaWiadomoscSMS.PodajBledy());
                    }
                    else
                    {
                        liczbaWyslanychWiadomosci++;
                        log.AppendLine($"Wysłano wiadomość SMS do {adresatPrzypomnienia.NazwaSkrocona}");
                    }
                }
            }

            return liczbaWyslanychWiadomosci > 0
                ? DescriptiveBoolean.Success(log.ToString())
                : DescriptiveBoolean.Error(log.ToString());
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