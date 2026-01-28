using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Flagi;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.ModelOrganizacyjny;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Sfera;
using InsERT.Moria.Skojarzenia;
using InsERT.Moria.Wspolne;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PrzykladyFunkcjiWyliczaniaKZD
{
    /// <summary>
    /// Funkcja zawsze zamawia na określony magazyn przyjęć i generuje w tle odłożone przesunięcie międzymagazynowe na zamówiony asortyment.
    /// </summary>
    public class ZamawiajZawszeNaMagazynPrzyjecZPrzesunieciem : IFunkcjaWyliczaniaKZD
    {
        private readonly IUchwyt _uchwyt;
        private readonly IMagazyny _magazyny;
        private readonly IPrzyjeciaMiedzymagazynowe _przyjecia;
        private readonly IStatusyDokumentow _statusy;
        private readonly IFlagiWlasne _flagiWlasne;

        public ZamawiajZawszeNaMagazynPrzyjecZPrzesunieciem(IUchwyt uchwyt)
        {
            _uchwyt = uchwyt ?? throw new ArgumentNullException(nameof(uchwyt));
            _magazyny = _uchwyt.Magazyny();
            _przyjecia = _uchwyt.PrzyjeciaMiedzymagazynowe();
            _statusy = _uchwyt.StatusyDokumentow();
            _flagiWlasne = _uchwyt.FlagiWlasne();
        }

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("D7ABD00E-7295-43CF-9EE2-72DE9109652A");

        public string Nazwa => "Zamów do magazynu przyjęć i wygeneruj przesunięcie";

        public string Opis => $"Funkcja generuje przesunięcie międzymagazynowe z magazynu '{Resources.SymbolMagazynuPrzyjec}' na magazyn docelowy (magazyn z pozycji zamówienia od klientów, magazyn wydań dla zleceń produkcyjnych lub magazyn zlecenia serwisowego) oznaczone flagą o nazwie '{Resources.NazwaFlagiPrzesuniecia}'.";

        // Funkcja może być użyta przy przetwarzaniu zamówień od klientów, zleceń produkcyjnych montowania lub zleceń montowania:
        public IEnumerable<TypZdarzeniaDokumentowego> MiejscaUzycia
        {
            get
            {
                yield return TypZdarzeniaDokumentowego.UtworzenieZamowienDoDostawcowNaPodstawieZlecenMontowania;
                yield return TypZdarzeniaDokumentowego.UtworzenieZamowienDoDostawcowNaPodstawieZlecenSerwisowych;
                yield return TypZdarzeniaDokumentowego.UtworzenieZamowienDoDostawcowNaPodstawieZamowienOdKlientow;
            }
        }

        public ParametryFunkcjiWyliczaniaKZD UtworzParametry(IKontekstWyliczaniaKZD kontekst) => new ParametryFunkcjiWyliczaniaKZD()
        {
            MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji,
            ObslugaKompletow = ObslugaKompletowDlaKZD.ZamowSkladniki,
            ObslugaUslug = ObslugaUslugDlaKZD.ZamowMaterialy,
            WyborMagazynu = WyborMagazynuDlaKZD.Konkretny,
            Magazyn = ZnajdzMagazynPrzyjec(),
            WyborOddzialu = WyborOddzialuDlaKZD.ZKontekstu
        };

        public LogicznyWynikOperacji CzyMoznaUzyc(IKontekstWyliczaniaKZD kontekst)
        {
            if (!_magazyny.Dane.Wszystkie().Where(m => m.Symbol == Resources.SymbolMagazynuPrzyjec).Any())
            {
                return LogicznyWynikOperacji.Falsz($"W systemie nie istnieje magazyn o symbolu '{Resources.SymbolMagazynuPrzyjec}'.");
            }
            if (!FlagiPrzesuniecia().Any())
            {
                return LogicznyWynikOperacji.Falsz($"W systemie nie istnieje flaga o nazwie '{Resources.NazwaFlagiPrzesuniecia}' dla przyjęć magazynowych.");
            }
            return LogicznyWynikOperacji.Prawda();
        }

        public LogicznyWynikOperacji Wylicz(IEnumerable<IPozycjaMenedzeraZapotrzebowania> pozycje, IKontekstWyliczaniaKZD kontekst)
        {
            Func<PozycjaDokumentu, Magazyn> pobierzMagazynDocelowy;
            switch (kontekst.SposobUzycia)
            {
                case TypZdarzeniaDokumentowego.UtworzenieZamowienDoDostawcowNaPodstawieZlecenMontowania:
                    pobierzMagazynDocelowy = p => ((p as PozycjaSkladnik)?.Dokument as DokumentProdukcyjny)?.MagazynSkladnikow;
                    break;
                case TypZdarzeniaDokumentowego.UtworzenieZamowienDoDostawcowNaPodstawieZlecenSerwisowych:
                    pobierzMagazynDocelowy = p => p.Magazyn ?? (p.Dokument as DokumentCRM)?.PowiazaneZlecenieSerwisowe?.Magazyn;
                    break;
                case TypZdarzeniaDokumentowego.UtworzenieZamowienDoDostawcowNaPodstawieZamowienOdKlientow:
                    pobierzMagazynDocelowy = p => p.Magazyn ?? p.Dokument.Magazyn;
                    break;
                default:
                    throw new InvalidOperationException("Niepoprawne użycie funkcji.");
            }

            var pozycjeGrupowanePoMagazynie = pozycje
                .Select(p => new
                {
                    PozycjaKreatora = p,
                    // Mozemy użyć Single() gdyż w parametrach nie konsolidujemy pozycji:
                    p.PozycjeRealizowane.Single().PozycjaRealizowana,
                    Magazyn = pobierzMagazynDocelowy(p.PozycjeRealizowane.Single().PozycjaRealizowana)
                })
                .Where(p => p.PozycjaRealizowana.AsortymentAktualnyId.HasValue) // Upewniamy się, że pobieramy tylko pozycje asortymentów z kartoteki.
                .GroupBy(d => d.Magazyn.Id);

            Magazyn zrodlowy = ZnajdzMagazynPrzyjec();
            List<string> wygenerowanePrzesuniecia = new List<string>();
            List<string> zaktualizowanePrzesuniecia = new List<string>();
            foreach (var grupaPozycji in pozycjeGrupowanePoMagazynie)
            {
                DokumentMMP istniejaceNiewykonane = _przyjecia
                    .Dane
                    .Wszystkie()
                    .Where(mmp => mmp.StatusDokumentu.SkutekMagazynowyPrzyjecia == (byte)SkutekMagazynowy.Brak
                                && mmp.StatusDokumentu.SkutekMagazynowyWydania == (byte)SkutekMagazynowy.Brak
                                && mmp.FlagaWlasna.Nazwa == Resources.NazwaFlagiPrzesuniecia
                                && mmp.MagazynId == grupaPozycji.Key)
                    .FirstOrDefault();

                using (IPrzyjecieMiedzymagazynowe przyjecie = istniejaceNiewykonane == null ? _przyjecia.Utworz() : _przyjecia.Znajdz(istniejaceNiewykonane))
                {
                    bool zapiszPrzyjecie = false;
                    if (istniejaceNiewykonane == null)
                    {
                        przyjecie.Dane.DokumentMMW.Magazyn = zrodlowy;
                        przyjecie.Dane.Magazyn = grupaPozycji.First().Magazyn;
                        przyjecie.Dane.StatusDokumentu = _statusy.DaneDomyslne.PrzyjecieMiedzymagazynowe_Odlozone;
                        przyjecie.Dane.FlagaWlasna = FlagiPrzesuniecia().First();
                        zapiszPrzyjecie = true;
                    }
                    foreach (var pozycja in grupaPozycji)
                    {
                        IPozycjaMenedzeraZapotrzebowania pozycjaKreatora = pozycja.PozycjaKreatora;
                        PozycjaDokumentu pozycjaPrzesuniecia = przyjecie.Dane.Pozycje
                            .FirstOrDefault(p => p.Opis.Contains(pozycja.PozycjaRealizowana.Dokument.NumerWewnetrzny.PelnaSygnatura)
                                                && p.AsortymentAktualnyId == pozycjaKreatora.Asortyment.Id
                                                && p.JednostkaMiaryAsId == pozycja.PozycjaKreatora.Jednostka.Id);
                        if (pozycjaPrzesuniecia == null)
                        {
                            pozycjaPrzesuniecia = przyjecie.Pozycje.Dodaj(pozycjaKreatora.Asortyment, pozycjaKreatora.Ilosc, pozycjaKreatora.Jednostka);
                            pozycjaPrzesuniecia.Opis += $"{pozycja.PozycjaRealizowana.Dokument.NumerWewnetrzny.PelnaSygnatura}{Environment.NewLine}";
                            zapiszPrzyjecie = true;
                        }
                    }
                    if (zapiszPrzyjecie)
                    {
                        if (przyjecie.Zapisz())
                        {
                            (istniejaceNiewykonane == null ? wygenerowanePrzesuniecia : zaktualizowanePrzesuniecia).Add($"{przyjecie.Dane.NumerWewnetrzny.PelnaSygnatura} na {przyjecie.Dane.Magazyn.Symbol}");
                        }
                        else
                        {
                            return LogicznyWynikOperacji.Falsz($"Nie udało się wygenerować przesunięcia międzymagazynowego na magazyn {przyjecie.Dane.Magazyn.Symbol}:{Environment.NewLine}{string.Join(Environment.NewLine, przyjecie.PobierzKomunikatyBledow().Select(k => k.Tresc))}");
                        }
                    }
                }
            }

            if (wygenerowanePrzesuniecia.Any() || zaktualizowanePrzesuniecia.Any())
            {
                StringBuilder komunikat = new StringBuilder();
                if (wygenerowanePrzesuniecia.Any())
                {
                    komunikat.Append("Wygenerowano przesunięcia międzymagazynowe:");
                    komunikat.AppendLine();
                    komunikat.AppendLine(string.Join(Environment.NewLine, wygenerowanePrzesuniecia));
                }
                if (zaktualizowanePrzesuniecia.Any())
                {
                    komunikat.Append("Zaktualizowano przesunięcia międzymagazynowe:");
                    komunikat.AppendLine();
                    komunikat.AppendLine(string.Join(Environment.NewLine, zaktualizowanePrzesuniecia));
                }
                Okna.PokazOknoZInformacja(_uchwyt, komunikat.ToString());
            }

            return LogicznyWynikOperacji.Prawda();
        }

        private Magazyn ZnajdzMagazynPrzyjec() => _magazyny.Dane.Pierwszy(m => m.Symbol == Resources.SymbolMagazynuPrzyjec);

        private IQueryable<FlagaWlasna> FlagiPrzesuniecia() => _flagiWlasne.Dane.Wszystkie().Where(f => f.Nazwa == Resources.NazwaFlagiPrzesuniecia && ((f.Domena ?? 0) & (byte)DomenaFlagiWlasnej.PrzyjeciaMagazynowe) > 0);
    }
}