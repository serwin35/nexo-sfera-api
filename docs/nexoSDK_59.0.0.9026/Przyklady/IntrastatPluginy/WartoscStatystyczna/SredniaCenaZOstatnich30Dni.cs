using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Finanse;
using InsERT.Moria.Intrastat;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Waluty;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntrastatPluginy.WartoscStatystyczna
{
    /// <summary>
    /// Wylicza cenę statystyczną na podstawie średniej ceny sprzedaży dla wywozu lub zakupu dla przywozu.
    /// </summary>
    public class SredniaCenaZOstatnich30Dni : IFunkcjaWyliczaniaCenyStatystycznej
    {
        private readonly IDokumentyZakupu _dokumentyZakupu;
        private readonly IDokumentySprzedazy _dokumentySprzedazy;
        private readonly IParametryWalut _parametryWalut;
        private readonly Lazy<Waluta> _walutaSystemowa;
        // zapamiętujemy wyliczone w wewnętrznym cache:
        private readonly Dictionary<int, decimal> _cacheCenPrzywozu;
        private readonly Dictionary<int, decimal> _cacheCenWywozu;

        public SredniaCenaZOstatnich30Dni(IParametryWalut parametryWalut
            , IDokumentyZakupu dokumentyZakupu
            , IDokumentySprzedazy dokumentySprzedazy)
        {
            _dokumentyZakupu = dokumentyZakupu ?? throw new ArgumentNullException(nameof(dokumentyZakupu));
            _dokumentySprzedazy = dokumentySprzedazy ?? throw new ArgumentNullException(nameof(dokumentySprzedazy));
            _parametryWalut = parametryWalut ?? throw new ArgumentNullException(nameof(parametryWalut));
            _walutaSystemowa = new Lazy<Waluta>(() => _parametryWalut.WalutaSystemowa);
            _cacheCenPrzywozu = new Dictionary<int, decimal>();
            _cacheCenWywozu = new Dictionary<int, decimal>();
        }

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("A7834C1D-00EC-4CD0-9835-F292530AA6CC");

        public string Nazwa => "Średnia cena z ostatnich 30 dni";

        public string Opis => "Wylicza cenę statystyczną na podstawie średniej ceny zakupu (dla pozycji przywozowych) lub sprzedaży (dla pozycji wywozowych) z ostatnich 30 dni.";

        public Kwota WyliczCeneStatystyczna(PozycjaIntrastatu pozycjaIntrastatu, Asortyment asortyment)
        {
            Dictionary<int, decimal> cache = pozycjaIntrastatu.Typ == (byte)TypPozycji.Przywoz
                ? _cacheCenPrzywozu : _cacheCenWywozu;
            decimal cena = 0m;
            if (!cache.TryGetValue(asortyment.Id, out cena))
            {
                int precyzjaWaluty = _walutaSystemowa.Value.Precyzja;
                DateTime dataPodlegania = pozycjaIntrastatu.DataPodlegania
                    ?? pozycjaIntrastatu.PozycjaDokumentu.Dokument.DataDlaIntrastatu
                    ?? pozycjaIntrastatu.PozycjaDokumentu.Dokument.DataWprowadzenia;
                DateTime dataPoczatkowa = dataPodlegania.AddDays(-30);
                IQueryable<PozycjaDokumentu> pozycje;
                if (pozycjaIntrastatu.Typ == (byte)TypPozycji.Przywoz)
                {
                    // dla przywozu pobieramy pozycje dokumentów zakupu:
                    pozycje = _dokumentyZakupu.Dane.Wszystkie()
                        .Where(d => (d.DataWydaniaWystawienia ?? d.DataWprowadzenia) >= dataPoczatkowa
                                    && (d.DataWydaniaWystawienia ?? d.DataWprowadzenia) <= dataPodlegania)
                        .SelectMany(d => d.Pozycje);
                }
                else
                {
                    // dla wywozu pobieramy pozycje dokumentów sprzedaży:
                    pozycje = _dokumentySprzedazy.Dane.Wszystkie()
                        .Where(d => d.DataWprowadzenia >= dataPoczatkowa && d.DataWprowadzenia <= dataPodlegania)
                        .SelectMany(d => d.Pozycje);
                }
                var daneCen = (from pozycja in pozycje
                               let dokument = pozycja.Dokument
                               let kurs = dokument.KursWalutyDokumentu
                               let kursZnormalizowany = kurs == null ? 1m : (kurs.Kurs / kurs.LiczbaJednostek)
                               where pozycja.AsortymentAktualnyId == asortyment.Id
                               select new
                               {
                                   Ilosc = pozycja.IloscWJednostceBazowej,
                                   Wartosc = pozycja.Wartosc.NettoPoRabacie * kursZnormalizowany
                               }).ToArray();
                if (daneCen.Any())
                {
                    decimal sumaIlosci = daneCen.Select(d => d.Ilosc).Sum();
                    decimal sumaWartosci = daneCen.Select(d => d.Wartosc).Sum();
                    if (sumaIlosci != 0m)
                    {
                        cena = Math.Round(sumaWartosci / sumaIlosci, precyzjaWaluty, MidpointRounding.AwayFromZero);
                    }
                }
                cache.Add(asortyment.Id, cena);
            }
            if (cena == 0m)
                return null;
            return new Kwota(cena, _walutaSystemowa.Value);
        }
    }
}
