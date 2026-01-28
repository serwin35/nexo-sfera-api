using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Wydruki.RTF;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutotekstySferycznePrzyklady
{
    /// <summary>
    /// Autotekst wyznaczający wartość składnika płacowego lub parametrycznego z rachunku do umowy cywilnoprawnej.
    /// </summary>
    public class AutotekstWartoscSkladnikaNaRachunku : IDefinicjaAutotekstu
    {
        /// <summary>
        /// Identyfikator plugina.
        /// </summary>
        public Guid Identyfikator => new Guid("18fc3a9c-bded-4f5c-a827-869dde5c203e");

        /// <summary>
        /// Nazwa plugina.
        /// </summary>
        public string Nazwa => "Wartość składnika na rachunku do UCP";

        /// <summary>
        /// Opis plugina.
        /// </summary>
        public string Opis => "Autotekst zwracający wartość składnika z rachunku do UCP";

        /// <summary>
        /// Nazwa autotekstu widoczna w menu.
        /// </summary>
        public string NazwaWMenu => "wartość składnika";

        /// <summary>
        /// Nazwa autotekstu widoczna w treści.
        /// </summary>
        public string NazwaWTresci => "wartosc skladnika z rachunku";

        /// <summary>
        /// Domyślne argumenty przy wstawianiu autotekstu z menu.
        /// </summary>
        public IEnumerable<string> DomyslneArgumenty => new string[] { "Nazwa składnika" };

        /// <summary>
        /// Grupa, do której należy autotekst. W tym przypadku, autotekst będzie dostępny tylko dla wydruków rachunków do umów cywilnoprawnych dostępnych na podmiotach z Gratyfikantem nexo.
        /// </summary>
        public GrupaAutotekstu Grupa => GrupaAutotekstu.RachunekGr;

        /// <summary>
        /// Dane dostawcy plugina.
        /// </summary>
        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        /// <summary>
        /// Metoda wyliczająca wartość autotekstu.
        /// </summary>
        /// <param name="kontekst">Kontekst autotekstu.</param>
        /// <returns>Wartość składnika o nazwie podanej jako argument autotekstu.</returns>
        public string ObliczAutotekst(IKontekstAutotekstu kontekst)
        {
            if (kontekst.Argumenty.Count() != 1)
                return "BŁĄD AUTOTEKSTU: NIE PODANO NAZWY SKŁADNIKA.";

            var nazwaSkladnika = kontekst.Argumenty.ElementAt(0);

            var skladnik = kontekst.PodajZrodloWiedzy<RachunekDoUmowyPracowniczejGr>().WartosciSladnikowPlacowychWynagrodzenia
                .FirstOrDefault(x => x.SkladnikPlacowy.Nazwa == nazwaSkladnika);

            if (skladnik == null)
                return $"BŁĄD AUTOTEKSTU: NIE ZNALEZIONO SKŁADNIKA O NAZWIE '{nazwaSkladnika}'.";

            return skladnik.Wartosc.ToString("N");
        }
    }
}