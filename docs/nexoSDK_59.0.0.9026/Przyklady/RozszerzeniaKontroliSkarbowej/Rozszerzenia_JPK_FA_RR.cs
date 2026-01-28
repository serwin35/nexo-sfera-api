using InsERT.Moria.Logistyka;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using System.Collections.Generic;
using System.Linq;

namespace RozszerzeniaKontroliSkarbowej
{
    public class Rozszerzenia_JPK_FA_RR : RozszerzeniaBase
    {
        /// <summary>
        /// Funkcja pobiera opis asortymentu historia (opis na moment wystawienia dokumentu) jako nazwę asortymentu w polu P_5 wiersza faktury RR pliku JPK_FA_RR.
        /// </summary>
        /// <param name="obiekt">Pozycja faktury RR w pliku JPK_FA_RR.</param>
        /// <returns>Opis asortymentu historia lub nazwę gdy opis jest pusty.</returns>
        public string OpisAsortymentuHistoria_P_5(PozycjaFakturyRRJPK obiekt)
        {
            InsERT.Moria.Asortymenty.IAsortymentyDane asortymentyDane = PodajDaneTypu<InsERT.Moria.Asortymenty.IAsortymentyDane>();
            InsERT.Moria.Asortymenty.IAsortymentyJednorazoweDane uslugiJednorazoweDane = PodajDaneTypu<InsERT.Moria.Asortymenty.IAsortymentyJednorazoweDane>();

            Dictionary<int, (string nazwa, string opis)> cacheOpisow = PobierzLubDodajZmiennaUzytkownika("CacheOpisow", () => new Dictionary<int, (string nazwa, string opis)>());
            if (!cacheOpisow.TryGetValue(obiekt.AsortymentWybranyId, out (string nazwa, string opis) dane))
            {
                if (obiekt.IdKartotekiAsortymentu.HasValue)
                {
                    var nazwaOpis = asortymentyDane.Wszystkie()
                        .Concat(asortymentyDane.Nieaktywne())
                        .Where(a => a.Id == obiekt.IdKartotekiAsortymentu.Value)
                        .SelectMany(a => a.HistoriaAsortymentu)
                        .Where(ah => ah.Id == obiekt.AsortymentWybranyId)
                        .Select(ah => new { ah.Nazwa, ah.Opis })
                        .FirstOrDefault();
                    if (nazwaOpis != null)
                        dane = (nazwaOpis.Nazwa, nazwaOpis.Opis);
                }
                else
                {
                    var nazwaOpis = uslugiJednorazoweDane.Wszystkie()
                        .Where(uj => uj.Id == obiekt.AsortymentWybranyId)
                        .Select(uj => new { uj.Nazwa, uj.Opis })
                        .FirstOrDefault();
                    if (nazwaOpis != null)
                        dane = (nazwaOpis.Nazwa, nazwaOpis.Opis);
                }
                cacheOpisow.Add(obiekt.AsortymentWybranyId, dane);
            }
            if (string.IsNullOrEmpty(dane.opis))
                return dane.nazwa ?? string.Empty;
            else
                return dane.opis;
        }

        /// <summary>
        /// Funkcja pobiera numer seryjny podpisu elektroniczncego dostawcy z pola własnego o nazwie "Numer seryjny podpisu" w kartotece klienta do pola P_3A/NumerSeryjny.
        /// </summary>
        /// <param name="obiekt">Nagłówek faktury RR z pliku JPK.</param>
        /// <returns>Numer seryjny podpisu elektronicznego dostawcy.</returns>
        public string NumerSeryjnyPodpisuDostawcy_P_3A_NumerSeryjny(NaglowekFakturyRRJPK obiekt)
        {
            if (!obiekt.KartotekaDostawcyId.HasValue)
                return string.Empty;

            InsERT.Moria.Klienci.IPodmiotyDane podmioty = PodajDaneTypu<InsERT.Moria.Klienci.IPodmiotyDane>();
            Dictionary<int, string> cacheNumerow = PobierzLubDodajZmiennaUzytkownika("CacheNumerow", () => new Dictionary<int, string>());
            if (!cacheNumerow.TryGetValue(obiekt.KartotekaDostawcyId.Value, out string numer))
            {
                numer = podmioty
                    .Wszystkie()
                    .Where(pdm => pdm.Id == obiekt.KartotekaDostawcyId.Value)
                    .Select(pdm => pdm.PolaWlasneAdv2.Get<string>("Numer seryjny podpisu"))
                    .ResolveExtensionProperties()
                    .FirstOrDefault();
                cacheNumerow.Add(obiekt.KartotekaDostawcyId.Value, numer);
            }
            return numer ?? string.Empty;
        }

        /// <summary>
        /// Funkcja pobiera nazwę posiadacza podpisu elektroniczncego dostawcy z nazwy skróconej w kartotece klienta do pola P_3A/Posiadacz.
        /// </summary>
        /// <param name="obiekt">Nagłówek faktury RR z pliku JPK.</param>
        /// <returns>Nazwa posiadacza podpisu elektronicznego dostawcy.</returns>
        public string PosiadaczPodpisuDostawcy_P_3A_Posiadacz(NaglowekFakturyRRJPK obiekt)
        {
            if (!obiekt.KartotekaDostawcyId.HasValue)
                return string.Empty;

            InsERT.Moria.Klienci.IPodmiotyDane podmioty = PodajDaneTypu<InsERT.Moria.Klienci.IPodmiotyDane>();
            Dictionary<int, string> cacheNumerow = PobierzLubDodajZmiennaUzytkownika("CacheNazw", () => new Dictionary<int, string>());
            if (!cacheNumerow.TryGetValue(obiekt.KartotekaDostawcyId.Value, out string nazwa))
            {
                nazwa = podmioty
                    .Wszystkie()
                    .Where(pdm => pdm.Id == obiekt.KartotekaDostawcyId.Value)
                    .Select(pdm => pdm.NazwaSkrocona)
                    .FirstOrDefault();
                cacheNumerow.Add(obiekt.KartotekaDostawcyId.Value, nazwa);
            }
            return nazwa ?? string.Empty;

        }

        /// <summary>
        /// Funkcja pobiera nazwę wystawcy podpisu elektroniczncego dostawcy z pola własnego o nazwie "Dostawca podpisu" w kartotece klienta do pola P_3A/Wystawca.
        /// </summary>
        /// <param name="obiekt">Nagłówek faktury RR z pliku JPK.</param>
        /// <returns>Nazwa wystawcy podpisu elektronicznego dostawcy.</returns>
        public string WystawcaPodpisuDostawcy_P_3A_Wystawca(NaglowekFakturyRRJPK obiekt)
        {
            if (!obiekt.DostawcaId.HasValue)
                return string.Empty;

            InsERT.Moria.Klienci.IPodmiotyDane podmioty = PodajDaneTypu<InsERT.Moria.Klienci.IPodmiotyDane>();
            Dictionary<int, string> cacheNumerow = PobierzLubDodajZmiennaUzytkownika("CacheWystawcow", () => new Dictionary<int, string>());
            if (!cacheNumerow.TryGetValue(obiekt.KartotekaDostawcyId.Value, out string wystawca))
            {
                wystawca = podmioty
                    .Wszystkie()
                    .Where(pdm => pdm.Id == obiekt.KartotekaDostawcyId.Value)
                    .Select(pdm => pdm.PolaWlasneAdv2.Get<string>("Dostawca podpisu"))
                    .ResolveExtensionProperties()
                    .FirstOrDefault();
                cacheNumerow.Add(obiekt.KartotekaDostawcyId.Value, wystawca);
            }
            return wystawca ?? string.Empty;
        }

        /// <summary>
        /// Funkcja zwraca stały numer seryjny podpisu nabywcy dla pola P_3B/NumerSeryjny.
        /// </summary>
        /// <returns>Stały numer seryjny podpisu elektronicznego nabywcy.</returns>
        public string NumerSeryjnyPodpisuNabywcy_P_3B_NumerSeryjny(NaglowekFakturyRRJPK _)
        {
            return "87657896321";
        }

        /// <summary>
        /// Funkcja zwraca stałą nazwę posiadacza podpisu nabywcy dla pola P_3B/Posiadacz.
        /// </summary>
        /// <returns>Stała nazwa posiadacza podpisu elektronicznego nabywcy.</returns>
        public string PosiadaczPodpisuNabywcy_P_3B_Posiadacz(NaglowekFakturyRRJPK _)
        {
            return "Moja Firma";
        }

        /// <summary>
        /// Funkcja zwraca stałą nazwę wystawcy podpisu nabywcy dla pola P_3B/Wystawca.
        /// </summary>
        /// <returns>Stały wystawca podpisu elektronicznego nabywcy.</returns>
        public string WystawcaPodpisuNabywcy_P_3B_Wystawca(NaglowekFakturyRRJPK _)
        {
            return "mSzafir";
        }
    }
}
