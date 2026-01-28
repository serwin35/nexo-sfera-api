using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KsefPluginyImport
{
    public class PrzenoszenieMasyPozycjiDoOpisu : IFunkcjaObslugiDodatkowegoOpisuPozycji
    {
        private readonly Guid _id = new Guid("577E2426-FA17-465E-8AB9-57C571583002");

        public string DomyslnyKlucz => "MASA";

        public Guid Identyfikator => _id;

        public string Nazwa => "Przenoszenie masy do opisu pozycji";

        public string Opis => "Funkcja przepisuje wartość z dodatkowego opisu do opisu pozycji w postaci \"MASA: {WARTOSC} {J.M.}\".";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public bool CzyMoznaUzyc(IDokument dokument, PozycjaDokumentu pozycja) => pozycja.JednostkaMiaryAs != null && pozycja.JednostkaMiaryAs.JednostkaMiaryMasy != null;

        public void Obsluz(IDokument dokument, PozycjaDokumentu pozycja, IEnumerable<DodatkowyOpisEFaktury> dodatkowyOpis)
        {
            if (dodatkowyOpis.Count() == 1)
            {
                string wartosc = $"MASA: {dodatkowyOpis.Single().Wartosc} {pozycja.JednostkaMiaryAs.JednostkaMiaryMasy.Symbol}";
                if (!string.IsNullOrWhiteSpace(pozycja.Opis))
                    wartosc = Environment.NewLine + wartosc;
                pozycja.Opis += wartosc;
            }
        }
    }
}
