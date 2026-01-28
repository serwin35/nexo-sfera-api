using InsERT.Moria.Rozszerzanie;
using System.Collections.Generic;

namespace WartoscSkladnikaPlacowegoPlugins
{
    public class DostawcaPluginowInsERT : IDostawcaPluginow
    {
        public string Adres { get { return "ul. Jerzmanowska 2, 54-519 Wrocław"; } }
        public string AdresWWW { get { return "www.insert.com.pl"; } }
        public IEnumerable<string> Kontakty { get { yield return "tel. +48 71 78 76 100"; yield return "email: office@insert.com.pl"; } }
        public string KRS { get { return "0000306888"; } }
        public string Nazwa { get { return "InsERT S.A."; } }
        public string NIP { get { return "898-19-45-134"; } }
        public string REGON { get { return "932283479"; } }
    }
}
