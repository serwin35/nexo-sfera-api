using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Linq;

namespace KsefPluginyImport
{
    /// <summary>
    /// Wyszukuje klienta w kartotece wg nazwy handlowej wpisanej do pola własnego.
    /// </summary>
    public class WyszukiwanieKlientaWgPolaWlasnego : IFunkcjaWyszukiwaniaPodmiotu
    {
        private readonly Guid _id = new Guid("E43DBE81-64D2-49E8-A322-64C4062C691F");
        private readonly IPodmioty _podmiotyMenedzer;
        private readonly IProstePolaWlasne _prostePolaWlasne;

        public WyszukiwanieKlientaWgPolaWlasnego(IPodmioty podmiotyMenedzer
            , IProstePolaWlasne prostePolaWlasne)
        {
            _podmiotyMenedzer = podmiotyMenedzer ?? throw new ArgumentNullException(nameof(podmiotyMenedzer));
            _prostePolaWlasne = prostePolaWlasne ?? throw new ArgumentNullException(nameof(prostePolaWlasne));
        }

        public Guid Identyfikator => _id;

        public string Nazwa => "Wg nazwy handlowej w polu własnym";

        public string Opis => "Funkcja wyszukuje klienta wg nazwy handlowej wpisanej do pola własnego.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        /// <param name="_">W tym przykładzie parametr z encją e-Faktury jest nieużywany więc można go zignorować.</param>
        public PodmiotHistoria Wyszukaj(DokumentElektroniczny _, IDanePodmiotuFaktury danePodmiotu)
        {
            string nazwa = string.IsNullOrEmpty(danePodmiotu.NazwaHandlowa) ? danePodmiotu.Nazwa : danePodmiotu.NazwaHandlowa;
            if (string.IsNullOrWhiteSpace(nazwa))
                return null;
            if (_prostePolaWlasne.MaProstePolaWlasne(typeof(Podmiot)))
            {
                IProstePoleWlasne poleNazwaHandlowa = _prostePolaWlasne.PobierzProstePolaWlasne(typeof(Podmiot)).Where(p => p.Nazwa == "Nazwa handlowa").FirstOrDefault();
                if (poleNazwaHandlowa == null || !poleNazwaHandlowa.Widoczne)
                    return null;
                IQueryable<Podmiot> query = _podmiotyMenedzer.Dane.Wszystkie();
                switch (poleNazwaHandlowa.Id)
                {
                    case "PoleWlasne1":
                        query = query.Where(p => p.PolaWlasne.PoleWlasne1.Equals(nazwa));
                        break;
                    case "PoleWlasne2":
                        query = query.Where(p => p.PolaWlasne.PoleWlasne2.Equals(nazwa));
                        break;
                    case "PoleWlasne3":
                        query = query.Where(p => p.PolaWlasne.PoleWlasne3.Equals(nazwa));
                        break;
                    case "PoleWlasne4":
                        query = query.Where(p => p.PolaWlasne.PoleWlasne4.Equals(nazwa));
                        break;
                    default:
                        return null;
                }
                return query.Select(p => p.Aktualny).FirstOrDefault();
            }
            return null;
        }
    }
}
