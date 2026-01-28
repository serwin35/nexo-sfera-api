using System;
using System.Collections.Generic;

namespace OperacjeSferyczne.ImportEksportPracownikow.Model
{
    [Serializable]
    public class DanePracownika
    {
        public string Imie { get; set; }

        public string Nazwisko { get; set; }

        public DateTime DataUrodzenia { get; set; }

        public string Plec { get; set; }

        public string Pesel { get; set; }

        public DaneAdresowe AdresZamieszkania { get; set; }

        public DaneAdresowe AdresKorespondencyjny { get; set; }

        public DaneAdresowe AdresZameldowania { get; set; }

        public List<DaneKontaktu> Kontakty { get; set; }

        public override string ToString()
        {
            return $"{Imie} {Nazwisko}";
        }
    }
}
