namespace PrzepisywaniePowiazanWiadomosci.Models
{
    public class UproszczoneKontoPocztowe
    {
        public string Nazwa { get; set; }
        public string Adres { get; set; }
        public string NazwaRozszerzona
        {
            get => $"{Nazwa} ({Adres})";
        }
    }
}
