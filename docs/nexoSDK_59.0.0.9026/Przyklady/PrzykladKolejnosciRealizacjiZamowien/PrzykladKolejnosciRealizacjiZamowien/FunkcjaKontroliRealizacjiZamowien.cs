using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrzykladKolejnosciRealizacjiZamowien
{
    public class FunkcjaKontroliRealizacjiZamowien : IFunkcjaKontroliRealizacjiZamowien
    {
        private readonly IZamowieniaOdKlientow _zamowienia;

        public FunkcjaKontroliRealizacjiZamowien(IZamowieniaOdKlientow zamowienia)
        {
            _zamowienia = zamowienia;
        }

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("b1ac0777-4e83-41c3-bf42-0c880848d0a3");

        public string Nazwa => "Wg kolejności wprowadzania";

        public string Opis => "Zamówienia są realizowane wg kolejności wprowadzania bez względu na datę, termin ważności oraz status zamówienia";

        public decimal PodajIloscMozliwaDoRealizacji(PozycjaDokumentu pozycjaZamowienia, out IEnumerable<IPozycjaZamowieniaBlokujacaRealizacje> dokumentyBlokujace)
        {
            List<IPozycjaZamowieniaBlokujacaRealizacje> blokujace = new List<IPozycjaZamowieniaBlokujacaRealizacje>();

            decimal iloscMozliwaDoRealizacji = pozycjaZamowienia.IloscWJednostceBazowej;

            string jmPodstawowa = pozycjaZamowienia.AsortymentAktualny.PodstawowaJednostkaMiaryAsortymentu.Symbol();
            int precyzjaIlosci = pozycjaZamowienia.AsortymentAktualny.PodstawowaJednostkaMiaryAsortymentu.PrecyzjaWlasciwa() ?? 3;
            var pozycjeZamowien = _zamowienia.Dane.Wszystkie().SelectMany(z => z.Pozycje);
            var pozycjeBlokujace = (from pozycja in pozycjeZamowien
                                    let dokumentZK = pozycja.Dokument as DokumentZK
                                    where pozycja.AsortymentAktualnyId == pozycjaZamowienia.AsortymentAktualnyId
                                           && dokumentZK.Zamkniety == false
                                           && (pozycjaZamowienia.Id == 0 || pozycja.Id < pozycjaZamowienia.Id)
                                    select new PozycjaBlokujaca()
                                    {
                                        DataWystawienia = dokumentZK.DataWprowadzenia,
                                        IloscZamowiona = pozycja.IloscWJednostceBazowej - (pozycja.StanRealizacjiZamowienia.ZrealizowanaIlosc ?? 0m),
                                        PozycjaId = pozycja.Id,
                                        LpPozycji = pozycja.LP,
                                        NumerDokumentu = dokumentZK.NumerWewnetrzny.PelnaSygnatura,
                                        Numer = dokumentZK.NumerWewnetrzny.Numer,
                                        NumerDokumentuPo = dokumentZK.NumerWewnetrzny.SygnaturaPoNr,
                                        NumerDokumentuPrzed = dokumentZK.NumerWewnetrzny.SygnaturaPrzedNr,
                                        StatusDokumentu = dokumentZK.StatusDokumentu.Mnemonik,
                                        Wystawil = dokumentZK.Wystawil,
                                        TerminRealizacji = pozycja.Termin ?? dokumentZK.TerminRealizacji,
                                        Zamawiajacy = dokumentZK.Podmiot.NazwaSkrocona
                                    }).ToArray();
            int kolejnosc = 1;
            decimal iloscBlokujaca = 0m;
            foreach (PozycjaBlokujaca blokujaca in pozycjeBlokujace.OrderBy(p => p.PozycjaId))
            {
                blokujaca.Kolejnosc = kolejnosc++;
                blokujaca.JmPodstawowa = jmPodstawowa;
                blokujaca.PrecyzjaIlosci = precyzjaIlosci;
                blokujaca.SymbolAsortymentu = pozycjaZamowienia.AsortymentAktualny.Symbol;
                blokujaca.NazwaAsortymentu = pozycjaZamowienia.AsortymentAktualny.Nazwa;
                iloscBlokujaca += blokujaca.IloscZamowiona;
                blokujace.Add(blokujaca);
            }
            iloscMozliwaDoRealizacji = Math.Max(0m, iloscMozliwaDoRealizacji - iloscBlokujaca);
            dokumentyBlokujace = blokujace;
            return iloscMozliwaDoRealizacji;
        }
    }
}