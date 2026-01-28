using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.PolaWlasne2;

namespace RozwiazanieWlasne
{
    public class ProstePolaWlasneTester
    {
        private readonly IProstePolaWlasne _prostePolaWlasne;

        public ProstePolaWlasneTester(IProstePolaWlasne prostePolaWlasne)
        {
            if (prostePolaWlasne == null)
            {
                throw new ArgumentNullException(nameof(prostePolaWlasne));
            }
            _prostePolaWlasne = prostePolaWlasne;
        }

        public void Test()
        {
            MaProstePolaWlasne_Test();
            PobierzProstePolaWlasne_Test();
        }

        private void MaProstePolaWlasne_Test()
        {
            bool ma;
            ma = _prostePolaWlasne.MaProstePolaWlasne(typeof(Asortyment));
            ma = _prostePolaWlasne.MaProstePolaWlasne<Asortyment>();
        }

        private void PobierzProstePolaWlasne_Test()
        {
            foreach (IProstePoleWlasne pole in _prostePolaWlasne.PobierzProstePolaWlasne(typeof(Asortyment)))
            {
                SprawdzProstePoleWlasne(pole, false);
            }
            foreach (IProstePoleWlasne pole in _prostePolaWlasne.PobierzProstePolaWlasne<Asortyment>())
            {
                SprawdzProstePoleWlasne(pole, true);
            }
        }

        private void SprawdzProstePoleWlasne(IProstePoleWlasne pole, bool wypiszNaKonsole)
        {
            string id = pole.Id;
            string nazwa = pole.Nazwa;
            bool widoczne = pole.Widoczne;
            if (wypiszNaKonsole)
            {
                Console.WriteLine($"         IdPola={id}, Nazwa={nazwa}, Widoczne={widoczne}");
            }
        }
    }
}
