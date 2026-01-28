using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace OperacjeSferyczne.ImportEksportPracownikow.Model
{
    [Serializable]
    [XmlRoot("Pracownicy")]
    public class DanePracownikow
    {
        [XmlElement("Pracownik")]
        public List<DanePracownika> Pracownicy { get; set; } = new List<DanePracownika>();
    }
}
