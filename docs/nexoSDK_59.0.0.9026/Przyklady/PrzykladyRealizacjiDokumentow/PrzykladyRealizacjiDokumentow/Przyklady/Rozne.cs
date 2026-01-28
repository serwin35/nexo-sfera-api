using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealizacjeDokumentow.Przyklady
{
    public class Rozne : RealizacjaBase
    {
        public Rozne(Uchwyt sfera) : base(sfera) { }

        private DokumentZK DodajZamowienieZUstawionaOsobaPrzedstawiciela(string numerZewnetrzny = null)
        {
            Console.WriteLine($"Dodawanie zamówienia. Symbol klienta: KSAVON.");
            IZamowieniaOdKlientow zamowienia = _sfera.ZamowieniaOdKlientow();
            int? identyfikatorZamowienia = null;
            using (IZamowienieOdKlienta zamowienie = zamowienia.UtworzZamowienieOdKlienta())
            {
                zamowienie.PodmiotyDokumentu.UstawZamawiajacegoWedlugSymbolu("KSAVON");
                zamowienie.Dane.OdebralaOsoba = zamowienie.Dane.Podmiot.Firma.PrzedstawicielGlowny.Osoba;
                if (!string.IsNullOrEmpty(numerZewnetrzny))
                {
                    zamowienie.Dane.NumerZewnetrzny = numerZewnetrzny;
                    zamowienie.Dane.DataWydaniaWystawienia = zamowienie.Dane.DataWprowadzenia.AddDays(-1);
                }
                zamowienie.Pozycje.Dodaj("DZFOREVER");
                zamowienie.Przelicz();
                Console.WriteLine($"Zapis zamówienia. Liczba pozycji: {zamowienie.Dane.Pozycje.Count}.");
                if (zamowienie.Zapisz())
                {
                    Console.WriteLine($"Poprawnie zapisano zamówienie nr {zamowienie.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                    identyfikatorZamowienia = zamowienie.Dane.Id;
                }
                else
                {
                    zamowienie.WypiszBledy();
                }
            }
            if (identyfikatorZamowienia.HasValue)
            {
                DokumentZK utworzoneZamowienie = zamowienia.Dane.Pierwszy(zk => zk.Id == identyfikatorZamowienia.Value);
                if (utworzoneZamowienie == null)
                    throw new InvalidOperationException($"Nie udało się odnaleźć zapisanego zamówienia o identyfikatorze {identyfikatorZamowienia.Value}");
                return utworzoneZamowienie;
            }
            return null;
        }

        public void RealizacjaZamowieniaZUstawionaOsobaPrzedstawiciela_DokumentSprzedazy()
        {
            DokumentZK zamowienie = DodajZamowienieZUstawionaOsobaPrzedstawiciela();
            Console.WriteLine("Realizacja zamówienia z ustawioną osobą przedstawiciela do faktury sprzedaży.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                faktura.Dane.Uwagi = "Dokument realizuje jedno zamówienie z ustawioną osobą przedstawiciela od klienta bez konsolidacji pozycji.";
                _ = ZapiszDokumentSprzedazy(faktura);
            }
        }


        public void RealizacjaZamowieniaZUstawionaOsobaPrzedstawiciela_WydanieZewnetrzne()
        {
            DokumentZK zamowienie = DodajZamowienieZUstawionaOsobaPrzedstawiciela();
            Console.WriteLine("Realizacja zamówienia z ustawioną osobą przedstawiciela do wydania zewnętrznego.");
            using (IWydanieZewnetrzne wydanie = _sfera.WydaniaZewnetrzne().UtworzWydanieZewnetrzne())
            {
                wydanie.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                wydanie.Dane.Uwagi = "Dokument realizuje jedno zamówienie z ustawioną osobą przedstawiciela od klienta bez konsolidacji pozycji.";
                _ = ZapiszWydanieZewnetrzne(wydanie);
            }
        }

        public void RealizacjaZamowieniaZUstawionaOsobaPrzedstawicielaINumeremOryginalu_DokumentSprzedazy()
        {
            DokumentZK zamowienie = DodajZamowienieZUstawionaOsobaPrzedstawiciela("ZAM 2/2023");
            Console.WriteLine("Realizacja zamówienia z ustawioną osobą przedstawiciela do faktury sprzedaży.");
            using (IDokumentSprzedazy faktura = _sfera.DokumentySprzedazy().UtworzFaktureSprzedazy())
            {
                faktura.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                faktura.Dane.Uwagi = "Dokument realizuje jedno zamówienie z ustawioną osobą przedstawiciela od klienta bez konsolidacji pozycji.";
                _ = ZapiszDokumentSprzedazy(faktura);
            }
        }


        public void RealizacjaZamowieniaZUstawionaOsobaPrzedstawicielaINumeremOryginalu_WydanieZewnetrzne()
        {
            DokumentZK zamowienie = DodajZamowienieZUstawionaOsobaPrzedstawiciela("ZAM 1/2023");
            Console.WriteLine("Realizacja zamówienia z ustawioną osobą przedstawiciela do wydania zewnętrznego.");
            using (IWydanieZewnetrzne wydanie = _sfera.WydaniaZewnetrzne().UtworzWydanieZewnetrzne())
            {
                wydanie.WypelnijNaPodstawieZK(new[] { zamowienie }, zamowienie, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji
                });
                wydanie.Dane.Uwagi = "Dokument realizuje jedno zamówienie z ustawioną osobą przedstawiciela od klienta bez konsolidacji pozycji.";
                _ = ZapiszWydanieZewnetrzne(wydanie);
            }
        }
    }
}
