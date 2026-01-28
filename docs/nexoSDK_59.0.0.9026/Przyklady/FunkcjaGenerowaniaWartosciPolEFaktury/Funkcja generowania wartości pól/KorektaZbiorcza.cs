using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Sfera;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KsefPluginy
{
    public class KorektaZbiorcza : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private class DaneFakturyKorygowanej
        {
            public int Id { get; set; }
            public string Numer { get; set; }
            public string NumerKsef { get; set; }
            public DateTime DataWystawienia { get; set; }
        }

        private class WierszTabeliVatKorektyZbiorczej
        {
            public decimal Stawka { get; set; }
            public bool Zwolniona { get; set; }
            public decimal Netto { get; set; }
            public decimal Vat => Math.Round(Netto * Stawka, 2);
            public decimal Brutto => Math.Round(Netto * (1m + Stawka), 2);
        }

        private readonly IUchwyt _uchwyt;
        private readonly Lazy<Guid> _idWalutySystemowej;

        private List<DaneFakturyKorygowanej> _fakturyKorygowane;
        private List<WierszTabeliVatKorektyZbiorczej> _tabelaVatKorektyZbiorczej;

        public KorektaZbiorcza(IUchwyt uchwyt)
        {
            _uchwyt = uchwyt ?? throw new ArgumentNullException(nameof(uchwyt));
            _idWalutySystemowej = new Lazy<Guid>(PobierzIdWalutySystemowej);
        }

        private readonly Guid _id = new Guid("A921A1A8-8F14-44E4-B841-CACB0D251759");

        public Guid Identyfikator => _id;

        public string Nazwa => "Korekta zbiorcza";

        public string Opis => "Wypełnia numery korygowanych dokumentów na podstawie danych z zadanego okresu w polu własnym i wylicza rabat zbiorczy.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            ignorujPole = false;

            switch (pole)
            {
                // Stawka 23%:
                case PolaEFaktury.P_13_1:
                    {
                        WierszTabeliVatKorektyZbiorczej wiersz = PobierzWierszTabeliVatKorektyZbiorczej(kontekst.Dokument, 0.23m, false);
                        return wiersz == null ? null : string.Format(CultureInfo.InvariantCulture, "{0:0.00}", wiersz.Netto);
                    }
                case PolaEFaktury.P_14_1:
                    {
                        WierszTabeliVatKorektyZbiorczej wiersz = PobierzWierszTabeliVatKorektyZbiorczej(kontekst.Dokument, 0.23m, false);
                        return wiersz == null ? null : string.Format(CultureInfo.InvariantCulture, "{0:0.00}", wiersz.Vat);
                    }
                case PolaEFaktury.P_14_1W:
                    {
                        // w przypadku faktur w walucie obcej należy wyliczyć VAT w PLN
                    }
                    break;

                // Stawka 8%:
                case PolaEFaktury.P_13_2:
                    {
                        WierszTabeliVatKorektyZbiorczej wiersz = PobierzWierszTabeliVatKorektyZbiorczej(kontekst.Dokument, 0.08m, false);
                        return wiersz == null ? null : string.Format(CultureInfo.InvariantCulture, "{0:0.00}", wiersz.Netto);
                    }
                case PolaEFaktury.P_14_2:
                    {
                        WierszTabeliVatKorektyZbiorczej wiersz = PobierzWierszTabeliVatKorektyZbiorczej(kontekst.Dokument, 0.08m, false);
                        return wiersz == null ? null : string.Format(CultureInfo.InvariantCulture, "{0:0.00}", wiersz.Vat);
                    }
                case PolaEFaktury.P_14_2W:
                    {
                        // w przypadku faktur w walucie obcej należy wyliczyć VAT w PLN
                    }
                    break;

                // Stawka 5%:
                case PolaEFaktury.P_13_3:
                    {
                        WierszTabeliVatKorektyZbiorczej wiersz = PobierzWierszTabeliVatKorektyZbiorczej(kontekst.Dokument, 0.05m, false);
                        return wiersz == null ? null : string.Format(CultureInfo.InvariantCulture, "{0:0.00}", wiersz.Netto);
                    }
                case PolaEFaktury.P_14_3:
                    {
                        WierszTabeliVatKorektyZbiorczej wiersz = PobierzWierszTabeliVatKorektyZbiorczej(kontekst.Dokument, 0.05m, false);
                        return wiersz == null ? null : string.Format(CultureInfo.InvariantCulture, "{0:0.00}", wiersz.Vat);
                    }
                case PolaEFaktury.P_14_3W:
                    {
                        // w przypadku faktur w walucie obcej należy wyliczyć VAT w PLN
                    }
                    break;

                // Stawka 0% w sprzedaży krajowej:
                case PolaEFaktury.P_13_6_1:
                    {
                        WierszTabeliVatKorektyZbiorczej wiersz = PobierzWierszTabeliVatKorektyZbiorczej(kontekst.Dokument, 0m, false);
                        return wiersz == null ? null : string.Format(CultureInfo.InvariantCulture, "{0:0.00}", wiersz.Netto);
                    }

                // Stawka zwolniona:
                case PolaEFaktury.P_13_7:
                    {
                        WierszTabeliVatKorektyZbiorczej wiersz = PobierzWierszTabeliVatKorektyZbiorczej(kontekst.Dokument, 0m, true);
                        return wiersz == null ? null : string.Format(CultureInfo.InvariantCulture, "{0:0.00}", wiersz.Netto);
                    }

                case PolaEFaktury.P_15:
                    {
                        List<WierszTabeliVatKorektyZbiorczej> wiersze = PobierzWierszeTabeliVatKorektyZbiorczej(kontekst.Dokument);
                        return wiersze.Any() ? string.Format(CultureInfo.InvariantCulture, "{0:0.00}", wiersze.Select(w => w.Brutto).Sum()) : null;
                    }

                case PolaEFaktury.PrzyczynaKorekty:
                    {
                        if (CzyKorektaZbiorcza(kontekst.Dokument))
                        {
                            return "Rabat za okres";
                        }
                    }
                    break;

                case PolaEFaktury.DataWystFaKorygowanej:
                    {
                        DaneFakturyKorygowanej dane = kontekst.IndeksElementu.HasValue ? PobierzDaneFakturyKorygowanej(kontekst.Dokument, kontekst.IndeksElementu.Value) : null;
                        return dane == null ? null : dane.DataWystawienia.ToString("yyyy-MM-dd");
                    }
                case PolaEFaktury.NrFaKorygowanej:
                    {
                        DaneFakturyKorygowanej dane = kontekst.IndeksElementu.HasValue ? PobierzDaneFakturyKorygowanej(kontekst.Dokument, kontekst.IndeksElementu.Value) : null;
                        return dane == null ? null : dane.Numer;
                    }
                case PolaEFaktury.NrKsefFaKorygowanej:
                    {
                        DaneFakturyKorygowanej dane = kontekst.IndeksElementu.HasValue ? PobierzDaneFakturyKorygowanej(kontekst.Dokument, kontekst.IndeksElementu.Value) : null;
                        return dane?.NumerKsef;
                    }
                case PolaEFaktury.OkresFaKorygowanej:
                    {
                        // przy próbie generowania pola OkresFaKorygowanej, które występuje po kolekcji DaneFaKorygowanej czyścimy cache danych faktur korygowanych:
                        _fakturyKorygowane = null;
                        _tabelaVatKorektyZbiorczej = null;
                        if (kontekst.Dokument is DokumentKDS kds && kds.DoNieistniejacego)
                        {
                            IPolaWlasneAdv2Accessor accessor = _uchwyt.UtworzPolaWlasneAdv2Accessor(kontekst.Dokument, createAndAttachIfNull: false);
                            DateTime? dataPoczatkowa = accessor.PobierzWartoscTypuData("Data początkowa");
                            DateTime? dataKoncowa = accessor.PobierzWartoscTypuData("Data końcowa");
                            if (dataPoczatkowa.HasValue && dataKoncowa.HasValue)
                            {
                                return string.Format("{0:yyyy-MM-dd} - {1:yyyy-MM-dd}", dataPoczatkowa.Value, dataKoncowa.Value);
                            }
                        }
                    }
                    break;
            }
            return null;
        }

        private bool CzyKorektaZbiorcza(Dokument dokument) => CzyKorektaZbiorcza(dokument, out _, out _, out _);

        private bool CzyKorektaZbiorcza(Dokument dokument, out DateTime? dataPoczatkowa, out DateTime? dataKoncowa, out decimal? rabat)
        {
            dataPoczatkowa = null;
            dataKoncowa = null;
            rabat = null;
            if (dokument is DokumentKDS kds && kds.DoNieistniejacego)
            {
                IPolaWlasneAdv2Accessor accessor = _uchwyt.UtworzPolaWlasneAdv2Accessor(dokument, createAndAttachIfNull: false);
                dataPoczatkowa = accessor.PobierzWartoscTypuData("Data początkowa");
                dataKoncowa = accessor.PobierzWartoscTypuData("Data końcowa");
                rabat = accessor.PobierzWartoscTypuLiczbaRzeczywista("Rabat");
                if (dataPoczatkowa.HasValue && dataKoncowa.HasValue && rabat.HasValue)
                {
                    if (dokument.Waluta.Id != _idWalutySystemowej.Value)
                    {
                        throw new InvalidOperationException("Korekty zbiorcze można wystawiać tylko w walucie PLN.");
                    }
                    return true;
                }
                return false;
            }
            return false;
        }

        private DaneFakturyKorygowanej PobierzDaneFakturyKorygowanej(Dokument dokument, int index)
        {
            if (_fakturyKorygowane == null)
            {
                if (CzyKorektaZbiorcza(dokument, out DateTime? dataPoczatkowa, out DateTime? dataKoncowa, out _))
                {
                    IDokumentySprzedazy dokumentySprzedazy = _uchwyt.DokumentySprzedazy();
                    _fakturyKorygowane = dokumentySprzedazy.Dane.Wszystkie()
                        .Where(ds => ds.DataWprowadzenia >= dataPoczatkowa.Value
                                    && ds.DataWprowadzenia <= dataKoncowa.Value
                                    && ds.PodmiotId == dokument.PodmiotId)
                        .Select(ds => new DaneFakturyKorygowanej()
                        {
                            Id = ds.Id,
                            DataWystawienia = ds.DataWprowadzenia,
                            Numer = ds.NumerWewnetrzny.PelnaSygnatura,
                            NumerKsef = ds.DokumentyElektroniczne.FirstOrDefault().NumerKSeF
                        })
                        .OrderBy(d => d.Id)
                        .ToList();
                }
                else
                {
                    _fakturyKorygowane = new List<DaneFakturyKorygowanej>();
                }
            }
            if (index < _fakturyKorygowane.Count)
            {
                return _fakturyKorygowane[index];
            }
            return null;
        }

        private List<WierszTabeliVatKorektyZbiorczej> PobierzWierszeTabeliVatKorektyZbiorczej(Dokument dokument)
        {
            if (_tabelaVatKorektyZbiorczej == null)
            {
                if (CzyKorektaZbiorcza(dokument, out DateTime? dataPoczatkowa, out DateTime? dataKoncowa, out decimal? rabat))
                {
                    decimal rabatPrzeliczony = rabat.Value / 100m;
                    IDokumentySprzedazy dokumentySprzedazy = _uchwyt.DokumentySprzedazy();
                    _tabelaVatKorektyZbiorczej = dokumentySprzedazy.Dane.Wszystkie()
                        .Where(ds => ds.DataWprowadzenia >= dataPoczatkowa.Value
                                    && ds.DataWprowadzenia <= dataKoncowa.Value
                                    && ds.PodmiotId == dokument.PodmiotId)
                        .SelectMany(ds => ds.TabeleVat.Concat(ds.TabeleVatDetaliczne))
                        .GroupBy(tv => new { tv.StawkaVat.Stawka, tv.StawkaVat.Zwolniona })
                        .Select(g => new WierszTabeliVatKorektyZbiorczej()
                        {
                            Stawka = g.Key.Stawka,
                            Zwolniona = g.Key.Zwolniona,
                            Netto = -Math.Round(g.Select(tv => tv.WartoscNettoSystemowa).DefaultIfEmpty(0m).Sum() * rabatPrzeliczony, 2) // znak '-' ponieważ podajemy kwotę rabatu
                        })
                        .ToList();
                }
            }
            if (_tabelaVatKorektyZbiorczej == null)
            {
                _tabelaVatKorektyZbiorczej = new List<WierszTabeliVatKorektyZbiorczej>();
            }
            return _tabelaVatKorektyZbiorczej;
        }

        private WierszTabeliVatKorektyZbiorczej PobierzWierszTabeliVatKorektyZbiorczej(Dokument dokument, decimal stawka, bool zwolniona)
        {
            return PobierzWierszeTabeliVatKorektyZbiorczej(dokument).FirstOrDefault(k => k.Stawka == stawka && k.Zwolniona == zwolniona);
        }

        private Guid PobierzIdWalutySystemowej() => _uchwyt.Waluty().DaneDomyslne.PLN.Id;

    }
}
