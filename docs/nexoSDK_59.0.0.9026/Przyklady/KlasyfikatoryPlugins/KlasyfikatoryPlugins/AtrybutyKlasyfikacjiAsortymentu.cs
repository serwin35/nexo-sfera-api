using InsERT.Moria.Asortymenty;
using InsERT.Moria.Klasyfikatory;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Promocje;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;

namespace KlasyfikatoryPlugins
{
    public class AtrybutyKlasyfikacjiAsortymentu : IGrupaAtrybutowKlasyfikacji
    {
        private readonly IJednostkiMiar _jednostkiMiar;
        private readonly IAsortymenty _asortymenty;
        private readonly IPromocje _promocje;

        public AtrybutyKlasyfikacjiAsortymentu(
            IJednostkiMiar jednostkiMiar,
            IAsortymenty asortymenty,
            IPromocje promocje)
        {
            _jednostkiMiar = jednostkiMiar;
            _asortymenty = asortymenty;
            _promocje = promocje;
        }

        public IEnumerable<AtrybutKlasyfikacji> Atrybuty
        {
            get
            {
                yield return new AtrybutKlasyfikacji_JednostkaMiary(_jednostkiMiar);
                yield return new AtrybutKlasyfikacji_KodCN(_asortymenty);
                yield return new AtrybutKlasyfikacji_StawkaVat();
                yield return AtrybutKlasyfikacjiPola.UtworzDlaPola<Asortyment>("Minimalna marża", nameof(Asortyment.MinimalnaMarza));
                yield return new AtrybutKlasyfikacji_TowaryObjetePromocja(_promocje);
            }
        }

        public Guid Identyfikator => new Guid("089F02CC-6CA0-46D6-8D4F-3EE3409630BF");

        public string Nazwa => "Atrybuty klasyfikacji asortymentu";

        public string Opis => "Grupa atrybutów klasyfikacji asortymentu";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();
    }
}
