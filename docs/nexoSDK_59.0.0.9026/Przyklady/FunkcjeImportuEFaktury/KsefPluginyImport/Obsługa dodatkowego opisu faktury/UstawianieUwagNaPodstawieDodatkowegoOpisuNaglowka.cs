using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;

namespace KsefPluginyImport
{
    /// <summary>
    /// Funkcja przenosi dodatkowy opis faktury w postaci "{KLUCZ}: {WARTOSC}" oddzielone znakiem nowej linii do uwag na dokumencie.
    /// </summary>
    public class UstawianieUwagNaPodstawieDodatkowegoOpisuNaglowka : IFunkcjaObslugiDodatkowegoOpisuFaktury
    {
        private readonly Guid _id = new Guid("3EAE859D-B3D6-4DC0-A290-C45FA02DC334");

        public string DomyslnyKlucz => "UWAGI";

        public Guid Identyfikator => _id;

        public string Nazwa => "Ustawianie uwag z dodatkowego opisu";

        public string Opis => "Funkcja ustawia uwagi na dokumencie na podstawie dodatkowego opisu w postaci \"{KLUCZ}: {WARTOSC}\"";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public bool CzyMoznaUzyc(IDokument dokument) => true;

        public void Obsluz(IDokument dokument, IEnumerable<DodatkowyOpisEFaktury> dodatkowyOpis)
        {
            string uwagi = string.Empty;
            foreach (DodatkowyOpisEFaktury dane in dodatkowyOpis)
            {
                uwagi += $"{dane.Klucz}: {dane.Wartosc}{Environment.NewLine}";
            }
            uwagi = $"{(string.IsNullOrWhiteSpace(dokument.Dokument.Uwagi) ? string.Empty : Environment.NewLine)}{uwagi.Trim()}";
            dokument.Dokument.Uwagi += uwagi;
        }
    }
}
