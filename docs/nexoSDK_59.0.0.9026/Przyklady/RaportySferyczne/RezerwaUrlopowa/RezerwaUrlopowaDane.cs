namespace RaportySferyczne.RezerwaUrlopowa
{
    public class RezerwaUrlopowaDane
    {
        public int Id { get; set; }

        public string Imie { get; set; }

        public string Nazwisko { get; set; }

        public decimal GodzinNiewykorzystanegoUrlopu { get; set; }

        public int GodzinNiewykorzystanegoUrlopuPrecyzja { get; set; }

        public decimal GodzinPrzepracowanych { get; set; }

        public int GodzinPrzepracowanychPrecyzja { get; set; }

        public decimal WynagrodzenieOtrzymane { get; set; }

        public decimal StawkaGodzinowa { get; set; }

        public decimal PrognozowaneWynagrodzenie { get; set; }

        public decimal ProcentSkladekZUS { get; set; }

        public decimal SkladkiZUS { get; set; }

        public decimal RezerwaUrlopowa { get; set; }
    }
}