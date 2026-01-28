using System;
using InsERT.Moria.KlientPoczty;
using InsERT.Moria.ModelDanych;
using System.Linq.Expressions;

namespace AutoresponderMailowy
{
	class MinimalnaCenaDlaGenerowaniaDzialania : IDefinicjaParametruReguly
    {
        public MinimalnaCenaDlaGenerowaniaDzialania()
        {
            Nazwa = "Próg ceny towarów:";
            Typ = TypParametruCzynnosciReguly.Decimal;
        }

        public string Nazwa { get; set; }

        public TypParametruCzynnosciReguly Typ { get; set; }

        public Type TypObiektu { get; set; }

        public Func<RegulaWiadomosciPocztowych, Expression<Func<object, bool>>> Filtr { get; set; }
    }
}
