using InsERT.Moria.Intrastat;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Linq.Expressions;

namespace IntrastatPluginy.Opis
{
    /// <summary>
    /// Generuje opis asortymentu do deklaracji INTRASTAT na podstawie pola własnego pozycji dokumentu typu 'Tekst' o nazwie 'Intrastat'.
    /// </summary>
    public class OpisDoIntrastatuZPolaWlasnegoPlugin : IFunkcjaGenerowaniaOpisuDlaIntrastatu
    {
        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("78F61A05-5F20-4344-A21E-74DF147E736E");

        public string Nazwa => "Pole własne pozycji 'Intrastat'";

        public string Opis => "Pobiera opis do deklaracji INTRASTAT z pola własnego pozycji dokumentu o nazwie 'Intrastat'";

        public Expression<Func<PozycjaDokumentu, OpisDoIntrastatu>> Wyrazenie => p => new OpisDoIntrastatu()
        {
            Opis = p.PolaWlasneAdv2.Get<string>("Intrastat"),
            PozycjaId = p.Id
        };

        public Func<OpisDoIntrastatu, string> OperacjaPrzetworzeniaOpisu => opis => opis.Opis; // bez przetworzenia

        public bool OperujeNaPolachWlasnych => true;
    }
}