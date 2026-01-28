using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace Excel2XmlSrodkiTrwale
{
    public partial class XlsxExporter
    {
        static XNamespace nsMiejscaUzytkowania = "http://schemas.insert.com.pl/2013/hop/miejscauzytkowania";
        /// <summary>
        /// Metoda tworząca plik xml z miejscem użytkowania (IN001, InsERT Jerzmanowska).
        /// </summary>
        /// <param name="filename">Nazwa pliku excel z danymi</param>
        /// <returns></returns>
        public string[] ExportMiejsceUzytkowania(string filename)
        {
            Slownik<string, int> kolumny = new Slownik<string, int>();
            XElement miejscaUzytkowania = new XElement(nsMiejscaUzytkowania + "MiejscaUzytkowania");

            StringBuilder sbMsg = new StringBuilder();

            XElement poz = GenerujMiejscaUzytkowania();

            if (!string.IsNullOrWhiteSpace(poz.Value))
                miejscaUzytkowania.Add(poz);

            List<string> info = new List<string>();
            string sciezka = Path.GetDirectoryName(filename);

            if (miejscaUzytkowania != null)
            {
                info.Add(ZapiszDoPliku(sciezka, "MiejscaUzytkowania.xml", miejscaUzytkowania));
            }

            if (sbMsg.Length > 0)
                info.Add(sbMsg.ToString());

            return info.ToArray();
        }

        private static XElement GenerujMiejscaUzytkowania()
        {
            var poz = new ElementHelper(nsMiejscaUzytkowania.NamespaceName, "MiejsceUzytkowania", null);
            poz.AddAttributeValue("Aktywne", 1);
            poz.AddElementValue("Symbol", "IN001");
            poz.AddElementValue("Nazwa", "InsERT Jerzmanowska");

            return poz.GetXElement();
        }
    }
}
