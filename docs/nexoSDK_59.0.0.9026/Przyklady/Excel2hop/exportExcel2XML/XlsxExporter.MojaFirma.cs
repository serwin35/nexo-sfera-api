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
        static XNamespace nsMojaFirma = "http://schemas.insert.com.pl/2013/hop/danepodmiotu";

        private static XElement GenerujXmlMojaFirma(string sciezka, IEnumerable<string> listaMagazynow)
        {
            ElementHelper mf = new ElementHelper(nsMojaFirma.NamespaceName, "MojaFirma", null);
            mf.GetXElement().Add(new XAttribute(XNamespace.Xmlns + "w", nsHop));
            mf.AddElementValue("NIP", "1111111111");
            var adresy = mf.AddElement("Adresy");
            var adres = adresy.AddElementHelper(nsHop.NamespaceName, "Adres", null);
            adres.AddAttributeValue("Rodzaj", "Glowny");
            var szczegoly = adres.AddElement("AdresSzczegoly");
            szczegoly.AddElementValue("Miasto", "Wrocław");
            szczegoly.AddElementValue("Ulica", "Jerzmanowska");
            szczegoly.AddElementValue("Panstwo", "Polska");
            mf.AddElement("Cenniki");
            var dane = mf.AddElement("DaneFirmy");
            dane.AddElementValue("Nazwa", "Moja firma");
            dane.AddElementValue("NazwaSkrocona", "Moja firma");

            //****************************
            XElement magazynyXML = new XElement(nsMojaFirma + "Magazyny");
            var listaMagazynowUnikalnych = new List<Tuple<string, string, string>>();

            string plikFlagi = Path.Combine(sciezka, "MojaFirma.xml");
            WczytajMagazyny(listaMagazynowUnikalnych, plikFlagi);

            foreach (var item in listaMagazynow.Distinct().Select(m => new Tuple<string, string, string>(m, m, m)))
                listaMagazynowUnikalnych.Add(item);

            foreach (var mag in listaMagazynowUnikalnych.Distinct())
            {
                XElement magazyn = new XElement(nsMojaFirma + "Magazyn");
                magazynyXML.Add(magazyn);
                magazyn.Add(new XElement(nsMojaFirma + "Symbol", mag.Item1));
                magazyn.Add(new XElement(nsMojaFirma + "Nazwa", mag.Item2));
                magazyn.Add(new XElement(nsMojaFirma + "Opis", mag.Item3));
                magazyn.Add(new XElement(nsMojaFirma + "Adresy"));
            }
            //*****************************
            mf.GetXElement().Add(magazynyXML);

            var model = mf.AddElement("ModelOrganizacyjny");
            var centrala = model.AddElement("Centrala");
            centrala.AddElementValue("Symbol", "MOJA FIRMA");
            return mf.GetXElement();
        }

        private static void WczytajMagazyny(List<Tuple<string, string, string>> listaMagazynowUnikalnych, string plikMojejFirmy)
        {
            if (!File.Exists(plikMojejFirmy))
                return;

            using (XmlReader reader = XmlReader.Create(plikMojejFirmy))
            {
                string[] magazyn = null;
                bool wMagazynach = false;

                reader.MoveToContent();
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "Magazyny")
                            wMagazynach = reader.IsStartElement() && !reader.IsEmptyElement;
                        else if (wMagazynach)
                        {
                            if (reader.Name == "Magazyn")
                                magazyn = new string[3];
                            else if (reader.Name == "Symbol")
                                magazyn[0] = reader.ReadElementContentAsString();
                            else if (reader.Name == "Nazwa")
                                magazyn[1] = reader.ReadElementContentAsString();
                            else if (reader.Name == "Opis")
                                magazyn[2] = reader.ReadElementContentAsString();
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (wMagazynach && reader.Name == "Magazyn")
                            listaMagazynowUnikalnych.Add(
                                new Tuple<string, string, string>(
                                    magazyn[0], magazyn[1], magazyn[2]));
                    }
                }
            }
        }
    }
}