using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ObslugaLogistyki.Pomocnicze
{
    public class DaneCzasuDostawy
    {
        public DaneCzasuDostawy(DateTime data)
        {
            DataPoczatkowa = DataKoncowa = data;
            GodzinaPoczatkowa = new TimeSpan(8, 0, 0);
            GodzinaKoncowa = new TimeSpan(11, 0, 0);
        }
        public DaneCzasuDostawy(DateTime data, TimeSpan poczatek)
        {
            DataPoczatkowa = DataKoncowa = data;
            GodzinaPoczatkowa = poczatek;
            GodzinaKoncowa = poczatek.Add(new TimeSpan(3, 0, 0));
        }
        public DateTime DataPoczatkowa { get; }
        public TimeSpan GodzinaPoczatkowa { get; }
        public DateTime DataKoncowa { get; }
        public TimeSpan GodzinaKoncowa { get; }
    }
}
