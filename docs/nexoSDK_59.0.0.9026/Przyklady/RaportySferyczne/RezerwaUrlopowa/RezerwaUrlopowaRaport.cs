using InsERT.Moria;
using Kadry = InsERT.Moria.Kadry;
using InsERT.Moria.Kadry.Duze;
using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Place.Duze;
using InsERT.Moria.Raporty;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;
using InsERT.Moria.Parametry;

namespace RaportySferyczne.RezerwaUrlopowa
{
    /// <summary>
    /// W poniższym przykładzie zastosowano następujące założenia:
    /// - Uwzględniane są wartości finalne składników wypłaconych w okresie październik - grudzień wchodzące do podstawy wynagrodzenia urlopowego.
    /// - Liczba godzin przepracowanych pobierana jest z okresu październik - grudzień niezależnie od tego czy wynagrodzenia wypłacane są w bieżącym czy następnym miesiącu.
    /// </summary>
    public partial class RezerwaUrlopowaRaport : IDaneRaportu
    {
        private const string PARAMETR_ROK = "Rok";
        private const string PARAMETR_METODA = "Metoda";
        private const int METODA_SZCZEGOLOWA = 1;
        private const int METODA_UPROSZCZONA = 2;

        private readonly IDataSystemowa _dataSystemowa;
        private readonly IMenadzerWymiaruUrlopuPracownika _menadzerWymiaruUrlopuPracownika;
        private readonly IWyplaty _menadzerWyplat;
        private readonly IUmowyPracownicze _menadzerUmow;
        private readonly ISumaryczneECP _menadzerSumarycznychECP;
        private readonly IParametry _menadzerParametrow;

        public RezerwaUrlopowaRaport(
            IDataSystemowa dataSystemowa,
            IMenadzerWymiaruUrlopuPracownika menadzerWymiaruUrlopuPracownika,
            IWyplaty menadzerWyplat,
            IUmowyPracownicze menadzerUmow,
            ISumaryczneECP menadzerSumarycznychECP,
            IParametry menadzerParametrow)
        {
            _dataSystemowa = dataSystemowa;
            _menadzerWymiaruUrlopuPracownika = menadzerWymiaruUrlopuPracownika;
            _menadzerWyplat = menadzerWyplat;
            _menadzerUmow = menadzerUmow;
            _menadzerSumarycznychECP = menadzerSumarycznychECP;
            _menadzerParametrow = menadzerParametrow;
        }

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("da36e52d-dd3f-4292-ab0f-4a847c458775");

        public string Nazwa => "Rezerwa urlopowa";

        public string Opis => "Raport sferyczny wyliczający rezerwę urlopową na podstawie trzech ostatnich miesięcy poprzedniego roku.";

        public Type TypWyniku => typeof(RezerwaUrlopowaDane);

        public Type TypGlownegoElementu => typeof(PracownikGr);

        public void DefiniujParametryRaportu(IBudowniczyParametrowRaportu fabrykaParametrowRaportu)
        {
            fabrykaParametrowRaportu.DodajLiczbowy(PARAMETR_ROK, PARAMETR_ROK, 0, _dataSystemowa.KontekstDaty().Year);
            fabrykaParametrowRaportu.Dodaj(DefinicjaParametru.UtworzParametrLista(PARAMETR_METODA, PARAMETR_METODA)
                .NieZezwalajNaBrakWartosci()
                .DodajElementListy(METODA_SZCZEGOLOWA, "Szczegółowa")
                .DodajElementListy(METODA_UPROSZCZONA, "Uproszczona")
                .UstawDomyslnieWybranyElement(METODA_SZCZEGOLOWA));
        }

        public IQueryable PodajDaneRaportu(IParametryDanychRaportu parametryDanychRaportu)
        {
            var rok = (int?)parametryDanychRaportu.PodajWartoscParametruLiczbowego(PARAMETR_ROK);
            var metoda = parametryDanychRaportu.PodajWartoscParametruLista(PARAMETR_METODA);

            if (!rok.HasValue || !metoda.HasValue)
                return Enumerable.Empty<RezerwaUrlopowaDane>().AsQueryable();

            var poczatekRoku = new DateTime(rok.Value, 1, 1);
            var koniecRoku = new DateTime(rok.Value, 12, 31);
            var pracownicy = _menadzerUmow.Dane.Wszystkie()
                .AktualneWPrzedziale(poczatekRoku, koniecRoku)
                .GroupBy(x => x.Pracownik.Id)
                .Select(x => x.FirstOrDefault().Pracownik)
                .ToList();

            var poczatekPazdziernika = new DateTime(rok.Value - 1, 10, 1);
            var koniecGrudnia = new DateTime(rok.Value - 1, 12, 31);
            var daneWynagrodzen = PobierzDaneWynagrodzen(poczatekPazdziernika, koniecGrudnia);
            var daneECP = PobierzDaneECP(poczatekPazdziernika, koniecGrudnia);
            var procentSkladekZUS = ProcentSkladekZUSPracodawcy(poczatekRoku);

            var wynik = new List<RezerwaUrlopowaDane>();

            if (metoda == METODA_SZCZEGOLOWA)
            {
                foreach (var pracownik in pracownicy)
                {
                    var dane = new RezerwaUrlopowaDane();
                    dane.Id = pracownik.Osoba.Podmiot.Id;
                    dane.Imie = pracownik.Osoba.Imie;
                    dane.Nazwisko = pracownik.Osoba.Nazwisko;

                    var wymiarUrlopuPracownikaPodstawowy = _menadzerWymiaruUrlopuPracownika.Pobierz(pracownik.PracownikGr, rok.Value, false);
                    var wymiarUrlopuPracownikaDodatkowy = _menadzerWymiaruUrlopuPracownika.Pobierz(pracownik.PracownikGr, rok.Value, true);

                    dane.GodzinNiewykorzystanegoUrlopu = Math.Max(0M, ((decimal)(wymiarUrlopuPracownikaPodstawowy.Zalegly + wymiarUrlopuPracownikaDodatkowy.Zalegly) / 60).Round());
                    dane.GodzinNiewykorzystanegoUrlopuPrecyzja = Precyzja(dane.GodzinNiewykorzystanegoUrlopu);
                    dane.GodzinPrzepracowanych = daneECP.GetValueOrDefault(pracownik.Id)?.Sum(x => x.GodzinPrzepracowanych) ?? 0M;
                    dane.GodzinPrzepracowanychPrecyzja = Precyzja(dane.GodzinPrzepracowanych);
                    dane.WynagrodzenieOtrzymane = daneWynagrodzen.GetValueOrDefault(pracownik.Id)?.Sum(x => x.Wartosc) ?? 0M;

                    dane.StawkaGodzinowa = dane.GodzinPrzepracowanych == 0M ? 0M : (dane.WynagrodzenieOtrzymane / dane.GodzinPrzepracowanych).Round();
                    dane.PrognozowaneWynagrodzenie = (dane.StawkaGodzinowa * dane.GodzinNiewykorzystanegoUrlopu).Round();
                    dane.ProcentSkladekZUS = procentSkladekZUS * 100;
                    dane.SkladkiZUS = (procentSkladekZUS * dane.PrognozowaneWynagrodzenie).Round();
                    dane.RezerwaUrlopowa = dane.PrognozowaneWynagrodzenie + dane.SkladkiZUS;

                    wynik.Add(dane);
                }
            }
            else
            {
                var dane = new RezerwaUrlopowaDane();
                dane.Id = 0;
                dane.Imie = "-";
                dane.Nazwisko = "-";

                foreach (var pracownik in pracownicy)
                {
                    var wymiarUrlopuPracownikaPodstawowy = _menadzerWymiaruUrlopuPracownika.Pobierz(pracownik.PracownikGr, rok.Value, false);
                    var wymiarUrlopuPracownikaDodatkowy = _menadzerWymiaruUrlopuPracownika.Pobierz(pracownik.PracownikGr, rok.Value, true);
                    dane.GodzinNiewykorzystanegoUrlopu += ((decimal)(wymiarUrlopuPracownikaPodstawowy.Zalegly + wymiarUrlopuPracownikaDodatkowy.Zalegly) / 60).Round();
                    dane.GodzinNiewykorzystanegoUrlopuPrecyzja = Precyzja(dane.GodzinNiewykorzystanegoUrlopu);
                }
                dane.GodzinPrzepracowanych = daneECP.Values.Sum(x => x.Sum(ecp => ecp.GodzinPrzepracowanych));
                dane.GodzinPrzepracowanychPrecyzja = Precyzja(dane.GodzinPrzepracowanych);
                dane.WynagrodzenieOtrzymane = daneWynagrodzen.Values.Sum(x => x.Sum(w => w.Wartosc));

                dane.StawkaGodzinowa = dane.GodzinPrzepracowanych == 0M ? 0M : (dane.WynagrodzenieOtrzymane / dane.GodzinPrzepracowanych).Round();
                dane.PrognozowaneWynagrodzenie = (dane.StawkaGodzinowa * dane.GodzinNiewykorzystanegoUrlopu).Round();
                dane.ProcentSkladekZUS = procentSkladekZUS * 100;
                dane.SkladkiZUS = (procentSkladekZUS * dane.PrognozowaneWynagrodzenie).Round();
                dane.RezerwaUrlopowa = dane.PrognozowaneWynagrodzenie + dane.SkladkiZUS;

                wynik.Add(dane);
            }

            return wynik.AsQueryable();
        }

        private IDictionary<int, IEnumerable<RezerwaUrlopowaDaneWynagrodzenia>> PobierzDaneWynagrodzen(DateTime dataPoczatkowa, DateTime dataKoncowa)
        {
            return _menadzerWyplat.Dane.Wszystkie()
                .Where(x => x.ListaPlac.DataWyplaty >= dataPoczatkowa && x.ListaPlac.DataWyplaty <= dataKoncowa)
                .Select(x => new RezerwaUrlopowaDaneWynagrodzenia
                {
                    IdentyfikatorPracownika = x.Umowa.Pracownik.Id,
                    Wartosc = x.WartosciSladnikowPlacowychWynagrodzenia
                        .OfType<WartoscSkladnikaPlacowegoWynagrodzeniaGr>()
                        .Where(w => w.SkladnikPlacowyGr != null && w.SkladnikPlacowyGr.SposobWchodzeniaDoPodstawyWynagrodzeniaUrlopowego != (byte)SposobWchodzeniaDoPodstawyWynagrodzeniaUrlopowego.Nie)
                        .Select(w => w.Wartosc)
                        .DefaultIfEmpty(0M)
                        .Sum()
                })
                .GroupBy(x => x.IdentyfikatorPracownika)
                .ToDictionary(x => x.Key, x => x.AsEnumerable());
        }

        private IDictionary<int, IEnumerable<RezerwaUrlopowaDaneECP>> PobierzDaneECP(DateTime dataPoczatkowa, DateTime dataKoncowa)
        {
            return _menadzerSumarycznychECP.Dane.Wszystkie()
                .Where(x => x.Miesiac >= dataPoczatkowa && x.Miesiac <= dataKoncowa && x.Umowa.Typ == (byte)Kadry.TypUmowyPracowniczej.OPrace)
                .Select(x => new RezerwaUrlopowaDaneECP
                {
                    IdentyfikatorPracownika = x.Umowa.Pracownik.Id,
                    GodzinPrzepracowanych = (decimal)x.GodzinPrzepracowanych.Ticks / TimeSpan.TicksPerHour
                })
                .GroupBy(x => x.IdentyfikatorPracownika)
                .ToDictionary(x => x.Key, x => x.AsEnumerable());
        }

        private int? _stawkaFPId;
        public int StawkaFPId
        {
            get
            {
                if (!_stawkaFPId.HasValue)
                {
                    _stawkaFPId = _menadzerParametrow.DaneDomyslne.SkladkaFP.Id;
                }

                return _stawkaFPId.Value;
            }
        }

        private int? _stawkaFSId;
        public int StawkaFSId
        {
            get
            {
                if (!_stawkaFSId.HasValue)
                {
                    _stawkaFSId = _menadzerParametrow.DaneDomyslne.SkladkaFS.Id;
                }

                return _stawkaFSId.Value;
            }
        }


        private int? _stawkaFGSPId;
        public int StawkaFGSPId
        {
            get
            {
                if (!_stawkaFGSPId.HasValue)
                {
                    _stawkaFGSPId = _menadzerParametrow.DaneDomyslne.SkladkaFGSP.Id;
                }

                return _stawkaFGSPId.Value;
            }
        }

        private int? _stawkaEmId;
        public int StawkaEmId
        {
            get
            {
                if (!_stawkaEmId.HasValue)
                {
                    _stawkaEmId = _menadzerParametrow.DaneDomyslne.SkladkaEmerytalna.Id;
                }

                return _stawkaEmId.Value;
            }
        }

        private int? _stawkaReId;
        public int StawkaReId
        {
            get
            {
                if (!_stawkaReId.HasValue)
                {
                    _stawkaReId = _menadzerParametrow.DaneDomyslne.SkladkaRentowaPracodawca.Id;
                }

                return _stawkaReId.Value;
            }
        }


        private int? _stawkaWyId;
        public int StawkaWyId
        {
            get
            {
                if (!_stawkaWyId.HasValue)
                {
                    _stawkaWyId = _menadzerParametrow.DaneDomyslne.SkladkaWypadkowaPracownik.Id;
                }

                return _stawkaWyId.Value;
            }
        }

        private decimal WartoscParametru(int id, DateTime data)
        {
            return _menadzerParametrow.Dane.Wszystkie().Where(x => x.Id == id)
                .SelectMany(x => x.WartosciParametru)
                .Where(x => x.WaznaOd <= data)
                .OrderByDescending(x => x.WaznaOd)
                .FirstOrDefault()?.Wartosc ?? 0M;
        }

        private decimal ProcentSkladekZUSPracodawcy(DateTime data)
        {
            return WartoscParametru(StawkaEmId, data) / 2
                + WartoscParametru(StawkaReId, data)
                + WartoscParametru(StawkaWyId, data)
                + WartoscParametru(StawkaFPId, data)
                + WartoscParametru(StawkaFSId, data)
                + WartoscParametru(StawkaFGSPId, data);
        }

        private int Precyzja(decimal wartosc)
        {
            if (wartosc % 1 == 0)
                return 0;
            else if (wartosc % 0.1M == 0)
                return 1;
            else
                return 2;
        }
    }
}