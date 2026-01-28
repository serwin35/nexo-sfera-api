using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Excel2XmlSrodkiTrwale
{
    public partial class XlsxExporter
    {
        private static string ZapiszDoPliku(string sciezka, string nazwaPliku, XElement daneXml)
        {
            string outputFilename = Path.Combine(sciezka, nazwaPliku);
            using (XmlWriter writer = XmlTextWriter.Create(outputFilename, new XmlWriterSettings() { Indent = true }))
            {
                daneXml.WriteTo(writer);
            }
            return outputFilename;
        }
    }
}
