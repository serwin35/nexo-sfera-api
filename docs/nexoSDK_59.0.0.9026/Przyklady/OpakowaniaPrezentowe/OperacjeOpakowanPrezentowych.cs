using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using System;
using System.Collections.Generic;

namespace OpakowaniaPrezentowe
{
    public class OperacjeOpakowanPrezentowych : IGrupaOperacji
    {
        public static Guid IdentyfikatorGrupy { get; } = new Guid("2A91B0C3-49C0-4A6B-8728-2606638072BA");

        public Guid Identyfikator => IdentyfikatorGrupy;

        public string Nazwa => "Grupa operacji dla opakowań prezentowych";

        public string Opis => "Operacje związane z obsługą opakowań prezentowych";

        public IDostawcaPluginow Dostawca { get; } = new DostawcaPluginow();

        public IEnumerable<Operacja> Operacje { get; } = new Operacja[]
        {
            new Operacja_Formatka_EdytujDedykacje(),
            new Operacja_Lista_UtworzPlikDlaMaszynyGrawerujacej(),
            new Operacja_Formatka_UtworzPlikDlaMaszynyGrawerujacej(),
        };
    }
}