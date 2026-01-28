using InsERT.Moria.Asortymenty;
using InsERT.Moria.Klasyfikatory;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie.Operacje;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace KlasyfikatoryPlugins
{
    public class AtrybutKlasyfikacji_JednostkaMiary: AtrybutKlasyfikacji<Asortyment>
    {
        private readonly IJednostkiMiar _jednostkiMiar;

        public AtrybutKlasyfikacji_JednostkaMiary(
            IJednostkiMiar jednostkiMiar)
        {
            _jednostkiMiar = jednostkiMiar;
        }

        public override string Nazwa => "Jednostka miary";

        public override ZrodloDanychAtrybutuKlasyfikacji UtworzZrodloDanych(IKontekstKlasyfikacji kontekst)
            => kontekst.UtworzZrodloDanychDlaTypu(typeof(JednostkaMiary));

        protected override void Przypisz(Asortyment obiekt, IWirtualnyElementKlasyfikujacy element, IKontekstKlasyfikowanegoObiektu kontekst)
        {
            var id = int.Parse(element.Wartosc);
            var jednostka = _jednostkiMiar.Dane.Wszystkie().FirstOrDefault(j => j.Id == id);

            ((IAsortyment)kontekst.ObiektBiznesowy).JednostkiMiary.DodajJednostkeMiary(jednostka, obiekt.PodstawowaJednostkaMiaryAsortymentu);
        }

        protected override WynikOperacji SprawdzCzyMoznaPrzypisac(Asortyment obiekt, IWirtualnyElementKlasyfikujacy element, IKontekstKlasyfikowanegoObiektu kontekst)
        {
            if (!int.TryParse(element.Wartosc, out var id))
            {
                return WynikOperacji.ZakonczonaNiepowodzeniem($"Nieprawidłowa wartość: {element.Wartosc}");
            }

            if (obiekt.JednostkiMiar.Any(j => j.JednostkaMiary.Id == id))
            {
                return WynikOperacji.ZakonczonaPowodzeniem("Jednostka miary została już przypisana do asortymentu.");
            }

            if (!_jednostkiMiar.Dane.Wszystkie().Any(j => j.Id == id))
            {
                return WynikOperacji.ZakonczonaNiepowodzeniem($"Nie znaleziono jednostki miary o identyfikatorze: {id}");
            }

            return WynikOperacji.GotowaDoWykonania();
        }

        protected override Expression<Func<Asortyment, bool>> UtworzWyrazenieFiltrujace(IWirtualnyElementKlasyfikujacy element, IKontekstKlasyfikacji kontekst)
        {
            if (!int.TryParse(element.Wartosc, out var id))
            {
                return null;
            }

            return asortyment => asortyment.JednostkiMiar.Any(j => j.JednostkaMiary.Id == id);
        }
    }
}
