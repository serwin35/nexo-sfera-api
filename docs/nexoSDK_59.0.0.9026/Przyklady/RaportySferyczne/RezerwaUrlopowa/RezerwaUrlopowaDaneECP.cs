using System;

namespace RaportySferyczne.RezerwaUrlopowa
{
    internal class RezerwaUrlopowaDaneECP
    {
        public int IdentyfikatorPracownika { get; set; }

        public DateTime Miesiac { get; set; }

        public decimal GodzinPrzepracowanych { get; set; }
    }
}
