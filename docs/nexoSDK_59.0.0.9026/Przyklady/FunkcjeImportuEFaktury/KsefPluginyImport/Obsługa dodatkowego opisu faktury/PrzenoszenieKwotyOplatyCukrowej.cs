using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KsefPluginyImport
{
    /// <summary>
    /// Funkcja przenosi kwotę opłaty cukrowej zapisaną w jednym z dodatkowych opisów faktury do pola własnego na dokumencie.
    /// </summary>
    public class PrzenoszenieKwotyOplatyCukrowej : IFunkcjaObslugiDodatkowegoOpisuFaktury
    {
        private static readonly string NazwaPolaWlasnego = "Opłata cukrowa";
        private readonly Guid _id = new Guid("C3A233F8-21E1-4100-8333-1E555479876C");
        private readonly IPolaWlasneAdv2AccessorFactory _polaWlasneAccessorFactory;
        private readonly IZaawansowanePolaWlasne _zaawansowanePolaWlasne;

        public PrzenoszenieKwotyOplatyCukrowej(IPolaWlasneAdv2AccessorFactory polaWlasneAccessorFactory
            , IZaawansowanePolaWlasne zaawansowanePolaWlasne)
        {
            _polaWlasneAccessorFactory = polaWlasneAccessorFactory ?? throw new ArgumentNullException(nameof(polaWlasneAccessorFactory));
            _zaawansowanePolaWlasne = zaawansowanePolaWlasne ?? throw new ArgumentNullException(nameof(zaawansowanePolaWlasne));
        }

        public string DomyslnyKlucz => "OPLATACUKROWA";

        public Guid Identyfikator => _id;

        public string Nazwa => "Przenoszenie kwoty opłaty cukrowej";

        public string Opis => "Funkcja przenosi kwotę opłaty cukrowej do pola własnego";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public bool CzyMoznaUzyc(IDokument dokument)
        {
            Type dokumentType = dokument.Dokument.GetType();
            return _zaawansowanePolaWlasne.ObslugujeZaawansowanePolaWlasne(dokumentType)
                && _zaawansowanePolaWlasne.PobierzZaawansowanePoleWlasne(dokumentType, NazwaPolaWlasnego) != null;
        }

        public void Obsluz(IDokument dokument, IEnumerable<DodatkowyOpisEFaktury> dodatkowyOpis)
        {
            if (dodatkowyOpis.Count() == 1) // zakładamy, że będzie tylko jeden element dodatkowego opisu z kwotą opłaty cukrowej
            {
                string wartosc = dodatkowyOpis.Single().Wartosc;
                if (!string.IsNullOrWhiteSpace(wartosc)
                    && decimal.TryParse(wartosc, out decimal kwota))
                {
                    IPolaWlasneAdv2Accessor accessor = _polaWlasneAccessorFactory.Utworz(dokument.Dokument);
                    accessor.UstawWartoscTypuLiczbaRzeczywista(NazwaPolaWlasnego, kwota);
                }
            }
        }
    }
}
