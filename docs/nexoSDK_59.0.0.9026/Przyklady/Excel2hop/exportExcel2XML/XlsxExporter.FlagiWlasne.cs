using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace exportExcel2XML
{
    public partial class XlsxExporter
    {
        private static XElement GenerujXmlFlagiWlasne(string sciezka, List<Tuple<string, string, string>> listaFlagWlasnych)
        {
            XNamespace nsFlagiWlasne = "http://schemas.insert.com.pl/2015/hop/flagiwlasne";

            XElement flagiWlasneXML = new XElement(nsFlagiWlasne + "FlagiWlasne");
            var listaFlagWlasnychUnikalnych = new List<Tuple<string, string, string>>();

            string plikFlagi = Path.Combine(sciezka, "FlagiWlasne.xml");
            WczytajFlagiWlasne(listaFlagWlasnychUnikalnych, plikFlagi);

            foreach (var item in listaFlagWlasnych.Distinct())
                listaFlagWlasnychUnikalnych.Add(item);

            foreach (var flaga in listaFlagWlasnychUnikalnych.Distinct())
            {
                XElement flagaWlasna = new XElement(nsFlagiWlasne + "FlagaWlasna");
                flagiWlasneXML.Add(flagaWlasna);
                flagaWlasna.Add(new XElement(nsFlagiWlasne + "Nazwa", flaga.Item1));
                flagaWlasna.Add(new XElement(nsFlagiWlasne + "Kolor", flaga.Item2));
                flagaWlasna.Add(new XElement(nsFlagiWlasne + "Domena", flaga.Item3));
            }
            return flagiWlasneXML;
        }

        private static void WczytajFlagiWlasne(List<Tuple<string, string, string>> listaFlagWlasnychUnikalnych, string plikFlagi)
        {
            if (!File.Exists(plikFlagi))
                return;

            using (XmlReader reader = XmlReader.Create(plikFlagi))
            {
                string[] flaga = null;

                reader.MoveToContent();
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "FlagaWlasna")
                        {
                            flaga = new string[3];
                        }
                        else if (reader.Name == "Nazwa")
                            flaga[0] = reader.ReadElementContentAsString();
                        else if (reader.Name == "Kolor")
                            flaga[1] = reader.ReadElementContentAsString();
                        else if (reader.Name == "Domena")
                            flaga[2] = reader.ReadElementContentAsString();
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (reader.Name == "FlagaWlasna")
                            listaFlagWlasnychUnikalnych.Add(
                                new Tuple<string, string, string>(
                                    flaga[0], flaga[1], flaga[2]));
                    }
                }
            }
        }
    }
}