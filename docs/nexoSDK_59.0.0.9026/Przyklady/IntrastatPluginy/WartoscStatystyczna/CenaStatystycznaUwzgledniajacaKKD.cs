using InsERT.Moria.EgzekutorMagazynowy;
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
    /// Wylicza cenę statystyczną opierając się na wartości fakturowej doliczając kwotę z korekt kosztów dostaw powiazanych z partią asortymentu.
    /// </summary>
    public class CenaStatystycznaUwzgledniajacaKKD : IFunkcjaWyliczaniaCenyStatystycznej
    {
        private readonly IMagazynier _magazynier;
        private readonly IParametryWalut _parametryWalut;
        private readonly Lazy<Waluta> _walutaSystemowa;
        // doliczenia do wartości fakturowej zapamiętujemy w wewnętrznym cache:
        private readonly Dictionary<int, decimal> _cache;

        public CenaStatystycznaUwzgledniajacaKKD(IParametryWalut parametryWalut, IMagazynier magazynier)
        {
            _magazynier = magazynier ?? throw new ArgumentNullException(nameof(magazynier));
            _parametryWalut = parametryWalut ?? throw new ArgumentNullException(nameof(parametryWalut));
            _walutaSystemowa = new Lazy<Waluta>(() => _parametryWalut.WalutaSystemowa);
            _cache = new Dictionary<int, decimal>();
        }

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("CD22124A-FF1D-49FB-9985-717402E78469");

        public string Nazwa => "Wartość fakturowa z uwzględnieniem KKD";

        public string Opis => "Wylicza cenę statystyczną na podstawie wartości fakturowej z uwzględnieniem KKD z bieżącego miesiąca.";

        public Kwota WyliczCeneStatystyczna(PozycjaIntrastatu pozycjaIntrastatu, Asortyment asortyment)
        {
            if (pozycjaIntrastatu.IloscBazowa == 0m) // dla zerowej ilości nie wyliczamy wartości
                return null;
            int precyzjaWaluty = _walutaSystemowa.Value.Precyzja;
            decimal kwotaBazowa = Math.Round(pozycjaIntrastatu.WartoscFakturowa.GetValueOrDefault(0m) / pozycjaIntrastatu.IloscBazowa, precyzjaWaluty, MidpointRounding.AwayFromZero);
            SpecyfikacjaPozycji specyfikacja = pozycjaIntrastatu.SpecyfikacjaPozycjiPrzywozowa ?? pozycjaIntrastatu.SpecyfikacjaPozycji;
            Przyjecie przyjecie = specyfikacja?.Partia?.Przyjecie;
            if (przyjecie == null || przyjecie.EntityState == System.Data.Entity.EntityState.Added)
                return new Kwota(kwotaBazowa, _walutaSystemowa.Value);

            Dokument dokument = pozycjaIntrastatu.PozycjaDokumentu.Dokument;
            DateTime dataPodlegania = pozycjaIntrastatu.DataPodlegania ?? dokument.DataDlaIntrastatu ?? dokument.DataWprowadzenia;
            DateTime dataPoczatkowa = dataPodlegania.PoczatekMiesiaca();
            DateTime dataKoncowa = dataPodlegania.KoniecMiesiaca();
            decimal doliczenieJednostkowe = 0m;
            if (!_cache.TryGetValue(przyjecie.Id, out doliczenieJednostkowe))
            {
                var kosztyDodatkowe = _magazynier.Dane.KosztyDodatkowe()
                        .Where(k => k.Przyjecie.Id == przyjecie.Id
                                    && k.Data >= dataPoczatkowa && k.Data <= dataKoncowa
                                    && k.KosztDodatkowyGenerujacy == null // pomijamy koszty dodatkowe produkcji
                                    && k.KosztPrzyjeciaGenerujacy == null)
                        .Select(k => new
                        {
                            Wartosc = k.Wartosc * k.Kurs,
                            k.Przyjecie.Ilosc
                        })
                        .ToArray();

                if (kosztyDodatkowe.Any())
                {
                    decimal sumaIlosci = kosztyDodatkowe.Select(k => k.Ilosc).Sum();
                    decimal sumaWartosci = kosztyDodatkowe.Select(k => k.Wartosc).Sum();
                    if (sumaIlosci > 0m)
                        doliczenieJednostkowe = Math.Round(sumaWartosci / sumaIlosci, precyzjaWaluty, MidpointRounding.AwayFromZero);
                }
                _cache.Add(przyjecie.Id, doliczenieJednostkowe);
            }
            return new Kwota(kwotaBazowa + doliczenieJednostkowe, _walutaSystemowa.Value);
        }
    }
}
