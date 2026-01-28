using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Linq;

namespace KsefPluginyImport
{
    /// <summary>
    /// Funkcja ustawia uwagi na dokumencie na podstawie wszystkich danych wpisanych w sekcji DodatkowyOpis rozdzielonych znakiem nowej linii.
    /// W przypadku dodatkowego opisu całego dokumentu wiersz ma postać "DOKUMENT {KLUCZ}: {WARTOSC}".
    /// W przypadku dodatkowego opisu pozycji wiersz ma postać "POZYCJA {LP} {KLUCZ}: {WARTOSC}".
    /// </summary>
    public class UstawianieUwagNaPodstawieDodatkowegoOpisu : IDodatkowaFunkcjaImportuFaktury
    {
        private readonly Guid _id = new Guid("008FD48F-C0A7-425F-B196-24B5C136D5D4");

        public Guid Identyfikator => _id;

        public string Nazwa => "Ustawianie uwag na podstawie dodatkowego opisu";

        public string Opis => "Funkcja ustawia uwagi na dokumencie na podstawie wszystkich danych w sekcji DodatkowyOpis.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public void Przetworz(IDokument dokument, IDaneEFaktury daneFaktury, string trescXml)
        {
            string uwagi = string.Empty;
            foreach (DodatkowyOpisEFaktury dodatkowyOpis in daneFaktury.DodatkowyOpis.OrderBy(d => d.NrWiersza).ThenBy(d => d.Klucz))
            {
                uwagi += $"{(dodatkowyOpis.NrWiersza.HasValue ? $"POZYCJA {dodatkowyOpis.NrWiersza.Value}" : "DOKUMENT")} {dodatkowyOpis.Klucz}: {dodatkowyOpis.Wartosc}{Environment.NewLine}";
            }
            dokument.Dokument.Uwagi = uwagi.Trim();
        }
    }
}
