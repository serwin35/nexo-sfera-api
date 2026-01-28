using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Wydruki.Autoteksty;
using System;
using System.Collections.Generic;

namespace NexoPlugins
{
    public class FunkcjaPrzetwarzaniaTekstu_WielkieLitery : IFunkcjaPrzetwarzaniaTekstu
    {
        private readonly Guid _id;

        public FunkcjaPrzetwarzaniaTekstu_WielkieLitery()
        {
            _id = new Guid("A78527AF-6548-45F3-874E-38AD8DD15482");
        }

        public string PrzykladUzycia => "wielkie litery[tekst]";

        public Guid Identyfikator => _id;

        public string Nazwa => "wielkie litery";

        public string Opis => "Funkcja przetwarzająca tekst na wielkie litery.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string Przetworz(string tekstWejsciowy, object obiektPrzetwarzany)
        {
            return tekstWejsciowy.ToUpper();
        }
    }
}
