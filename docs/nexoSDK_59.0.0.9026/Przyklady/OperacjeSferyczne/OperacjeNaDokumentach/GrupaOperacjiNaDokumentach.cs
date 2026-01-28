using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using System;
using System.Collections.Generic;

namespace OperacjeSferyczne.OperacjeNaDokumentach
{
    public class GrupaOperacjiNaDokumentach : IGrupaOperacji
    {
        public IEnumerable<Operacja> Operacje { get; } = new Operacja[]
        {
            new UsunPozycje(),
            new ZmienPolaWlasne(),
        };

        public Guid Identyfikator => new Guid("6408E862-26DA-4222-9DAF-87803E9A8E18");

        public string Nazwa => "Operacje na dokumentach";

        public string Opis => "Grupa operacji na dokumentach";

        public IDostawcaPluginow Dostawca { get; } = new DostawcaPluginow();
    }
}
