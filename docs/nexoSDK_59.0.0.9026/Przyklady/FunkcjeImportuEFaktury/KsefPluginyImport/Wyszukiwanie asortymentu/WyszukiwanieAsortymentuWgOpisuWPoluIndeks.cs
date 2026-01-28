using InsERT.Moria.Asortymenty;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KsefPluginyImport
{
    /// <summary>
    /// Funkcja zakłada, że w polu DodatkoweInfo/Indeks w wierszu faktury podany będzie opis asortymentu. Funkcja wyszukuje asortyment po tym opisie wpisanym w kartotece asortymentu.
    /// </summary>
    public class WyszukiwanieAsortymentuWgOpisuWPoluIndeks : IFunkcjaWyszukiwaniaAsortymentu
    {
        private readonly Guid _id = new Guid("68EBCF6F-F596-48F9-9153-AFB493287905");
        private readonly IAsortymenty _asortymentyMenedzer;

        public WyszukiwanieAsortymentuWgOpisuWPoluIndeks(IAsortymenty asortymentyMenedzer)
        {
            _asortymentyMenedzer = asortymentyMenedzer ?? throw new ArgumentNullException(nameof(asortymentyMenedzer));
        }

        public Guid Identyfikator => _id;

        public string Nazwa => "Wg opisu w polu DodatkoweInfo/Indeks";

        public string Opis => "Funkcja wyszukuje asortyment wg opisu podanego w polu DodatkoweInfo lub Indeks e-Faktury.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public List<Asortyment> Wyszukaj(DokumentElektroniczny dokumentElektroniczny, IDaneWierszaFaktury daneWiersza)
        {
            string dane = string.IsNullOrWhiteSpace(daneWiersza.DodatkoweInfo) ? daneWiersza.Indeks : daneWiersza.DodatkoweInfo;
            if (string.IsNullOrWhiteSpace(dane))
                return null; // nie podano danych w polu DodatkoweInfo/Indeks
            if (!dokumentElektroniczny.PodmiotId.HasValue)
                return null; // e-Faktura nie została przypisana do dostawcy
            Asortyment znaleziony = (from asortyment in _asortymentyMenedzer.Dane.Wszystkie()
                                     where asortyment.Opis == dane
                                     select asortyment).FirstOrDefault();
            return znaleziony == null ? null :
                new List<Asortyment>() { znaleziony };
        }
    }
}
