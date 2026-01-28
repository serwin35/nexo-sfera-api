namespace OperacjeSferyczne.ImportEksportPracownikow.Model
{
    public class DaneAdresowe
    {
        public string Nazwa { get; set; }

        public string Ulica { get; set; }

        public string NrDomu { get; set; }

        public string NrLokalu { get; set; }

        public string KodPocztowy { get; set; }

        public string Miejscowosc { get; set; }

        public string Poczta { get; set; }

        public string Wojewodztwo { get; set; }

        public string Panstwo { get; set; }

        public string Powiat { get; set; }

        public DaneGminy Gmina { get; set; }
    }
}
