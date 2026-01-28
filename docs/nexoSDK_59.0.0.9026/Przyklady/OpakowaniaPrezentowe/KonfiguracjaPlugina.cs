using Newtonsoft.Json;
using System;
using System.IO;

namespace OpakowaniaPrezentowe
{
    public class KonfiguracjaPlugina
    {
        private const string ConfigFileName = "OpakowaniaPrezentowe.config.json";

        [JsonRequired]
        public string PoleWlasne_Konfiguracja_PrzepiszKosztyOpakowanPrzyRealizacji { get; set; }

        [JsonRequired]
        public string PoleWlasne_Konfiguracja_GrupaOpakowanPrezentowych { get; set; }

        [JsonRequired]
        public string PoleWlasne_DokumentZK_WiadomoscDoSprzedajacego { get; set; }

        [JsonRequired]
        public string PoleWlasne_PozycjaDokumentu_KosztOpakowan { get; set; }

        [JsonRequired]
        public string PoleWlasne_Partia_Dedykacja { get; set; }

        [JsonRequired]
        public string Operacja_Formatka_EdytujDedykacje { get; set; }

        [JsonRequired]
        public string Operacja_Formatka_UtworzPlikMaszynyGrawerujacej { get; set; }

        [JsonRequired]
        public string Operacja_Lista_UtworzPlikMaszynyGrawerujacej { get; set; }

        [JsonRequired]
        public string SciezkaZapisuSVG { get; set; }

        private static KonfiguracjaPlugina _instancja;
        public static KonfiguracjaPlugina Instancja
        {
            get
            {
                if (_instancja == null)
                {
                    _instancja = Wczytaj();
                }

                return _instancja;
            }
        }

        private static KonfiguracjaPlugina Wczytaj()
        {
            return
                JsonConvert.DeserializeObject<KonfiguracjaPlugina>(
                    File.ReadAllText(
                        PodajSciezkeDoPlikuZKonfiguracja()));
        }

        private static string PodajSciezkeDoPlikuZKonfiguracja()
        {
            string sciezka = Path.Combine("..", "Config", ConfigFileName);

            if (File.Exists(sciezka))
            {
                return sciezka;
            }
            else
            {
                return ConfigFileName;
            }
        }
    }
}