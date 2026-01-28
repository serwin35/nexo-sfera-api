using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using System;
using System.Collections.Generic;

namespace OperacjeSferyczne.OperacjeNaAsortymencie
{
    public class GrupaOperacjiNaAsortymencie : IGrupaOperacji
    {
        public IEnumerable<Operacja> Operacje { get; } = new Operacja[]
        {
            new UstawStroneWWW()
        };

        public Guid Identyfikator => new Guid("5AE7C74C-D364-43CB-95C7-CED2961E0D49");

        public string Nazwa => "Operacje na asortymencie";

        public string Opis => "Grupa operacji na asortymencie";

        public IDostawcaPluginow Dostawca { get; } = new DostawcaPluginow();
    }
}
