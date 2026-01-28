using System.IO;

namespace OpakowaniaPrezentowe
{
    public class SerializatorGrawerunkow
    {
        private const string NazwaPlikuWzorca = "Grawerunek.svg";
        private const string TekstPlaceholder = "@@Tekst";

        public string Serializuj(
            string grawerunek)
        {
            string wzorzec = WczytajWzorzec();

            return WstawGrawerunekDoWzorca(wzorzec, grawerunek);
        }

        private static string WczytajWzorzec()
        {
            return File.ReadAllText(NazwaPlikuWzorca);
        }

        private static string WstawGrawerunekDoWzorca(string wzorzec, string grawerunek)
        {
            return wzorzec.Replace(TekstPlaceholder, grawerunek);
        }
    }
}