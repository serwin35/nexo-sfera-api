using InsERT.Moria.Asortymenty;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Inwentaryzacja;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.ModelOrganizacyjny;
using InsERT.Moria.Sfera;
using System;
using System.Collections.Generic;
using System.Linq;

namespace InwentaryzacjaPrzyklady
{
    public class ObslugaInwentaryzacji
    {
        private readonly Uchwyt sfera;


		public ObslugaInwentaryzacji(Uchwyt sfera)
        {
            this.sfera = sfera;
        }


        /// <summary>
        /// Metoda tworzy spis inwentaryzacyjny dla 4 wybranych towarów z ilością ustawioną na bieżący stan magazynowy.
        /// </summary>
        public ObslugaInwentaryzacji TworzenieSpisuInwentaryzacyjnegoDlaWybranychTowarow()
        {
            ISpisyInwentaryzacyjne spisyInwentaryzacjne = sfera.PodajObiektTypu<ISpisyInwentaryzacyjne>();
            IKonfiguracje konfiguracje = sfera.PodajObiektTypu<IKonfiguracje>();
            IAsortymenty asortymenty = sfera.PodajObiektTypu<IAsortymenty>();
            IMagazyny magazyny = sfera.PodajObiektTypu<IMagazyny>();
            ICentrale centrale = sfera.PodajObiektTypu<ICentrale>();
            IStatusyDokumentow statusy = sfera.PodajObiektTypu<IStatusyDokumentow>();

            Konfiguracja konfiguracjaIs = konfiguracje.DaneDomyslne.SpisInwentaryzacyjny;
            Magazyn glowny = magazyny.Dane.Wszystkie().Where(m => m.Symbol == "MAG").FirstOrDefault();
            Centrala centrala = centrale.Dane.Wszystkie().FirstOrDefault();

            List<string> symboleTowarow = new List<string>()
                {
                    "BANAW200",
                    "DZSO100",
                    "POYAR01",
                    "ZELAQUA"
                };

            using (ISpisInwentaryzacyjny spis = spisyInwentaryzacjne.Utworz(konfiguracjaIs))
            {
                // podstawowe dane:
                spis.Dane.Magazyn = glowny;
                spis.Dane.MiejsceWprowadzenia = centrala;

                // dodawanie pozycji:
                foreach (string symbol in symboleTowarow)
                {
                    Asortyment asortyment = asortymenty.Dane.Wszystkie().Where(a => a.Symbol == symbol).FirstOrDefault();
                    if (asortyment == null)
                        Console.WriteLine(string.Format("Nie znaleziono asortymentu o symbolu {0}", symbol));
                    else
                    {
                        decimal stanMagazynowy = glowny
                            .StanyMagazynowe
                            .Where(s => s.Asortyment.Id == asortyment.Id)
                            .Select(s => s.IloscDostepna)
                            .DefaultIfEmpty(0m)
                            .FirstOrDefault();
                        spis.Pozycje.Dodaj(asortyment, stanMagazynowy, asortyment.PodstawowaJednostkaMiaryAsortymentu);
                    }
                }

                // ustawianie statusu spisu na 'Sporządzony':
                spis.UstawStatus(statusy.Dane.Wszystkie().Where(s => (s.TypyDokumentow & (int)TypDokumentu.SpisInwentaryzacyjny) > 0 && s.Mnemonik == "S").FirstOrDefault(), null);

                // zapis spisu:
                if (spis.Zapisz())
                    Console.WriteLine(string.Format("Zapisano spis inwentaryzacyjny o numerze {0}.", spis.Dane.NumerSpisu.PelnaSygnatura));
                else
                    spis.WypiszBledy();
            }
            return this;
        }

		/// <summary>
		/// Metoda tworzy spis inwentaryzacyjny dla asortymentów z grupy o nazwie 'Pomadki'
		/// z ilością ustawioną na stan magazynowy na dzień sporządzenia spisu.
		/// </summary>
		public ObslugaInwentaryzacji TworzenieSpisuDlaWybranejGrupyAsortymentu()
        {
            ISpisyInwentaryzacyjne spisyInwentaryzacjne = sfera.PodajObiektTypu<ISpisyInwentaryzacyjne>();
            IKonfiguracje konfiguracje = sfera.PodajObiektTypu<IKonfiguracje>();
            IAsortymenty asortymenty = sfera.PodajObiektTypu<IAsortymenty>();
            IMagazyny magazyny = sfera.PodajObiektTypu<IMagazyny>();
            ICentrale centrale = sfera.PodajObiektTypu<ICentrale>();
            IStatusyDokumentow statusy = sfera.PodajObiektTypu<IStatusyDokumentow>();

            Konfiguracja konfiguracjaIs = konfiguracje.DaneDomyslne.SpisInwentaryzacyjny;
            Magazyn glowny = magazyny.Dane.Wszystkie().Where(m => m.Symbol == "MAG").FirstOrDefault();
            Centrala centrala = centrale.Dane.Wszystkie().FirstOrDefault();

            using (ISpisInwentaryzacyjny spis = spisyInwentaryzacjne.Utworz(konfiguracjaIs))
            {
                // podstawowe dane:
                spis.Dane.Magazyn = glowny;
                spis.Dane.MiejsceWprowadzenia = centrala;

                // dodawanie pozycji:
                IEnumerable<int> identyfikatoryAsortymentu = asortymenty
                    .Dane
                    .Wszystkie()
                    .Where(a => a.Grupa != null && a.Grupa.Nazwa == "Pomadki")
                    .Select(a => a.Id)
                    .ToArray();
                spis.Pozycje.Dodaj(identyfikatoryAsortymentu, SposobUstawianiaIlosciPozycjiSpisu.StanNaDzienSporzadzaniaSpisu, (v, m, d) =>
                {
                    Console.WriteLine(string.Format("Postep operacji: {0}. Liczba elementów: {1}. {2}", v, m, d));
                    return false;
                });

                // ustawianie statusu spisu na 'Sporządzony':
                spis.UstawStatus(statusy.Dane.Wszystkie().Where(s => (s.TypyDokumentow & (int)TypDokumentu.SpisInwentaryzacyjny) > 0 && s.Mnemonik == "S").FirstOrDefault(), null);

                // zapis spisu:
                if (spis.Zapisz())
                    Console.WriteLine(string.Format("Zapisano spis inwentaryzacyjny o numerze {0}.", spis.Dane.NumerSpisu.PelnaSygnatura));
                else
                    spis.WypiszBledy();
            }
			return this;
		}

		/// <summary>
		/// Metoda tworzy inwentaryzację poprzez dodanie do niej 4 towarów ręcznie z ilością wg spisów ustawioną na 100,
		/// następnie dla wszystkich pozostałych towarów ustawia ilośc 0 i ustawia jej status na 'Wykonany'.
		/// </summary>
		public ObslugaInwentaryzacji TworzenieIWykonanieInwentaryzacjiRecznie()
        {
            IInwentaryzacje inwentaryzacje = sfera.PodajObiektTypu<IInwentaryzacje>();
            IKonfiguracje konfiguracje = sfera.PodajObiektTypu<IKonfiguracje>();
            IAsortymenty asortymenty = sfera.PodajObiektTypu<IAsortymenty>();
            IMagazyny magazyny = sfera.PodajObiektTypu<IMagazyny>();
            ICentrale centrale = sfera.PodajObiektTypu<ICentrale>();
            IStatusyDokumentow statusy = sfera.PodajObiektTypu<IStatusyDokumentow>();

            Konfiguracja konfiguracjaIw = konfiguracje.DaneDomyslne.Inwentaryzacja;
            Magazyn glowny = magazyny.Dane.Wszystkie().Where(m => m.Symbol == "MAG").FirstOrDefault();
            Centrala centrala = centrale.Dane.Wszystkie().FirstOrDefault();

            List<string> symboleTowarow = new List<string>()
                {
                    "BANAW200",
                    "DZSO100",
                    "POYAR01",
                    "ZELAQUA"
                };

            using (IInwentaryzacja inwentaryzacja = inwentaryzacje.Utworz(konfiguracjaIw))
            {
                // podstawowe dane:
                inwentaryzacja.Dane.Magazyn = glowny;
                inwentaryzacja.Dane.MiejsceWprowadzenia = centrala;

                // dodawanie pozycji:
                foreach (string symbol in symboleTowarow)
                {
                    Asortyment asortyment = asortymenty.Dane.Wszystkie().Where(a => a.Symbol == symbol).FirstOrDefault();
                    if (asortyment == null)
                        Console.WriteLine(string.Format("Nie znaleziono asortymentu o symbolu {0}", symbol));
                    else
                    {
                        IUproszczonaPozycjaInwentaryzacji pozycja = inwentaryzacja.Pozycje.Dodaj(asortyment);
                        pozycja.RozpocznijEdycje();
                        pozycja.StanWgSpisow = 100m;
                        pozycja.ZakonczEdycje();
                    }
                }
                // uzupełnianie pozostałych pozycji ilością zerową:
                foreach (IUproszczonaPozycjaInwentaryzacji pozycjaNiedodana in inwentaryzacja.Pozycje.WszystkieFakeowe)
                {
                    pozycjaNiedodana.RozpocznijEdycje();
                    pozycjaNiedodana.StanWgSpisow = 0m;
                    pozycjaNiedodana.ZakonczEdycje();
                }

                // osoba zatwierdzająca jest wymagana przy wykonanej inwentaryzacji:
                inwentaryzacja.Dane.Zatwierdzajacy = inwentaryzacja.Dane.Odpowiedzialny;

                // ustawianie statusu:
                inwentaryzacja.UstawStatus(statusy.Dane.Wszystkie().Where(s => (s.TypyDokumentow & (int)TypDokumentu.Inwentaryzacja) > 0 && (s.Zamkniety ?? false)).FirstOrDefault(), inwentaryzacja.Dane.DataUtworzenia, (v, m, d) =>
                {
                    Console.WriteLine(string.Format("Postep operacji: {0}. Maksymalny postep: {1}. {2}", v, m, d));
                });

                // zapis inwentaryzacji:
                if (inwentaryzacja.Zapisz())
                    Console.WriteLine(string.Format("Zapisano inwentaryzację o numerze {0}.", inwentaryzacja.Dane.NumerInwentaryzacji.PelnaSygnatura));
                else
                    inwentaryzacja.WypiszBledy();
            }
			return this;
		}

		/// <summary>
		/// Metoda wycofuje wcześniej wykonaną inwentaryzację.
		/// </summary>
		public ObslugaInwentaryzacji WycofanieInwentaryzacji()
        {
            IInwentaryzacje inwentaryzacje = sfera.PodajObiektTypu<IInwentaryzacje>();
            IStatusyDokumentow statusy = sfera.PodajObiektTypu<IStatusyDokumentow>();
            Inwentaryzacja encjaIw = inwentaryzacje.Dane.Wszystkie().Where(i => i.DataUtworzenia == DateTime.Today && (i.StatusInwentaryzacji.Zamkniety ?? false)).FirstOrDefault();
            if (encjaIw == null)
                Console.WriteLine("Nie znaleziono żadnej inwentaryzacji utworzonej i wykonanej dzisiaj.");
            else
            {
                using (IInwentaryzacja inwentaryzacja = inwentaryzacje.Znajdz(encjaIw))
                {
                    // ustawianie statusu:
                    inwentaryzacja.UstawStatus(statusy.Dane.Wszystkie().Where(s => (s.TypyDokumentow & (int)TypDokumentu.Inwentaryzacja) > 0 && !(s.Zamkniety ?? false)).FirstOrDefault(), null, (v, m, d) =>
                    {
                        Console.WriteLine(string.Format("Postep operacji: {0}. Maksymalny postep: {1}. {2}", v, m, d));
                    });

                    // zapis inwentaryzacji:
                    if (inwentaryzacja.Zapisz())
                        Console.WriteLine(string.Format("Zapisano inwentaryzację o numerze {0}.", inwentaryzacja.Dane.NumerInwentaryzacji.PelnaSygnatura));
                    else
                        inwentaryzacja.WypiszBledy();
                }
            }
			return this;
		}

		/// <summary>
		/// Metoda tworzy pustą inwentaryzację, następnie tworzy spis inwentaryzacyjny na cały asortyment z ilością 100
		/// i podłącza go do utworzonej inwentaryzacji.
		/// </summary>
		public ObslugaInwentaryzacji DodawanieInwentaryzacjiNaPodstawieSpisu()
        {
            string numerUtworzonejInwentaryzacji = string.Empty;

            IInwentaryzacje inwentaryzacje = sfera.PodajObiektTypu<IInwentaryzacje>();
            ISpisyInwentaryzacyjne spisy = sfera.PodajObiektTypu<ISpisyInwentaryzacyjne>();
            IKonfiguracje konfiguracje = sfera.PodajObiektTypu<IKonfiguracje>();
            IAsortymenty asortymenty = sfera.PodajObiektTypu<IAsortymenty>();
            IMagazyny magazyny = sfera.PodajObiektTypu<IMagazyny>();
            ICentrale centrale = sfera.PodajObiektTypu<ICentrale>();
            IStatusyDokumentow statusy = sfera.PodajObiektTypu<IStatusyDokumentow>();

            Konfiguracja konfiguracjaIw = konfiguracje.DaneDomyslne.Inwentaryzacja;
            Konfiguracja konfiguracjaIs = konfiguracje.DaneDomyslne.SpisInwentaryzacyjny;
            Magazyn glowny = magazyny.Dane.Wszystkie().Where(m => m.Symbol == "MAG").FirstOrDefault();
            Centrala centrala = centrale.Dane.Wszystkie().FirstOrDefault();

            using (IInwentaryzacja inwentaryzacja = inwentaryzacje.Utworz(konfiguracjaIw))
            {
                // podstawowe dane:
                inwentaryzacja.Dane.Magazyn = glowny;
                inwentaryzacja.Dane.MiejsceWprowadzenia = centrala;

                // zapis inwentaryzacji:
                if (inwentaryzacja.Zapisz())
                    Console.WriteLine(string.Format("Zapisano inwentaryzację o numerze {0}.", inwentaryzacja.Dane.NumerInwentaryzacji.PelnaSygnatura));
                else
                    inwentaryzacja.WypiszBledy();

                numerUtworzonejInwentaryzacji = inwentaryzacja.Dane.NumerInwentaryzacji.PelnaSygnatura;
            }

            Inwentaryzacja utworzonaInwentaryzacja = inwentaryzacje.Dane.Wszystkie().Where(i => i.NumerInwentaryzacji.PelnaSygnatura == numerUtworzonejInwentaryzacji).FirstOrDefault();

            if (utworzonaInwentaryzacja == null)
            {
                Console.WriteLine(string.Format("Nie odnaleziono utworzonej inwentaryzacji o numerze {0}.", numerUtworzonejInwentaryzacji));
				return this;
			}

			using (ISpisInwentaryzacyjny spis = spisy.Utworz(konfiguracjaIs))
            {
                IEnumerable<int> identyfikatoryAsortymentu = asortymenty
                    .Dane
                    .Wszystkie()
                    .Where(a => a.Rodzaj.StanyMagazynowe)
                    .Select(a => a.Id)
                    .ToArray();

                // podstawowe dane:
                spis.Dane.Magazyn = glowny;
                spis.Dane.MiejsceWprowadzenia = centrala;
                spis.Dane.Inwentaryzacja = utworzonaInwentaryzacja;

                // dodawanie pozycji
                spis.Pozycje.Dodaj(identyfikatoryAsortymentu, SposobUstawianiaIlosciPozycjiSpisu.IloscUstawionaRecznie, 100m, (v, m, d) =>
                {
                    Console.WriteLine(string.Format("Postep operacji: {0}. Maksymalny postep: {1}. {2}", v, m, d));
                    return false;
                });

                // ustawianie statusu spisu na 'Włączony do inwentaryzacji':
                spis.UstawStatus(statusy.Dane.Wszystkie().Where(s => (s.TypyDokumentow & (int)TypDokumentu.SpisInwentaryzacyjny) > 0 && s.Edycja == (byte)EdycjaDokumentu.Ograniczona).FirstOrDefault(), null, (v, m, d) =>
                {
                    Console.WriteLine(string.Format("Postep operacji: {0}. Maksymalny postep: {1}. {2}", v, m, d));
                });

                // zapis spisu:
                if (spis.Zapisz())
                    Console.WriteLine(string.Format("Zapisano spis inwentaryzacyjny o numerze {0}.", spis.Dane.NumerSpisu.PelnaSygnatura));
                else
                    spis.WypiszBledy();
            }
			return this;
		}
	}
}
