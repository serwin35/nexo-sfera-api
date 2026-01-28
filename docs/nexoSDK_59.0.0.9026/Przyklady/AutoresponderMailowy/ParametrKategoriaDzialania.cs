using System;
using InsERT.Moria.KlientPoczty;
using InsERT.Moria.ModelDanych;
using System.Linq.Expressions;

namespace AutoresponderMailowy
{
	class ParametrKategoriaDzialania : IDefinicjaParametruReguly
    {
        public ParametrKategoriaDzialania()
        {
            Nazwa = "Kategoria działania:";
            Typ = TypParametruCzynnosciReguly.Obiekt;
            TypObiektu = typeof(KategoriaDzialania);
            Filtr = x => o => true;            
        }

        public string Nazwa { get; set; }

        public TypParametruCzynnosciReguly Typ { get; set; }

        public Type TypObiektu { get; set; }

        public Func<RegulaWiadomosciPocztowych, Expression<Func<object, bool>>> Filtr { get; set; }
    }
}
