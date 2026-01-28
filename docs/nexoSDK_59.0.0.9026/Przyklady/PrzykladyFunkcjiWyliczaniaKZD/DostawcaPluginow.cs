using System.Collections.Generic;
using InsERT.Moria.Rozszerzanie;

namespace PrzykladyFunkcjiWyliczaniaKZD
{
    internal class DostawcaPluginow : IDostawcaPluginow
    {
        public string Adres => "<Adres>";

        public string AdresWWW => "<AdresWWW>";

        public IEnumerable<string> Kontakty => new string[] { "<email>" };

        public string KRS => "<KRS>";

        public string Nazwa => "<Nazwa>";

        public string NIP => "<NIP>";

        public string REGON => "<REGON>";
    }
}