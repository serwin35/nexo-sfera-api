using InsERT.Moria.Asortymenty;
using InsERT.Moria.Asortymenty.Typy_wyliczeniowe;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Moria.Waluty;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrzykladyKartoteki
{
    public class ObslugaKartotek
    {
		private readonly Uchwyt _sfera;

		public ObslugaKartotek(Uchwyt sfera)
        {
            _sfera = sfera;
        }

        /// <summary>
        /// Przykład pokazujący dodawanie dostawców, producenta oraz odbiorców do asortymentu.
        /// </summary>
        public ObslugaKartotek DodawanieDostawcow()
        {
            IPodmioty podmioty = _sfera.PodajObiektTypu<IPodmioty>();
            IAsortymenty asortymenty = _sfera.PodajObiektTypu<IAsortymenty>();
            IJakosciDostaw jakosciDostaw = _sfera.PodajObiektTypu<IJakosciDostaw>();
            IWaluty waluty = _sfera.PodajObiektTypu<IWaluty>();
            Asortyment encjaBalsam = asortymenty.Dane.Wszystkie().Where(a => a.Symbol == "BANAW200").FirstOrDefault();
            if (encjaBalsam == null)
            {
                Console.WriteLine("Nie znaleziono asortymentu o symbolu BANAW200.");
                return this;
            }
            using (IAsortyment balsam = asortymenty.Znajdz(encjaBalsam))
            {
                Podmiot alegro = podmioty.Dane.Wszystkie().Where(pdm => pdm.NazwaSkrocona == "Drogeria ALEGRO").FirstOrDefault();
                if (alegro != null)
                {
                    // producent może być tylko jeden
                    DaneAsortymentuDlaPodmiotu producentAlegro = balsam.Producent.Ustaw(
                        alegro
                        , 5 // dni
                        , jakosciDostaw.Dane.Wszystkie().Where(j => j.Nazwa == "Wysoka").FirstOrDefault());
                    producentAlegro.CenaDeklarowana = 5m;
                    producentAlegro.WalutaCenyDeklarowanej = waluty.DaneDomyslne.PLN;
                }
                else
                    Console.WriteLine("Nie znaleziono klienta o nazwie 'Drogeria ALEGRO'.");

                Podmiot beatrice = podmioty.Dane.Wszystkie().Where(pdm => pdm.NazwaSkrocona == "Hurtownia kosmetyków BEATRICE").FirstOrDefault();
                if (beatrice != null)
                {
                    // dodanie pierwszego dostawcy automatycznie ustawi go jako "podstawowego dostawcę"
                    DaneAsortymentuDlaPodmiotu dostawcaAla = balsam.Dostawcy.Dodaj(beatrice); // nie ma konieczności podawania czasu i jakości dostawy
                    dostawcaAla.CenaDeklarowana = 8m;
                    dostawcaAla.WalutaCenyDeklarowanej = waluty.DaneDomyslne.PLN;
                    dostawcaAla.RabatDeklarowany = 0.05m;
                    dostawcaAla.RodzajDeklarowanegoRabatu = (byte)RodzajRabatu.Procentowy;
                }
                else
                    Console.WriteLine("Nie znaleziono klienta o nazwie 'Hurtownia kosmetyków BEATRICE'.");

                Podmiot ala = podmioty.Dane.Wszystkie().Where(pdm => pdm.NazwaSkrocona == "Hurtownia ALA").FirstOrDefault();
                if (ala != null)
                {
                    DaneAsortymentuDlaPodmiotu dostawcaAla = balsam.Dostawcy.Dodaj(
                        ala
                        , 3 // dni
                        , jakosciDostaw.Dane.Wszystkie().Where(j => j.Nazwa == "Zadowalająca").FirstOrDefault());
                    dostawcaAla.AsortymentDlaKtoregoDostawcaPodstawowy = balsam.Dane;
                    dostawcaAla.CenaDeklarowana = 8m;
                    dostawcaAla.WalutaCenyDeklarowanej = waluty.DaneDomyslne.PLN;
                    dostawcaAla.RabatDeklarowany = 0.05m;
                    dostawcaAla.RodzajDeklarowanegoRabatu = (byte)RodzajRabatu.Procentowy;
                }
                else
                    Console.WriteLine("Nie znaleziono klienta o nazwie 'Hurtownia ALA'.");

                Podmiot abc = podmioty.Dane.Wszystkie().Where(pdm => pdm.NazwaSkrocona == "ABC s.c.").FirstOrDefault();
                if (abc != null)
                {
                    DaneAsortymentuDlaPodmiotu odbiorcaAbc = balsam.Odbiorcy.Dodaj(abc);
                    odbiorcaAbc.CenaDeklarowana = 10m;
                    odbiorcaAbc.WalutaCenyDeklarowanej = waluty.DaneDomyslne.EUR;
                    odbiorcaAbc.RabatDeklarowany = 1m;
                    odbiorcaAbc.RodzajDeklarowanegoRabatu = (byte)RodzajRabatu.Kwotowy;
                }
                else
                    Console.WriteLine("Nie znaleziono klienta o nazwie 'Hurtownia ALA'.");

                if (balsam.Zapisz())
                    Console.WriteLine("Poprawnie zapisano dostawców i odbiorców dla asortymentu 'BANAW200'.");
                else
                    balsam.WypiszBledy();

            }
            return this;
        }

        /// <summary>
        /// Przykład pokazujący dodawanie dodatkowych jednostek miar do asortymentu z własnymi przelicznikami.
        /// </summary>
        public ObslugaKartotek DodawanieAsortymentuZDodatkowymiJednostkamiMiary()
        {
            IAsortymenty asortymenty = _sfera.PodajObiektTypu<IAsortymenty>();
            ISzablonyAsortymentu szablony = _sfera.PodajObiektTypu<ISzablonyAsortymentu>();
            IJednostkiMiar jednostkiMiar = _sfera.PodajObiektTypu<IJednostkiMiar>();
            JednostkaMiary jm4pak = jednostkiMiar.Dane.Wszystkie().Where(jm => jm.Symbol == "4pak").FirstOrDefault();
            JednostkaMiary polOpakowania = jednostkiMiar.Dane.Wszystkie().Where(jm => jm.Symbol == "0.5op").FirstOrDefault();
            if (jm4pak == null)
            {
                using (IJednostkaMiary jednostkaMiary = jednostkiMiar.Utworz())
                {
                    jednostkaMiary.Dane.Symbol = "4pak";
                    jednostkaMiary.Dane.Nazwa = "4-pak";
                    jednostkaMiary.Dane.SymbolDlaUrzadzeniaFiskalnego = "4pak";
                    jednostkaMiary.Dane.Typ = (int)TypJednostkiMiary.ZalezyOdAsortymentu;
                    jednostkaMiary.Dane.Precyzja = 0;
                    if (jednostkaMiary.Zapisz())
                    {
                        Console.WriteLine("Poprawnie zapisano jednostkę miary 4pak.");
                        jm4pak = jednostkaMiary.Dane;
                    }
                    else
                    {
                        jednostkaMiary.WypiszBledy();
                        return this;
                    }
                }
            }
            if (polOpakowania == null)
            {
                using (IJednostkaMiary jednostkaMiary = jednostkiMiar.Utworz())
                {
                    jednostkaMiary.Dane.Symbol = "0.5op";
                    jednostkaMiary.Dane.Nazwa = "0.5 opakowania";
                    jednostkaMiary.Dane.SymbolDlaUrzadzeniaFiskalnego = "0.5op";
                    jednostkaMiary.Dane.Typ = (int)TypJednostkiMiary.ZalezyOdAsortymentu;
                    jednostkaMiary.Dane.Precyzja = 0;
                    if (jednostkaMiary.Zapisz())
                    {
                        Console.WriteLine("Poprawnie zapisano jednostkę miary 0.5op.");
                        polOpakowania = jednostkaMiary.Dane;
                    }
                    else
                    {
                        jednostkaMiary.WypiszBledy();
                        return this;
                    }
                }
            }
            using (IAsortyment nowyTowar = asortymenty.Utworz())
            {
                JednostkaMiaryAsortymentu nowaJednostka4Pak = null;
                JednostkaMiaryAsortymentu nowaJednostkaPolOpakowania = null;
                PrzelicznikJednostekMiarAsortymentu przelicznik4pak = null;
                PrzelicznikJednostekMiarAsortymentu przelicznikPolOpakowania = null;
                nowyTowar.WypelnijNaPodstawieSzablonu(szablony.DaneDomyslne.Towar);
                nowyTowar.AutoSymbol();
                nowyTowar.Dane.Nazwa = "Nowy towar ze zbiorczymi jednostkami miary.";
                nowaJednostka4Pak = nowyTowar.JednostkiMiary.DodajJednostkeMiary(jm4pak, nowyTowar.Dane.PodstawowaJednostkaMiaryAsortymentu);
                przelicznik4pak = nowaJednostka4Pak.PrzelicznikJednostkiPodrzednej.FirstOrDefault(p => p.JednostkaPodrzedna == nowaJednostka4Pak);
                // ustawiamy przelicznik 1 4pak = 4 szt
                przelicznik4pak.LiczbaJednostkiNadrzednej = 4m;
                przelicznik4pak.LiczbaJednostkiPodrzednej = 1m;
                nowaJednostkaPolOpakowania = nowyTowar.JednostkiMiary.DodajJednostkeMiary(polOpakowania, nowyTowar.Dane.PodstawowaJednostkaMiaryAsortymentu);
                przelicznikPolOpakowania = nowaJednostkaPolOpakowania.PrzelicznikJednostkiPodrzednej.FirstOrDefault(p => p.JednostkaPodrzedna == nowaJednostkaPolOpakowania);
                // ustawiamy przelicznik 1 0.5op = 1/2 szt
                przelicznikPolOpakowania.LiczbaJednostkiNadrzednej = 1m;
                przelicznikPolOpakowania.LiczbaJednostkiPodrzednej = 2m;
                if (nowyTowar.Zapisz())
                    Console.WriteLine("Poprawnie zapisano nowy towar ze zbiorczą jednostką");
                else
                    nowyTowar.WypiszBledy();
            }
			return this;
		}

		/// <summary>
		/// Przykład pokazujący łączenie wybranych asortymentów w grupę zamienników.
		/// </summary>
		public ObslugaKartotek Zamienniki()
        {
            IAsortymenty asortymenty = _sfera.PodajObiektTypu<IAsortymenty>();
            List<Asortyment> grupaPomadki = asortymenty.Dane.Wszystkie().Where(a => a.Grupa != null && a.Grupa.Nazwa == "Pomadki").ToList();
            Guid idGrupyZamiennikow = Guid.NewGuid();
            foreach (Asortyment asortyment in grupaPomadki)
            {
                using (IAsortyment pomadka = asortymenty.Znajdz(asortyment))
                {
                    pomadka.Dane.GrupaZamiennikow = idGrupyZamiennikow;
                    if (pomadka.Zapisz())
                        Console.WriteLine("Poprawnie dodano do grupy zamienników towar o symbolu {0}", pomadka.Dane.Symbol);
                    else
                        pomadka.WypiszBledy();
                }
            }
			return this;
		}

		/// <summary>
		/// Przykład pokazujący dodawanie nowych kontaktów do istniejącego klienta.
		/// </summary>
		public ObslugaKartotek DodawanieKontaktowDoKlienta()
        {
            IPodmioty podmioty = _sfera.PodajObiektTypu<IPodmioty>();
            IRodzajeKontaktu rodzajeKontaktu = _sfera.PodajObiektTypu<IRodzajeKontaktu>();
            Podmiot encjaAbc = podmioty.Dane.Wszystkie().Where(pdm => pdm.NazwaSkrocona == "ABC s.c.").FirstOrDefault();
            using (IPodmiot abc = podmioty.Znajdz(encjaAbc))
            {
                Kontakt faxDodatkowy = new Kontakt();
                abc.Dane.Kontakty.Add(faxDodatkowy);
                faxDodatkowy.Rodzaj = rodzajeKontaktu.DaneDomyslne.Fax;
                faxDodatkowy.Wartosc = "111-222-333";
                Kontakt faxGlowny = new Kontakt();
                abc.Dane.Kontakty.Add(faxGlowny);
                faxGlowny.Rodzaj = rodzajeKontaktu.DaneDomyslne.Fax;
                faxGlowny.Wartosc = "333-222-111";
                faxGlowny.Podstawowy = true; // jeden kontakt każdego typu musi być podstawowy
                if (abc.Zapisz())
                    Console.WriteLine("Poprawnie dodano kontakty dla klienta 'ABC s.c.'.");
                else
                    abc.WypiszBledy();
            }
			return this;
		}

		/// <summary>
		/// Przykład pokazujący dodawanie nowego klienta z wieloma adresami różnych typów.
		/// </summary>
		public ObslugaKartotek DodawanieKlientaZWielomaAdresami()
        {
            IPodmioty podmioty = _sfera.PodajObiektTypu<IPodmioty>();
            ITypyAdresu typyAdresu = _sfera.PodajObiektTypu<ITypyAdresu>();
            using (IPodmiot nowyPodmiot = podmioty.UtworzFirme())
            {
                AdresPodmiotu adresGlowny = null;
                AdresPodmiotu adresKorespondencyjny = null;
                AdresPodmiotu adresDostaw = null;
                nowyPodmiot.AutoSymbol();
                nowyPodmiot.Dane.NazwaSkrocona = "Nowy podmiot";
                nowyPodmiot.Dane.Firma.Nazwa = "Nowy podmiot z wieloma adresami";
                nowyPodmiot.Dane.NIPSformatowany = "888-888-88-88";
                if (nowyPodmiot.Dane.AdresPodstawowy == null)
                    adresGlowny = nowyPodmiot.DodajAdres(typyAdresu.DaneDomyslne.Glowny);
                else
                    adresGlowny = nowyPodmiot.Dane.AdresPodstawowy;
                adresGlowny.Szczegoly.Ulica = "ulica główna";
                adresGlowny.Szczegoly.NrDomu = "1";
                adresGlowny.Szczegoly.NrLokalu = "1";
                adresGlowny.Szczegoly.KodPocztowy = "11-111";
                adresGlowny.Szczegoly.Miejscowosc = "Miasto Główne";
                adresKorespondencyjny = nowyPodmiot.DodajAdres(typyAdresu.DaneDomyslne.Korespondencyjny);
                adresKorespondencyjny.Nazwa = "Do korespondencji";
                adresKorespondencyjny.Szczegoly.Ulica = "ulica korespondencyjna";
                adresKorespondencyjny.Szczegoly.NrDomu = "2";
                adresKorespondencyjny.Szczegoly.NrLokalu = "2";
                adresKorespondencyjny.Szczegoly.KodPocztowy = "22-222";
                adresKorespondencyjny.Szczegoly.Miejscowosc = "Miasto Korespondencyjne";
                adresDostaw = nowyPodmiot.DodajAdres(typyAdresu.DaneDomyslne.DoWysylki);
                adresDostaw.Nazwa = "Do wysyłki";
                adresDostaw.Szczegoly.Ulica = "ulica do wysyłki";
                adresDostaw.Szczegoly.NrDomu = "3";
                adresDostaw.Szczegoly.NrLokalu = "3";
                adresDostaw.Szczegoly.KodPocztowy = "33-333";
                adresDostaw.Szczegoly.Miejscowosc = "Miasto Do Wysyłki";
                if (nowyPodmiot.Zapisz())
                    Console.WriteLine("Poprawnie zapisano nowego klienta z dodatkowymi adresami.");
                else
                    nowyPodmiot.WypiszBledy();
            }
			return this;
		}

		/// <summary>
		/// Przykład pokazujący dodawanie klienta oraz jego oddziału.
		/// </summary>
		public ObslugaKartotek DodawanieKlientaZOddziałem()
        {
            IPodmioty podmioty = _sfera.PodajObiektTypu<IPodmioty>();
            Podmiot encjaOddzial = null;
            using (IPodmiot oddzial = podmioty.UtworzFirme())
            {
                oddzial.AutoSymbol();
                oddzial.Dane.NazwaSkrocona = "Oddział klienta";
                oddzial.Dane.Firma.Nazwa = "Firma oddział klienta";
                oddzial.Dane.NIPSformatowany = "222-222-22-22";
                if (oddzial.Zapisz())
                    Console.WriteLine("Poprawnie zapisano oddział.");
                else
                {
                    oddzial.WypiszBledy();
                    return this;
                }
                encjaOddzial = oddzial.Dane;
            }
            using (IPodmiot centrala = podmioty.UtworzFirme())
            {
                RelacjaPodmiotow powiazanie = new RelacjaPodmiotow();
                centrala.AutoSymbol();
                centrala.Dane.NazwaSkrocona = "Klient centrala";
                centrala.Dane.Firma.Nazwa = "Firma centrala";
                centrala.Dane.NIPSformatowany = "222-222-22-22";
                centrala.Dane.PodmiotyPodrzedne.Add(powiazanie);
                powiazanie.PodmiotPodrzedny = encjaOddzial;
                powiazanie.PodrzednyJestPlatnikiem = false;
                powiazanie.StosujCenyPodrzednego = false;
                if (centrala.Zapisz())
                    Console.WriteLine("Poprawnie zapisano centralę.");
                else
                    centrala.WypiszBledy();
            }
			return this;
		}
	}
}
