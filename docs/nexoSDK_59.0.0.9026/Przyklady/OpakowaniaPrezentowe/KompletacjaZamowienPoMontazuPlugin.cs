using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Dokumenty.Logistyka.Zdarzenia;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpakowaniaPrezentowe
{
    /// <summary>
    /// Kompletuje zamówienia po zmontowaniu opakowania z grawerunkiem.
    /// </summary>
    public class KompletacjaZamowienPoMontazuPlugin : KlientSferyZdarzeniowejDokumentu<IZlecenieProdukcyjneMontowania>
    {
        private const string PierwotnySkutekMagazynowy_Nazwa = "PierwotnySkutekMagazynowy";
        private const string FinalnySkutekMagazynowy_Nazwa = "FinalnySkutekMagazynowy";

        public override void PoInicjalizacjiDokumentu(
            IKontekstInicjalizacjiDokumentu<IZlecenieProdukcyjneMontowania> kontekst)
        {
            if (kontekst.StanObiektu == StanObiektu.Dodawany)
            {
                kontekst.ZmienneKlienta[PierwotnySkutekMagazynowy_Nazwa] = SkutekMagazynowy.Brak;
            }
            else
            {
                kontekst.ZmienneKlienta[PierwotnySkutekMagazynowy_Nazwa] = (SkutekMagazynowy?)kontekst.ObiektBiznesowy.Dokument.StatusDokumentu?.StatusDokumentuPrzyjecia?.SkutekMagazynowyPrzyjecia ?? SkutekMagazynowy.Brak;
            }
        }

        public override void PoZmianieStatusuDokumentu(
            IKontekstZdarzeniaPoZmianieStatusuDokumentu<IZlecenieProdukcyjneMontowania> kontekst)
        {
            kontekst.ZmienneKlienta[FinalnySkutekMagazynowy_Nazwa] = (SkutekMagazynowy?)kontekst.NowyStatus?.StatusDokumentuPrzyjecia?.SkutekMagazynowyPrzyjecia ?? SkutekMagazynowy.Brak;
        }

        public override void PoZapisieObiektu(
            IKontekstZdarzeniaPoZapisieObiektu<IZlecenieProdukcyjneMontowania> kontekst)
        {
            if (CzyZmienionoStatusNaPrzyjecie(kontekst.ZmienneKlienta))
            {
                int zpmId = (int)kontekst.IdDanych;
                KompletujMontowaneZamowienie(kontekst.Uchwyt, zpmId);
            }
        }

        private static void KompletujMontowaneZamowienie(
            IUchwyt uchwyt,
            int zpmId)
        {
            IZamowieniaOdKlientow zamowienia = uchwyt.PodajObiektTypu<IZamowieniaOdKlientow>();
            DokumentZK montowaneZamowienie = ZnajdzZamowienieDoKompletacji(uchwyt, zpmId);

            if (montowaneZamowienie != null)
            {
                (int? kompletId, ICollection<Partia> partieDoKompletacji) = ZnajdzPartieDoKompletacji(uchwyt, zpmId);

                if (partieDoKompletacji?.Count > 0)
                {
                    using (IZamowienieOdKlienta zamowienie = zamowienia.Znajdz(montowaneZamowienie))
                    {
                        IEnumerable<PozycjaDokumentu> pozycjeDoKompletacji = ZnajdzZmontowanePozycje(zamowienie, kompletId.Value);

                        zamowienie.KompletujZamowienie(DateTime.Now, pozycjeDoKompletacji, partieDoKompletacji);

                        if (!zamowienie.Zapisz())
                        {
                            throw new InvalidOperationException($"{zamowienie.Dane.NumerWewnetrzny.PelnaSygnatura}: Kompletacja zamówienia nie powiodła się.");
                        }
                    }
                }
            }
        }

        private static bool CzyZmienionoStatusNaPrzyjecie(
            IZmienneKlientaSferyZdarzeniowej zmienne)
        {
            return zmienne.SprobujPobracWartoscZmiennej(PierwotnySkutekMagazynowy_Nazwa, out SkutekMagazynowy pierwotnySkutek)
                && zmienne.SprobujPobracWartoscZmiennej(FinalnySkutekMagazynowy_Nazwa, out SkutekMagazynowy finalnySkutek)
                && pierwotnySkutek != SkutekMagazynowy.SkutekMagazynowy
                && finalnySkutek == SkutekMagazynowy.SkutekMagazynowy;
        }

        private static DokumentZK ZnajdzZamowienieDoKompletacji(
            IUchwyt uchwyt,
            int zpmId)
        {
            IZamowieniaOdKlientow zamowienia = uchwyt.PodajObiektTypu<IZamowieniaOdKlientow>();

            return zamowienia
                .Dane
                .Wszystkie()
                .FirstOrDefault(z => !z.Zamkniety
                                  && z.StatusDokumentu.SkutekMagazynowyWydania != (byte)SkutekMagazynowy.Rezerwacja
                                  && z.DokumentyRealizujace.Any(d => d.Id == zpmId));
        }

        private static (int? kompletId, ICollection<Partia> partie) ZnajdzPartieDoKompletacji(
            IUchwyt uchwyt,
            int zpmId)
        {
            IZleceniaProdukcyjneMontowania zleceniaMontowania = uchwyt.PodajObiektTypu<IZleceniaProdukcyjneMontowania>();

            var wynik = zleceniaMontowania
                .Dane
                .Wszystkie()
                .Where(z => z.Id == zpmId)
                .Select(z => new
                {
                    KompletId = z.PozycjaKomplet.AsortymentAktualnyId,
                    Partie = z.PozycjaKomplet.PozycjaAutomatyczna.Przyjecie.Partie,
                })
                .FirstOrDefault();

            return (wynik?.KompletId, wynik?.Partie?.ToList());
        }

        private static IEnumerable<PozycjaDokumentu> ZnajdzZmontowanePozycje(
            IZamowienieOdKlienta zamowienie,
            int kompletId)
        {
            return zamowienie
                .Dane
                .Pozycje
                .Where(p => p.AsortymentAktualnyId == kompletId);
        }
    }
}