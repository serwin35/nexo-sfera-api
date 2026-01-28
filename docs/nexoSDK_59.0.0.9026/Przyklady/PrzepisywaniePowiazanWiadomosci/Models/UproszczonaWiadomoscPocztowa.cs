using System;
using System.Collections.Generic;

namespace PrzepisywaniePowiazanWiadomosci.Models
{
    public class UproszczonaWiadomoscPocztowa
    {
        public int Id { get; set; }
        public string Temat { get; set; }
        public DateTime DataWyslania { get; set; }
        public string Nadawca { get; set; }
        public IEnumerable<string> Adresaci { get; set; }
        public byte[] HashTresci { get; set; }
        public int? KlientId { get; set; }
        public int? DzialanieId { get; set; }
        public int? DokumentId { get; set; }
        public int? ZlecenieSerwisoweId { get; set; }
        public int? SerwisowaneUrzadzenieId { get; set; }
        public int? ProcesOfertowyId { get; set; }
        public int? ZasobId { get; set; }
        public int? RezerwacjaZasobuId { get; set; }

        public bool MaPowiazania()
        {
            int? id = KlientId ?? DzialanieId ?? DokumentId ?? ZlecenieSerwisoweId ?? ProcesOfertowyId ?? ZasobId ?? RezerwacjaZasobuId ?? SerwisowaneUrzadzenieId;
            bool maPowiazania = (id != null) && (id != 0);
            return maPowiazania;
        }
    }
}
