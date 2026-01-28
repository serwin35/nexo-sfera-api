using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using System;
using System.Collections.Generic;

namespace OperacjeSferyczne.ImportEksportPracownikow
{
    public class GrupaOperacji : IGrupaOperacji
    {
        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("d1fb77d2-f5ea-4b3f-aa1c-88b6a961ef6c");

        public string Nazwa => "Import oraz eksport pracowników";

        public string Opis => "Grupa operacji w module pracowników pomocna do przenoszenia danych pracowników między podmiotami.";

        public IEnumerable<Operacja> Operacje { get; } = new Operacja[]
        {
            new OperacjaEksportuPracownikow(),
            new OperacjaImportuPracownikow()
        };
    }
}