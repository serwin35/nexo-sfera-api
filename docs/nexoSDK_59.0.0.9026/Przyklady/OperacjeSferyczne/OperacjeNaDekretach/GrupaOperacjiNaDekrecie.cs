using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using System;
using System.Collections.Generic;

namespace OperacjeSferyczne.OperacjeNaDekretach
{
    public class GrupaOperacjiNaDekrecie : IGrupaOperacji
    {
        public IEnumerable<Operacja> Operacje { get; } = new Operacja[]
        {
            new DodajGrupe1Do1ZNowymZapisemRozrachunkowym(),
        };

        public Guid Identyfikator => new Guid("5AE71CBA-8FB1-40D3-91F8-4202F87C29A3");

        public string Nazwa => "Operacje na dekretach";

        public string Opis => "Grupa operacji na dekretach";

        public IDostawcaPluginow Dostawca { get; } = new DostawcaPluginow();
    }
}
