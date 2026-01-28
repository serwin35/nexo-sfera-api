using InsERT.Moria.Intrastat;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Linq.Expressions;

namespace IntrastatPluginy.Opis
{
    /// <summary>
    /// Generuje opis asortymentu do deklaracji INTRASTAT na podstawie drugiej linii opisu asortymentu z kartoteki.
    /// </summary>
    public class OpisDoIntrastatuZOpisuAsortymentu : IFunkcjaGenerowaniaOpisuDlaIntrastatu
    {
        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("3E10E5D3-DD79-4FCB-A753-90F5E8F5CA91");

        public string Nazwa => "Druga linia opisu asortymentu";

        public string Opis => "Pobiera opis do deklaracji INTRASTAT z drugiej linii opisu asortymentu.";

        public Expression<Func<PozycjaDokumentu, OpisDoIntrastatu>> Wyrazenie => p => new OpisDoIntrastatu()
        {
            Opis = p.AsortymentWybrany.Opis,
            PozycjaId = p.Id
        };

        public Func<OpisDoIntrastatu, string> OperacjaPrzetworzeniaOpisu => opis =>
        {
            string[] split = opis.Opis.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length > 1)
                return split[1];
            return split[0];
        };

        public bool OperujeNaPolachWlasnych => false;
    }
}