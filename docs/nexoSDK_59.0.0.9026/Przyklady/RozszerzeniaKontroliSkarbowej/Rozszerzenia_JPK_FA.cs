using InsERT.Moria.Logistyka;
using System.Collections.Generic;
using System.Linq;

namespace RozszerzeniaKontroliSkarbowej
{
    public class Rozszerzenia_JPK_FA : RozszerzeniaBase
    {
        /// <summary>
        /// Funkcja pobiera opis asortymentu historia (opis na moment wystawienia dokumentu) jako nazwę asortymentu w polu P_7 wiersza faktury pliku JPK_FA.
        /// </summary>
        /// <param name="obiekt">Pozycja faktury w pliku JPK_FA.</param>
        /// <returns>Opis asortymentu historia lub nazwę gdy opis jest pusty.</returns>
        public string OpisAsortymentuHistoria_P_7(PozycjaFakturyJPK obiekt)
        {
            InsERT.Moria.Asortymenty.IAsortymentyDane asortymentyDane = PodajDaneTypu<InsERT.Moria.Asortymenty.IAsortymentyDane>();
            InsERT.Moria.Asortymenty.IAsortymentyJednorazoweDane uslugiJednorazoweDane = PodajDaneTypu<InsERT.Moria.Asortymenty.IAsortymentyJednorazoweDane>();

            Dictionary<int, string> cacheOpisow = PobierzLubDodajZmiennaUzytkownika("CacheOpisow", () => new Dictionary<int, string>());
            if (!cacheOpisow.TryGetValue(obiekt.IdAsortymentu, out string opis))
            {
                if (obiekt.IdKartotekiAsortymentu.HasValue)
                {
                    opis = asortymentyDane.Wszystkie()
                        .Concat(asortymentyDane.Nieaktywne())
                        .Where(a => a.Id == obiekt.IdKartotekiAsortymentu.Value)
                        .SelectMany(a => a.HistoriaAsortymentu)
                        .Where(ah => ah.Id == obiekt.IdAsortymentu)
                        .Select(ah => ah.Opis)
                        .FirstOrDefault() ?? string.Empty;
                }
                else
                {
                    opis = uslugiJednorazoweDane.Wszystkie()
                        .Where(uj => uj.Id == obiekt.IdAsortymentu)
                        .Select(uj => uj.Opis)
                        .FirstOrDefault();
                }
                cacheOpisow.Add(obiekt.IdAsortymentu, opis);
            }
            if (string.IsNullOrEmpty(opis))
                return obiekt.NazwaAsortymentu;
            else
                return opis;

        }

        /// <summary>
        /// Funkcja pobiera opis asortymentu historia (opis na moment wystawienia dokumentu) jako nazwę asortymentu w polu P_7Z wiersza zamówienia pliku JPK_FA.
        /// </summary>
        /// <param name="obiekt">Pozycja zamówienia zaliczkowego w pliku JPK_FA.</param>
        /// <returns>Opis asortymentu historia lub nazwę gdy opis jest pusty.</returns>
        public string OpisAsortymentuHistoria_P_7Z(PozycjaFakturyZaliczkowejJPK obiekt)
        {
            InsERT.Moria.Asortymenty.IAsortymentyDane asortymentyDane = PodajDaneTypu<InsERT.Moria.Asortymenty.IAsortymentyDane>();
            InsERT.Moria.Asortymenty.IAsortymentyJednorazoweDane uslugiJednorazoweDane = PodajDaneTypu<InsERT.Moria.Asortymenty.IAsortymentyJednorazoweDane>();

            Dictionary<int, string> cacheOpisow = PobierzLubDodajZmiennaUzytkownika("CacheOpisow", () => new Dictionary<int, string>());
            if (!cacheOpisow.TryGetValue(obiekt.IdAsortymentu, out string opis))
            {
                if (obiekt.IdKartotekiAsortymentu.HasValue)
                {
                    opis = asortymentyDane.Wszystkie()
                        .Concat(asortymentyDane.Nieaktywne())
                        .Where(a => a.Id == obiekt.IdKartotekiAsortymentu.Value)
                        .SelectMany(a => a.HistoriaAsortymentu)
                        .Where(ah => ah.Id == obiekt.IdAsortymentu)
                        .Select(ah => ah.Opis)
                        .FirstOrDefault() ?? string.Empty;
                }
                else
                {
                    opis = uslugiJednorazoweDane.Wszystkie()
                        .Where(uj => uj.Id == obiekt.IdAsortymentu)
                        .Select(uj => uj.Opis)
                        .FirstOrDefault();
                }
                cacheOpisow.Add(obiekt.IdAsortymentu, opis);
            }
            if (string.IsNullOrEmpty(opis))
                return obiekt.Nazwa;
            else
                return opis;
        }
        
        /// <summary>
        /// Funkcja pobiera aktualny opis w kartotece jako nazwę asortymentu w polu P_7 wiersza faktury pliku JPK_FA.
        /// </summary>
        /// <param name="obiekt">Pozycja faktury w pliku JPK_FA.</param>
        /// <returns>Opis asortymentu w kartotece lub nazwę gdy opis jest pusty.</returns>
        public string AktualnyOpisWKartotece_P_7(PozycjaFakturyJPK obiekt)
        {
            InsERT.Moria.Asortymenty.IAsortymentyDane asortymentyDane = PodajDaneTypu<InsERT.Moria.Asortymenty.IAsortymentyDane>();
            Dictionary<int, string> cacheOpisow = PobierzLubDodajZmiennaUzytkownika("CacheOpisow", () => new Dictionary<int, string>());
            string opis = string.Empty;
            if (obiekt.IdKartotekiAsortymentu.HasValue)
            {
                if (!cacheOpisow.TryGetValue(obiekt.IdKartotekiAsortymentu.Value, out opis))
                {
                    opis = asortymentyDane.Wszystkie()
                        .Concat(asortymentyDane.Nieaktywne())
                        .Where(a => a.Id == obiekt.IdKartotekiAsortymentu.Value)
                        .Select(a => a.Opis)
                        .FirstOrDefault() ?? string.Empty;
                    cacheOpisow.Add(obiekt.IdKartotekiAsortymentu.Value, opis);
                }
            }
            
            if (string.IsNullOrEmpty(opis))
                return obiekt.NazwaAsortymentu;
            else
                return opis;
        }

    }
}
