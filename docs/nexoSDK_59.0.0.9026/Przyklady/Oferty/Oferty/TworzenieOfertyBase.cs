using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.ModelOrganizacyjny;
using InsERT.Moria.Sfera;
using System;
using System.Linq;

namespace Oferty
{
    internal abstract class TworzenieOfertyBase
    {
        protected readonly Uchwyt _sfera;
        public TworzenieOfertyBase(Uchwyt sfera)
        {
            _sfera = sfera;
        }

        internal abstract string KomunikatRozpoczecia { get; }
        internal abstract void WypelnijDaneOferty(IOferta ofertaBO);

        public void Utworz()
        {
            Console.WriteLine(KomunikatRozpoczecia);
            IOferty oferty = _sfera.PodajObiektTypu<IOferty>();

            // Utworzenie dokumentu wymaga podania konfiguracji.
            // Dlatego najpierw wyszukujemy odpowiednią konfigurację dla tworzonego dokumentu.
            Konfiguracja wybranaKonfiguracja = PodajWybranaKonfiguracje();

            using (IOferta ofertaBO = oferty.Utworz(wybranaKonfiguracja))
            {
                // Tu należy umieścić kod wypełniający dokument poprawnymi danymi.
                // Główna encja z danymi dokumentu jest dostępna poprzez właściwość ofertaBO.Dane

                WypelnijPrzykladowyPodmiot(ofertaBO);
                WypelnijMagazyn(ofertaBO);
                WypelnijDaneOferty(ofertaBO);

                if (!ofertaBO.Zapisz())
                {
                    Console.WriteLine("Podczas tworzenia oferty wystąpiły błędy:");
                    ofertaBO.WypiszBledy(); // Użyta metoda rozszerzająca z klasy Rozszerzenia
                }
                else
                {
                    Console.WriteLine($"Utworzono ofertę {ofertaBO.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                }
            }
        }

        private Konfiguracja PodajWybranaKonfiguracje()
        {
            IKonfiguracje konfiguracje = _sfera.PodajObiektTypu<IKonfiguracje>();

            // W przykładzie używamy konfiguracji domyślnej dla oferty.
            // W tym miejscu można podać dowolną konfigurację, aby stworzyć dokument własnego typu.
            return konfiguracje.Dane.WszystkieOTypieDokumentu(TypDokumentu.Oferta).FirstOrDefault(x => x.Domyslna);
        }

        private void WypelnijPrzykladowyPodmiot(IOferta ofertaBO)
        {
            // W przykładzie używamy dowolnego dostępnego podmiotu.
            Podmiot podmiot = _sfera.PodajObiektTypu<IPodmioty>().Dane.WszystkieDostepne().FirstOrDefault();
            ofertaBO.Dane.Podmiot = podmiot;
        }

        private void WypelnijMagazyn(IOferta ofertaBO)
        {
            // W przykładzie używamy dowolnego dostępnego magazynu.
            Magazyn magazyn = _sfera.PodajObiektTypu<IMagazyny>().Dane.WszystkieDostepne().FirstOrDefault();
            ofertaBO.Dane.Magazyn = magazyn;
        }
    }
}