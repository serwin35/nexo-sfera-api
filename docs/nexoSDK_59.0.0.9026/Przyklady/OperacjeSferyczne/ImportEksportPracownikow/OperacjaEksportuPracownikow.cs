using InsERT.Moria.Klienci;
using InsERT.Moria.Listy;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using InsERT.Moria.Sfera;
using Microsoft.Win32;
using OperacjeSferyczne.ImportEksportPracownikow.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace OperacjeSferyczne.ImportEksportPracownikow
{
    internal class OperacjaEksportuPracownikow : OperacjaNaLiscieDanych<Pracownik, int>
    {
        protected override string[] SciezkaWMenu => new string[] { "Import/eksport pracowników" };

        public override string Nazwa => "Eksportuj";

        protected override void Wykonaj(IReadOnlyCollection<int> identyfikatoryWybranychElementow, IKontekstListyDanych kontekstListyDanych, IKontekstOperacji kontekstOperacji)
        {
            if (PodajOSciezkeDoPliku(out string sciezka))
            {
                var danePracownikow = PobierzDanePracownikow(kontekstOperacji.Uchwyt, identyfikatoryWybranychElementow);
                var serializer = new XmlSerializer(typeof(DanePracownikow));
                using (var streamWriter = new StreamWriter(sciezka))
                {
                    serializer.Serialize(streamWriter, danePracownikow);
                }
            }
        }

        private DanePracownikow PobierzDanePracownikow(IUchwyt uchwyt, IReadOnlyCollection<int> identyfikatoryPracownikow)
        {
            var pracownicy = uchwyt.Podmioty().Dane.WszyscyPracownicy()
                .Where(x => identyfikatoryPracownikow.Contains(x.Osoba.Pracownik.Id))
                .Select(x => new
                {
                    Podmiot = x,
                    Osoba = x.Osoba,
                    AdresZamieszkania = x.AdresPodstawowy,
                    AdresKorespondencyjny = x.DomyslnyAdresKorespondencyjny,
                    AdresZameldowania = x.DomyslnyAdresZameldowania
                })
                .Select(x => new
                {
                    Imie = x.Osoba.Imie,
                    Nazwisko = x.Osoba.Nazwisko,
                    DataUrodzenia = x.Osoba.DataUrodzenia,
                    Plec = x.Osoba.Plec,
                    Pesel = x.Osoba.PESEL,
                    AdresZamieszkaniaNazwa = x.AdresZamieszkania.Nazwa,
                    AdresZamieszkaniaUlica = x.AdresZamieszkania.Szczegoly.Ulica,
                    AdresZamieszkaniaNrDomu = x.AdresZamieszkania.Szczegoly.NrDomu,
                    AdresZamieszkaniaNrLokalu = x.AdresZamieszkania.Szczegoly.NrLokalu,
                    AdresZamieszkaniaKodPocztowy = x.AdresZamieszkania.Szczegoly.KodPocztowy,
                    AdresZamieszkaniaMiejscowosc = x.AdresZamieszkania.Szczegoly.Miejscowosc,
                    AdresZamieszkaniaPoczta = x.AdresZamieszkania.Szczegoly.Poczta,
                    AdresZamieszkaniaPanstwo = x.AdresZamieszkania.Panstwo.Nazwa,
                    AdresZamieszkaniaWojewodztwo = x.AdresZamieszkania.Szczegoly.Wojewodztwo.Nazwa,
                    AdresZamieszkaniaPowiat = x.AdresZamieszkania.Szczegoly.Gmina.Powiat.Nazwa,
                    AdresZamieszkaniaGminaNazwa = x.AdresZamieszkania.Szczegoly.Gmina.Nazwa,
                    AdresZamieszkaniaGminaTyp = (int?)x.AdresZamieszkania.Szczegoly.Gmina.TypGminy,
                    AdresZamieszkaniaGminaKod = x.AdresZamieszkania.Szczegoly.Gmina.KodGUS,
                    AdresKorespondencyjnyNazwa = x.AdresKorespondencyjny.Nazwa,
                    AdresKorespondencyjnyUlica = x.AdresKorespondencyjny.Szczegoly.Ulica,
                    AdresKorespondencyjnyNrDomu = x.AdresKorespondencyjny.Szczegoly.NrDomu,
                    AdresKorespondencyjnyNrLokalu = x.AdresKorespondencyjny.Szczegoly.NrLokalu,
                    AdresKorespondencyjnyKodPocztowy = x.AdresKorespondencyjny.Szczegoly.KodPocztowy,
                    AdresKorespondencyjnyMiejscowosc = x.AdresKorespondencyjny.Szczegoly.Miejscowosc,
                    AdresKorespondencyjnyPoczta = x.AdresKorespondencyjny.Szczegoly.Poczta,
                    AdresKorespondencyjnyPanstwo = x.AdresKorespondencyjny.Panstwo.Nazwa,
                    AdresKorespondencyjnyWojewodztwo = x.AdresKorespondencyjny.Szczegoly.Wojewodztwo.Nazwa,
                    AdresKorespondencyjnyPowiat = x.AdresKorespondencyjny.Szczegoly.Gmina.Powiat.Nazwa,
                    AdresKorespondencyjnyGminaNazwa = x.AdresKorespondencyjny.Szczegoly.Gmina.Nazwa,
                    AdresKorespondencyjnyGminaTyp = (int?)x.AdresKorespondencyjny.Szczegoly.Gmina.TypGminy,
                    AdresKorespondencyjnyGminaKod = x.AdresKorespondencyjny.Szczegoly.Gmina.KodGUS,
                    AdresZameldowaniaNazwa = x.AdresZameldowania.Nazwa,
                    AdresZameldowaniaUlica = x.AdresZameldowania.Szczegoly.Ulica,
                    AdresZameldowaniaNrDomu = x.AdresZameldowania.Szczegoly.NrDomu,
                    AdresZameldowaniaNrLokalu = x.AdresZameldowania.Szczegoly.NrLokalu,
                    AdresZameldowaniaKodPocztowy = x.AdresZameldowania.Szczegoly.KodPocztowy,
                    AdresZameldowaniaMiejscowosc = x.AdresZameldowania.Szczegoly.Miejscowosc,
                    AdresZameldowaniaPoczta = x.AdresZameldowania.Szczegoly.Poczta,
                    AdresZameldowaniaPanstwo = x.AdresZameldowania.Panstwo.Nazwa,
                    AdresZameldowaniaWojewodztwo = x.AdresZameldowania.Szczegoly.Wojewodztwo.Nazwa,
                    AdresZameldowaniaPowiat = x.AdresZameldowania.Szczegoly.Gmina.Powiat.Nazwa,
                    AdresZameldowaniaGminaNazwa = x.AdresZameldowania.Szczegoly.Gmina.Nazwa,
                    AdresZameldowaniaGminaTyp = (int?)x.AdresZameldowania.Szczegoly.Gmina.TypGminy,
                    AdresZameldowaniaGminaKod = x.AdresZameldowania.Szczegoly.Gmina.KodGUS,
                    Kontakty = x.Podmiot.Kontakty.Select(k => new DaneKontaktu
                    {
                        Wartosc = k.Wartosc,
                        Rodzaj = k.Rodzaj.Nazwa,
                        Podstawowy = k.Podstawowy,
                        Komentarz = k.Komentarz
                    })
                }).ToList()
                .Select(x => new DanePracownika
                {
                    Imie = x.Imie,
                    Nazwisko = x.Nazwisko,
                    DataUrodzenia = x.DataUrodzenia ?? new System.DateTime(1900, 0, 0),
                    Plec = ((Plec)x.Plec).ToString(),
                    Pesel = x.Pesel,
                    AdresZamieszkania = !String.IsNullOrWhiteSpace(x.AdresZamieszkaniaPanstwo)
                        ? new DaneAdresowe()
                        {
                            Nazwa = x.AdresZamieszkaniaNazwa,
                            Ulica = x.AdresZamieszkaniaUlica,
                            NrDomu = x.AdresZamieszkaniaNrDomu,
                            NrLokalu = x.AdresZamieszkaniaNrLokalu,
                            KodPocztowy = x.AdresZamieszkaniaKodPocztowy,
                            Miejscowosc = x.AdresZamieszkaniaMiejscowosc,
                            Poczta = x.AdresZamieszkaniaPoczta,
                            Panstwo = x.AdresZamieszkaniaPanstwo,
                            Wojewodztwo = x.AdresZamieszkaniaWojewodztwo,
                            Powiat = x.AdresZamieszkaniaPowiat,
                            Gmina = !String.IsNullOrWhiteSpace(x.AdresZamieszkaniaGminaNazwa) && x.AdresZamieszkaniaGminaTyp.HasValue
                                ? new DaneGminy()
                                {
                                    Nazwa = x.AdresZamieszkaniaGminaNazwa,
                                    TypGminy = ((TypGminy)x.AdresZamieszkaniaGminaTyp).ToString(),
                                    Kod = x.AdresZamieszkaniaGminaKod
                                } : null
                        } : null,
                    AdresKorespondencyjny = !String.IsNullOrWhiteSpace(x.AdresKorespondencyjnyPanstwo)
                        ? new DaneAdresowe()
                        {
                            Nazwa = x.AdresKorespondencyjnyNazwa,
                            Ulica = x.AdresKorespondencyjnyUlica,
                            NrDomu = x.AdresKorespondencyjnyNrDomu,
                            NrLokalu = x.AdresKorespondencyjnyNrLokalu,
                            KodPocztowy = x.AdresKorespondencyjnyKodPocztowy,
                            Miejscowosc = x.AdresKorespondencyjnyMiejscowosc,
                            Poczta = x.AdresKorespondencyjnyPoczta,
                            Panstwo = x.AdresKorespondencyjnyPanstwo,
                            Wojewodztwo = x.AdresKorespondencyjnyWojewodztwo,
                            Powiat = x.AdresKorespondencyjnyPowiat,
                            Gmina = !String.IsNullOrWhiteSpace(x.AdresKorespondencyjnyGminaNazwa) && x.AdresKorespondencyjnyGminaTyp.HasValue
                                ? new DaneGminy()
                                {
                                    Nazwa = x.AdresKorespondencyjnyGminaNazwa,
                                    TypGminy = ((TypGminy)x.AdresKorespondencyjnyGminaTyp).ToString(),
                                    Kod = x.AdresKorespondencyjnyGminaKod
                                } : null
                        } : null,
                    AdresZameldowania = !String.IsNullOrWhiteSpace(x.AdresZameldowaniaPanstwo)
                        ? new DaneAdresowe()
                        {
                            Nazwa = x.AdresZameldowaniaNazwa,
                            Ulica = x.AdresZameldowaniaUlica,
                            NrDomu = x.AdresZameldowaniaNrDomu,
                            NrLokalu = x.AdresZameldowaniaNrLokalu,
                            KodPocztowy = x.AdresZameldowaniaKodPocztowy,
                            Miejscowosc = x.AdresZameldowaniaMiejscowosc,
                            Poczta = x.AdresZameldowaniaPoczta,
                            Panstwo = x.AdresZameldowaniaPanstwo,
                            Wojewodztwo = x.AdresZameldowaniaWojewodztwo,
                            Powiat = x.AdresZameldowaniaPowiat,
                            Gmina = !String.IsNullOrWhiteSpace(x.AdresZameldowaniaGminaNazwa) && x.AdresZameldowaniaGminaTyp.HasValue
                                ? new DaneGminy()
                                {
                                    Nazwa = x.AdresZameldowaniaGminaNazwa,
                                    TypGminy = ((TypGminy)x.AdresZameldowaniaGminaTyp).ToString(),
                                    Kod = x.AdresZameldowaniaGminaKod
                                } : null
                        } : null,
                    Kontakty = x.Kontakty.ToList()
                }).ToList();

            return new DanePracownikow()
            {
                Pracownicy = pracownicy
            };
        }


        private bool PodajOSciezkeDoPliku(out string sciezka)
        {
            var sfd = new SaveFileDialog();
            sfd.Filter = "Plik XML (*.xml) | *.xml";
            sfd.Title = "Zapisz plik na dysku";

            var result = sfd.ShowDialog();

            if (result == true)
            {
                sciezka = sfd.FileName;
                return true;
            }
            else
            {
                sciezka = null;
                return false;
            }
        }
    }
}
