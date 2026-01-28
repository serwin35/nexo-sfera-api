using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;


namespace exportExcel2XML
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