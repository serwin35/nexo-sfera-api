using System.Collections.Generic;
using InsERT.Moria.Rozszerzanie;

namespace NaglowkiWydrukow
{
    public class DostawcaPluginow : IDostawcaPluginow
    {
        public string Nazwa => "Firma przykładowa";

        public string AdresWWW => "www.przyklad.com";

        public string NIP => "888-888-88-88";

        public string REGON => "0123456789";

        public string KRS => "9876543210";

        public string Adres => "ul. Piękna 3/4, 22-888 Inowrocław";

        public IEnumerable<string> Kontakty
        {
            get
            {
                yield return "kontakt@przyklad.com";
            }
        }
    }
}
