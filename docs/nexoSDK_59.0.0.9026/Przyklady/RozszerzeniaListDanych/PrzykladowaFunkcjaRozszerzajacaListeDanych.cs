using InsERT.Moria;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Listy;
using InsERT.Moria.Listy.Filtry;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Linq;

namespace RozszerzeniaListDanych
{
    public class PrzykladowaFunkcjaRozszerzajacaListeDanych : IFunkcjaRozszerzajacaListeDanych
    {
        private static readonly Guid AsortymentNiechodliwyId = new Guid("0294F126-E693-4A5D-B5B0-F26BADB3BCA7");

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("97758e0e-1a6c-47d0-b735-9d545bc49d8f");

        public string Nazwa => "Przykładowa funkcja rozszerzająca listę danych";

        public string Opis => "Przykładowa funkcja rozszerzająca listę danych";

        public RozszerzenieListyDanych RozszerzListeDanych(IKontekstListyDanych kontekst)
        {
            var rozszerzenie = new RozszerzenieListyDanych();

            if (kontekst.Identyfikator == AsortymentNiechodliwyId)
            {
                var kontekstBiznesowy = kontekst.Uchwyt.PodajObiektTypu<IKontekstBiznesowy>();

                rozszerzenie.DodajKolumneKwoty<Asortyment>(
                    "Dostępne",
                    a => a.StanyMagazynowe
                          .Where(m => m.Magazyn.Id == kontekstBiznesowy.Magazyn.Id)
                          .Select(m => (decimal?)(m.IloscDostepna - m.IloscZarezerwowanaDostawowo - m.IloscZadysponowana))
                          .FirstOrDefault() ?? 0m,
                    a => a.PodstawowaJednostkaMiaryAsortymentu.Precyzja,
                    a => a.PodstawowaJednostkaMiaryAsortymentu.JednostkaMiary.Symbol);
            }

            if (kontekst.CzyTypDanychJestPrezentowany(typeof(Asortyment)))
            {
                if (kontekst.Rodzaj == RodzajListyDanych.Raport
                    || kontekst.Rodzaj == RodzajListyDanych.Informator)
                {
                    rozszerzenie.DodajKolumneTekstu<Asortyment>(
                        "Opis",
                        a => a.Opis);

                    rozszerzenie.DodajKolumneTekstu<Asortyment>(
                        "Grupa",
                        a => a.Grupa.Nazwa);

                    rozszerzenie.DodajKolumneLogiczna<Asortyment>(
                        "Aktywny",
                        a => !a.IsInRecycleBin);
                }

                if (kontekst.Rodzaj == RodzajListyDanych.Widok
                    || kontekst.Rodzaj == RodzajListyDanych.Lookup)
                {
                    rozszerzenie.DodajKolumneDaty<Asortyment>(
                        "Wyświetlaj komunikat od",
                        a => a.WyswietlajKomunikatOd);

                    rozszerzenie.DodajKolumneLogiczna<Asortyment>(
                        "Wyświetlaj komunikat",
                        a => a.WyswietlajKomunikat);

                    rozszerzenie.DodajKolumneTekstu<Asortyment>(
                        "Treść komunikatu",
                        a => a.TekstKomunikatu);

                    rozszerzenie.DodajKolumneLiczbyCalkowitej<Asortyment>(
                        "Składnik w kompletach",
                        a => a.SkladnikiWKompletach.Count);

                    rozszerzenie.DodajKolumneLiczbyCalkowitej<Asortyment>(
                        "Materiał w usługach",
                        a => a.AsortymentyMaterialy.Count);

                    rozszerzenie.DodajFiltrDaty<Asortyment>(
                        "Data dokumentu",
                        a => a.PozycjeDokumentu.Select(p => p.Dokument.DataWprowadzenia));

                    var kontekstBiznesowy = kontekst.Uchwyt.PodajObiektTypu<IKontekstBiznesowy>();
                    rozszerzenie.DodajFiltrKwoty<Asortyment>(
                        "Dostępne",
                        a => a.StanyMagazynowe
                              .Where(m => m.Magazyn.Id == kontekstBiznesowy.Magazyn.Id)
                              .Select(m => (decimal?)(m.IloscDostepna - m.IloscZarezerwowanaDostawowo - m.IloscZadysponowana))
                              .FirstOrDefault() ?? 0m)
                        .DomyslnieWieksza(0m);

                    rozszerzenie.DodajFiltrTekstu<Asortyment>(
                        "Pełna charakterystyka",
                        a => a.PelnaCharakterystyka)
                        .DomyslnieNiepusty();

                    rozszerzenie.DodajFiltrListyWyboruEncji<Asortyment, MinimalnaMarza>(
                        "Minimalna marża",
                        a => a.MinimalnaMarza)
                        .UstawDomyslnyWybor(1);

                    rozszerzenie.DodajFiltrLiczbyCalkowitej<Asortyment>(
                        "Liczba składników",
                        a => a.SkladnikiKompletu.Count);

                    rozszerzenie.DodajFiltrLogiczny<Asortyment>(
                        "Wyświetlaj komunikat",
                        a => a.WyswietlajKomunikat);

                    rozszerzenie.DodajFiltrWedlugWprowadzonegoPrzedzialuKwot<Asortyment>(
                        "Cena ewidencyjna",
                        "Pomiędzy",
                        przedzial => a => a.CenaEwidencyjna >= przedzial.PoczatekPrzedzialu && a.CenaEwidencyjna <= przedzial.KoniecPrzedzialu,
                        new PrzedzialWartosci<decimal>(0m, 200m));

                    rozszerzenie.DodajFiltrWedlugWprowadzonegoTekstu<Asortyment>(
                        "Opis na pozycji",
                        "zawiera",
                        tekst => a => a.PozycjeDokumentu.Any(p => p.Opis.Contains(tekst)));

                    rozszerzenie.DodajFiltrListyWyboru<Asortyment>(
                        "Asortyment zamówiony",
                        ("Tak", a => a.IlosciDoRealizacji.Any()),
                        ("Nie", a => !a.IlosciDoRealizacji.Any()),
                        ("Na ZK", a => a.IlosciDoRealizacji.Any(i => i.TypDokumentuRealizowanego == (int)TypDokumentu.ZamowienieOdKlienta)),
                        ("Na ZD", a => a.IlosciDoRealizacji.Any(i => i.TypDokumentuRealizowanego == (int)TypDokumentu.ZamowienieDoDostawcy)));
                }
            }

            if (kontekst.CzyTypDanychJestPrezentowany(typeof(DokumentZK)))
            {
                rozszerzenie.DodajFiltrListyWyboru<DokumentZK, Guid>(
                    "Transakcja handlowa",
                    () => kontekst.Uchwyt.PodajObiektTypu<ITransakcjeHandlowe>().Dane
                        .Wszystkie()
                        .Where(t => t.RodzajOperacjiHandlowej == (byte)TypOperacjiHandlowej.Sprzedaz)
                        .OrderBy(t => t.Kolejnosc)
                        .Select(t => new WartoscDoWyboru<Guid> { Wartosc = t.Id, Opis = t.Symbol + " - " + t.Nazwa }),
                    wybrane => d => wybrane.Contains(d.TransakcjaHandlowa.Id));

                rozszerzenie.DodajFiltrWedlugWprowadzonegoDnia<DokumentZK>(
                    "Wprowadzone dnia",
                    "Dnia",
                    data => d => d.DataWprowadzenia == data);

                rozszerzenie.DodajFiltrListyWyboruEncji<DokumentZK, GrupaAsortymentu>(
                    "Grupa asortymentu pozycji",
                    d => d.Pozycje.Where(p => p.AsortymentAktualny != null).Select(p => p.AsortymentAktualny.Grupa));
            }

            if (kontekst.CzyTypDanychJestPrezentowany(typeof(DokumentZD)))
            {
                rozszerzenie.DodajFiltrListyWyboruEncji<DokumentZD, TransakcjaHandlowa>(
                    "Transakcja handlowa",
                    d => d.TransakcjaHandlowa)
                    .FiltrujWartosci(t => t.RodzajOperacjiHandlowej == (byte)TypOperacjiHandlowej.Zakup)
                    .SortujWartosci(t => t.Kolejnosc)
                    .UstawOpisWartosci(t => t.Symbol + " - " + t.Nazwa)
                    .UstawDomyslnyWielokrotnyWybor(new Guid("38201386-F65E-435E-9967-39B228B9BFDA"), new Guid("F2CAE5BD-AC2E-4944-98E6-D0ED89D8F264"));

                rozszerzenie.DodajFiltrWedlugWprowadzonegoPrzedzialuDat<DokumentZD>(
                    "Wprowadzone dnia",
                    "Pomiędzy",
                    przedzial => d => d.DataWprowadzenia >= przedzial.PoczatekPrzedzialu && d.DataWprowadzenia <= przedzial.KoniecPrzedzialu);

                rozszerzenie.DodajFiltrListyWyboruEncji<DokumentZD, GrupaAsortymentu>(
                    "Grupa asortymentu pozycji",
                    d => d.Pozycje.Where(p => p.AsortymentAktualny != null).Select(p => p.AsortymentAktualny.Grupa));
            }

            if (kontekst.CzyTypDanychJestPrezentowany(typeof(Inwentaryzacja)))
            {
                rozszerzenie.DodajFiltrListyWyboruEncji<Inwentaryzacja, StatusDokumentu>(
                    "Status",
                    i => i.StatusInwentaryzacji)
                    .FiltrujWartosci(s => (s.TypyDokumentow & (int)TypDokumentu.Inwentaryzacja) != 0)
                    .SortujWartosci(s => s.Kolejnosc);
            }

            if (kontekst.CzyTypDanychJestPrezentowany(typeof(SpisInwentaryzacyjny)))
            {
                rozszerzenie.DodajFiltrListyWyboruEncji<SpisInwentaryzacyjny, StatusDokumentu>(
                    "Status",
                    i => i.StatusSpisu)
                    .FiltrujWartosci(s => (s.TypyDokumentow & (int)TypDokumentu.SpisInwentaryzacyjny) != 0)
                    .SortujWartosci(s => s.Kolejnosc);
            }

            if (kontekst.CzyTypDanychJestPrezentowany(typeof(DokumentKDS))
                || kontekst.CzyTypDanychJestPrezentowany(typeof(DokumentKDZ)))
            {
                rozszerzenie.DodajFiltrDaty<Dokument>(
                    "Data korygowanego",
                    d => d.DaneKorygowanego.DataKorygowanego)
                    .DomyslnieOstatnie60Dni();
            }

            if (kontekst.CzyTypDanychJestPrezentowany(typeof(Dokument)))
            {
                rozszerzenie.DodajFiltrWedlugWprowadzonejKwoty<Dokument>(
                    "Koszt magazynowy",
                    "większy od",
                    koszt => d => d.KosztMagazynowyTowarowWydanych + d.KosztMagazynowyTowarowPrzyjetych + d.KosztMagazynowyOpakowanPrzyjetych + d.KosztMagazynowyOpakowanWydanych > koszt);

                rozszerzenie.DodajFiltrWedlugWprowadzonejLiczbyCalkowitej<Dokument>(
                    "Liczba pozycji",
                    "większa lub równa",
                    liczba => d => d.Pozycje.Count >= liczba);

                rozszerzenie.DodajFiltryZaawansowanychPolWlasnych<Dokument, Podmiot>(
                    "Klient - ",
                    d => d.Podmiot);

                rozszerzenie.DodajFiltryZaawansowanychPolWlasnych<Dokument, PozycjaDokumentu>(
                    "Pozycja - ",
                    p => p.Pozycje);
            }

            return rozszerzenie;
        }
    }
}