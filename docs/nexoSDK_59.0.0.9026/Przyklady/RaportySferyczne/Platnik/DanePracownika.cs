using System;

namespace RaportySferyczne.Platnik
{
    public class DanePracownika
    {
        public int Id { get; set; }

        public string NexoImie { get; set; }

        public string NexoNazwisko { get; set; }

        public string NexoPesel { get; set; }

        public DateTime? NexoDataUrodzenia { get; set; }

        public string PlatnikImie { get; set; }

        public string PlatnikNazwisko { get; set; }

        public string PlatnikPesel { get; set; }

        public DateTime? PlatnikDataUrodzenia { get; set; }
    }
}