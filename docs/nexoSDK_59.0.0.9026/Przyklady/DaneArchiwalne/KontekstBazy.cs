using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaneArchiwalne
{
    public interface IKontekstBazy
    {
        string Serwer { get; }
        string Baza { get; }
        string Login { get; }
        string Haslo { get; }
        bool ZaufanePolaczenie { get; }
    }

    public class KontekstBazy : IKontekstBazy
    {
        public string Serwer { get; set; }
        public string Baza { get; set; }
        public string Login { get; set; }
        public string Haslo { get; set; }
        public bool ZaufanePolaczenie { get; set; }
    }
}
