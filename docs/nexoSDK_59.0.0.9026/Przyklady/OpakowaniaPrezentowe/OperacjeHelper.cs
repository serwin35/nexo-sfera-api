using InsERT.Moria.ModelDanych;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpakowaniaPrezentowe
{
    public static class OperacjeHelper
    {
        private static readonly char[] _separatorMenu = new[] { '|' };

        public static string PodajNazwe(string skonfigurowanaNazwa)
        {
            return SplitNazwa(skonfigurowanaNazwa).Last();
        }

        public static string[] PodajSciezkeWMenu(string skonfigurowanaNazwa)
        {
            string[] split = SplitNazwa(skonfigurowanaNazwa).ToArray();

            if (split.Length == 1)
            {
                return Array.Empty<string>();
            }
            else
            {
                return split.Take(split.Length - 1).ToArray();
            }
        }

        public static string PodajNazwePlikuSVGDlaDedykacji(
            DokumentZPM dokument,
            int lpDedykacji)
        {
            string nazwaPliku = $"_{lpDedykacji}.svg";

            if (string.IsNullOrWhiteSpace(dokument.NumerWewnetrzny.PelnaSygnatura))
            {
                nazwaPliku = $"DokumentZPM_{dokument.GetHashCode()}" + nazwaPliku;
            }
            else
            {
                nazwaPliku = dokument.NumerWewnetrzny.PelnaSygnatura.Replace('/', '_') + nazwaPliku;
            }

            return Path.Combine(
                KonfiguracjaPlugina.Instancja.SciezkaZapisuSVG,
                nazwaPliku);
        }

        public static void ZapewnijFolder(string nazwaPliku)
        {
            string folder = Path.GetDirectoryName(nazwaPliku);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        private static IEnumerable<string> SplitNazwa(string skonfigurowanaNazwa)
        {
            return skonfigurowanaNazwa
                .Split(_separatorMenu)
                .Select(n => n.Trim());
        }
    }
}