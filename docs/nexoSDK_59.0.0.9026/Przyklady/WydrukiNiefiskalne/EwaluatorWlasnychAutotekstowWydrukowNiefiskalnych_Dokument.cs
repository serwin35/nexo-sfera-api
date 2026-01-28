using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Urzadzenia.Core;

namespace NexoPlugins
{
    public class EwaluatorWlasnychAutotekstowWydrukowNiefiskalnych_Dokument : IEwaluatorAutotekstowWlasnychWydrukowNiefiskalnych
    {
        private readonly Guid _id;

        public EwaluatorWlasnychAutotekstowWydrukowNiefiskalnych_Dokument()
        {
            _id = new Guid("994B68AD-903E-424B-B291-37D943C7804C");
        }

        public ISet<string> ObslugiwaneAutoteksty => new HashSet<string>() { "suma płatności natychmiastowych", "wystawiony na", "cennik główny" };

        public Guid Identyfikator => _id;

        public string Nazwa => "Własne dokumentowe";

        public string Opis => "Obsługuje dodatkowe autoteksty dla dokumentu.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string Ewaluuj(string wzorzec, IZawartoscWydrukuNiefiskalnego dane, out bool zakonczonaPowodzeniem)
        {
            if (dane.Dane is Dokument dokument)
            {
                zakonczonaPowodzeniem = true;
                switch(wzorzec)
                {
                    case "suma platnosci natychmiastowych":
                        return string.Format("{0:N" + dokument.Waluta.Precyzja + "}", dokument.PlatnosciDokumentow.Where(p => p.RodzajPlatnosci == (int)RodzajPlatnosci.Natychmiastowa).Select(p => p.KwotaDokumentu).DefaultIfEmpty(0m).Sum());
                    case "cennik glowny":
                        return dokument.PoziomCen != null ? dokument.PoziomCen.Nazwa : string.Empty;
                    case "wystawiony na":
                        {
                            return dokument is DokumentHandlowy handlowy ? (handlowy.WystawionyNaFirme ? "firmę" : "osobę") : string.Empty;
                        }
                }
            }
            zakonczonaPowodzeniem = false;
            return string.Empty;
        }
    }
}
