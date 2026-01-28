using InsERT.Moria.Dokumenty.Logistyka;
using System;

namespace PrzykladKolejnosciRealizacjiZamowien
{
    public class PozycjaBlokujaca : IPozycjaZamowieniaBlokujacaRealizacje
    {
        public int PozycjaId { get; set; }
        public int Kolejnosc { get; set; }
        public int LpPozycji { get; set; }
        public string StatusDokumentu { get; set; }
        public string NumerDokumentu { get; set; }
        public string NumerDokumentuPrzed { get; set; }
        public string NumerDokumentuPo { get; set; }
        public int? Numer { get; set; }
        public DateTime DataWystawienia { get; set; }
        public string Zamawiajacy { get; set; }
        public decimal IloscZamowiona { get; set; }
        public string JmPodstawowa { get; set; }
        public int PrecyzjaIlosci { get; set; }
        public DateTime? TerminRealizacji { get; set; }
        public string Wystawil { get; set; }
        public string SymbolAsortymentu { get; set; }
        public string NazwaAsortymentu { get; set; }
    }
}
