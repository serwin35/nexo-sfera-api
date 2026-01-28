using InsERT.Moria.Rozszerzanie;
using System.Collections.Generic;

namespace KsefPluginy
{
    public class DostawcaPluginow : IDostawcaPluginow
    {
        public string Adres => "ul. Jerzmanowska 2, 54-519 Wrocław";
        public string AdresWWW => "www.insert.com.pl";
        public IEnumerable<string> Kontakty { get { yield return "tel. +48 71 78 76 100"; yield return "email: office@insert.com.pl"; } }
        public string KRS => "0000306888";
        public string Nazwa => "InsERT S.A.";
        public string NIP => "898-19-45-134";
        public string REGON => "932283479";
    }
}
