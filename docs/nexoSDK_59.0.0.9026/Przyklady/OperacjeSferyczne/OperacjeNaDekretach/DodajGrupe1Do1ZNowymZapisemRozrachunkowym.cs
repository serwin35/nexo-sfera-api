using InsERT.Moria.KsiegowoscPelna;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozrachunki;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using System;
using System.Linq;

namespace OperacjeSferyczne.OperacjeNaDekretach
{
    public class DodajGrupe1Do1ZNowymZapisemRozrachunkowym : OperacjaWOknieObiektu<IDokumentKsiegowy>
    {
        public override string Nazwa => "Dodaj przykładową grupę 1:1 z pozycją rozrachunkową";

        protected override void Wykonaj(
            IDokumentKsiegowy obiekt,
            IKontekstOknaObiektu kontekstOknaObiektu,
            IKontekstOperacji kontekstOperacji)
        {
            obiekt.PracaZAutomatemBilansujacym = false;

            try
            {
                var nowaGrupa = obiekt.Grupy.Dodaj();

                var definicjaKontaRozrachunkowego = ZnajdzDefinicjeKontaRozrachunkowego(obiekt, kontekstOperacji);
                if (definicjaKontaRozrachunkowego == null)
                {
                    Okna.PokazOknoZBledem(kontekstOperacji.Uchwyt, "Nie znaleziono w planie kont żadnego konta rozrachunkowego dla kartoteki o wzorcu 'Klient standardowy'.");
                    return;
                }

                var definicjaKontaZwyklego = ZnajdzDefinicjeKontaZwyklego(obiekt, kontekstOperacji);
                if (definicjaKontaZwyklego == null)
                {
                    Okna.PokazOknoZBledem(kontekstOperacji.Uchwyt, "Nie znaleziono w planie kont żadnego konta bilansowego nie-rozrachunkowego bez kartoteki.");
                    return;
                }

                var elementKartoteki = ZnajdzPrzykladowyElementKartotekiKlientowStandardowych(obiekt, kontekstOperacji, definicjaKontaRozrachunkowego);
                if (definicjaKontaZwyklego == null)
                {
                    Okna.PokazOknoZBledem(kontekstOperacji.Uchwyt, $"Nie udało się stworzyć/znaleźć przykładowego elementu kartoteki '{definicjaKontaRozrachunkowego.Kartoteka.Nazwa}'.");
                    return;
                }

                var p1 = nowaGrupa.Pozycje.Dodaj(definicjaKontaRozrachunkowego, elementKartoteki);
                p1.Opis = "Przykładowa pozycja rozrachunkowa dodana przez plugin DodajGrupe1Do1ZNowymZapisemRozrachunkowym";

                WykonajZWylaczonymiInterakcjami(kontekstOperacji.Uchwyt, () =>
                {
                    p1.KwotaWinien = 1000;
                });

                foreach (var phr in p1.Rozrachunek.Pozycje)
                    phr.TerminPlatnosci = DateTime.Now.AddDays(30).Date;

                {
                    var p2 = nowaGrupa.Pozycje.Dodaj(definicjaKontaZwyklego);
                    p2.Opis = "Przykładowa pozycja zwykła dodana przez plugin DodajGrupe1Do1ZNowymZapisemRozrachunkowym";
                    p2.KwotaMa = 1000;
                }

                kontekstOknaObiektu.OdswiezZawartosc();
            }
            finally
            {
                obiekt.PracaZAutomatemBilansujacym = true;
            }
        }

        private static DefinicjaKontaKsiegowego ZnajdzDefinicjeKontaZwyklego(IDokumentKsiegowy obiekt, IKontekstOperacji kontekstOperacji)
        {
            return kontekstOperacji.Uchwyt.PodajObiektTypu<IDefinicjeKontaKsiegowego>().Dane.Wszystkie()
                            .Where(dkk => dkk.OkresObrachunkowy_Id == obiekt.Dane.OkresObrachunkowy.Id)
                            .Where(dkk => !dkk.Rozrachunkowe && !dkk.MagazynWalut && (dkk.Typ ?? dkk.KontoSyntetyczne.Typ) == (byte)TypKontaKsiegowego.Bilansowe)
                            .Where(dkk => dkk.Kartoteka_Id == null)
                            .Where(dkk => !dkk.DefinicjePodrzedne.Any())
                            .FirstOrDefault();
        }

        private static DefinicjaKontaKsiegowego ZnajdzDefinicjeKontaRozrachunkowego(IDokumentKsiegowy obiekt, IKontekstOperacji kontekstOperacji)
        {
            return kontekstOperacji.Uchwyt.PodajObiektTypu<IDefinicjeKontaKsiegowego>().Dane.Wszystkie()
                            .Where(dkk => dkk.OkresObrachunkowy_Id == obiekt.Dane.OkresObrachunkowy.Id)
                            .Where(dkk => dkk.Rozrachunkowe && (dkk.Typ ?? dkk.KontoSyntetyczne.Typ) == (byte)TypKontaKsiegowego.Bilansowe)
                            .Where(dkk => dkk.Kartoteka.Wzorzec.Nazwa == "Klienci standardowi")
                            .FirstOrDefault();
        }

        private static ElementKartotekiKsiegowejZwykly ZnajdzPrzykladowyElementKartotekiKlientowStandardowych(
            IDokumentKsiegowy obiekt, 
            IKontekstOperacji kontekstOperacji,
            DefinicjaKontaKsiegowego definicjaKontaKartotekowego)
        {
            var przykladowyPodmiot = kontekstOperacji.Uchwyt.PodajObiektTypu<IElementyKartotekiZrodlowej>().Znajdz(definicjaKontaKartotekowego.Kartoteka)
                .OfType<Podmiot>()
                .Where(p => !(p is PodmiotMojaFirma))
                .Where(p =>
                    !p.ElementyKartotekZrodlowych.Any(ekz => ekz.ElementKartotekiKsiegowejPozostale.Kartoteka.OkresObrachunkowy_Id == obiekt.Dane.OkresObrachunkowy.Id))
                .FirstOrDefault();

            if (przykladowyPodmiot == null)
                return null;

            var ekkZwykle = kontekstOperacji.Uchwyt.PodajObiektTypu<IElementyKartotekiKsiegowejZwykle>();
            var istniejacyEkk = ekkZwykle.Dane.Wszystkie()
                .Where(ekk => ekk.Kartoteka.Id == definicjaKontaKartotekowego.Kartoteka.Id)
                .Where(ekk => ekk.ElementKartotekiZrodlowej.Podmiot_Id == przykladowyPodmiot.Id)
                .FirstOrDefault();

            if (istniejacyEkk == null)
            {
                using (var ekk = ekkZwykle.Utworz(definicjaKontaKartotekowego.Kartoteka, przykladowyPodmiot))
                    ekk.Zapisz();

                istniejacyEkk = ekkZwykle.Dane.Wszystkie()
                .Where(ekk => ekk.Kartoteka.Id == definicjaKontaKartotekowego.Kartoteka.Id)
                .Where(ekk => ekk.ElementKartotekiZrodlowej.Podmiot_Id == przykladowyPodmiot.Id)
                .FirstOrDefault();
            }

            return istniejacyEkk;
        }

        private void WykonajZWylaczonymiInterakcjami(IUchwyt uchwyt, Action akcja)
        {
            bool wyswietlajListeRozrachunkowDoPodlaczenia;
            ModelPracyZRozrachunkami? modelPracyZRozrachunkami = null;
            TrybTworzeniaRozrachunkuZZapisuKsiegowego trybTworzeniaRozrachunkuZZapisuKsiegowego;

            var prkMgr = uchwyt.PodajObiektTypu<IParametryRozrachunkowKsiegowych>();
            using (var prk = prkMgr.Znajdz(prkMgr.Parametr))
            {
                wyswietlajListeRozrachunkowDoPodlaczenia = prk.Dane.WyswietlajListeRozrachunkowDoPodlaczenia;
                trybTworzeniaRozrachunkuZZapisuKsiegowego = (TrybTworzeniaRozrachunkuZZapisuKsiegowego)prk.Dane.TrybTworzeniaRozrachunkuZZapisuKsiegowego;

                if (prk.Dane.ModelPracyZRozrachunkami == (byte)ModelPracyZRozrachunkami.Wspolny)
                {
                    modelPracyZRozrachunkami = (ModelPracyZRozrachunkami)prk.Dane.ModelPracyZRozrachunkami;
                    prk.Dane.ModelPracyZRozrachunkami = (byte)ModelPracyZRozrachunkami.Mieszany;
                }

                prk.Dane.WyswietlajListeRozrachunkowDoPodlaczenia = false;
                prk.Dane.TrybTworzeniaRozrachunkuZZapisuKsiegowego = (byte)TrybTworzeniaRozrachunkuZZapisuKsiegowego.UtworzWTle;
                prk.Zapisz();
            }

            try
            {
                akcja?.Invoke();
            }
            finally
            {
                using (var prk = prkMgr.Znajdz(prkMgr.Parametr))
                {
                    prk.Dane.WyswietlajListeRozrachunkowDoPodlaczenia = wyswietlajListeRozrachunkowDoPodlaczenia;
                    prk.Dane.TrybTworzeniaRozrachunkuZZapisuKsiegowego = (byte)trybTworzeniaRozrachunkuZZapisuKsiegowego;

                    if (modelPracyZRozrachunkami.HasValue)
                        prk.Dane.ModelPracyZRozrachunkami = (byte)modelPracyZRozrachunkami.Value;
                    prk.Zapisz();
                }
            }
        }
    }
}
