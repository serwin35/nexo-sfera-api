using InsERT.Moria.Asortymenty;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Linq;
using InsERT.Mox.DataExtensions;
using System.Collections.Generic;

namespace KsefPluginyImport
{
    /// <summary>
    /// Funkcja wyszukuje asortyment na podstawie nazwy podanej w polu własnym tekstowym asortymentu.
    /// </summary>
    public class WyszukiwanieAsortymentuWgNazwyWPoluWlasnym : IFunkcjaWyszukiwaniaAsortymentu
    {
        private readonly Guid _id = new Guid("9A4DB8F6-2881-4AF3-AF52-A68D4A06B9AB");
        private readonly IAsortymenty _asortymentyMenedzer;

        public WyszukiwanieAsortymentuWgNazwyWPoluWlasnym(IAsortymenty asortymentyMenedzer)
        {
            _asortymentyMenedzer = asortymentyMenedzer ?? throw new ArgumentNullException(nameof(asortymentyMenedzer));
        }

        public Guid Identyfikator => _id;

        public string Nazwa => "Wg nazwy w polu własnym";

        public string Opis => "Funkcja wyszukuje asortyment wg nazwy przechowywanej w polu własnym zaawansowanym.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        /// <param name="_">W tym przykładzie parametr z encją e-Faktury jest nieużywany więc można go zignorować.</param>
        public List<Asortyment> Wyszukaj(DokumentElektroniczny _, IDaneWierszaFaktury daneWiersza)
        {
            if (string.IsNullOrWhiteSpace(daneWiersza.NazwaTowaru))
                return null; // nie podano nazwy asortymentu w wierszu faktury
            Asortyment znaleziony = (from asortyment in _asortymentyMenedzer.Dane.Wszystkie()
                                     let nazwa = asortyment.PolaWlasneAdv2.Get<string>("nazwa własna")
                                     where nazwa.Equals(daneWiersza.NazwaTowaru)
                                     select asortyment).ResolveExtensionProperties().FirstOrDefault();
            return znaleziony == null ? null :
                new List<Asortyment>() { znaleziony };
        }
    }
}
