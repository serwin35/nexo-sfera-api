using InsERT.Moria;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Sfera;
using InsERT.Moria.SladRewizyjny;
using InsERT.Moria.Wspolne;
using InsERT.Moria.Wspolne.RegulyAutomatyzacji;
using InsERT.Mox.Formatting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutomatyzacjaPrzyklady
{
    /// <summary>
    /// Czynność dostępna w Harmonogramie w Automatach. Automatycznie czyści ślad rewizyjny z ostatnich X dni (do wybrania w parametrze czynności).
    /// </summary>
    public class CzynnoscWyczyscSladRewizyjny : ICzynnoscRegulyAutomatyzacji
    {
        private readonly Func<IZdarzeniaSladuRewizyjnego> _zdarzeniaSladuRewizyjnego;
        private readonly Func<IDataSystemowa> _dataSystemowa;
        public CzynnoscWyczyscSladRewizyjny(
            Func<IZdarzeniaSladuRewizyjnego> zdarzeniaSladuRewizyjnego,
            Func<IDataSystemowa> dataSystemowa
            )
        {
            _zdarzeniaSladuRewizyjnego = zdarzeniaSladuRewizyjnego;
            _dataSystemowa = dataSystemowa;
        }
        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("8d5ffba3-3215-401b-9292-9a056b81437e");

        public string Nazwa => "Wyczyść ślad rewizyjny";

        public string Opis => "Czynność czyszcząca zdarzenia śladu rewizyjnego";

        public MiejsceWykorzystaniaObiektuAutomatyzacji MiejsceWykorzystania => MiejsceWykorzystaniaObiektuAutomatyzacji.Harmonogram;

        public IEnumerable<IDefinicjaParametruCzynnosciRegulyAutomatyzacji> WymaganeParametry => new[]
        {
            ParametryAutomatow.Czynnosc.LiczbaCalkowita("Liczba ostatnich dni", wartoscDomyslna: 7)
        };

        public DescriptiveBoolean Waliduj(IKontekstWykonywaniaCzynnosciRegulyAutomatyzacji kontekst)
        {
            if (kontekst.WybraneParametry.Count != 1 || !(kontekst.WybraneParametry[0] is int liczbaDni))
            {
                return DescriptiveBoolean.Error("Niepoprawne wartości parametrów");
            }

            if (liczbaDni <= 0)
            {
                return DescriptiveBoolean.Error("Niepoprawna wartość parametru \"Liczba ostatnich dni\"");
            }

            return DescriptiveBoolean.True;
        }

        public DescriptiveBoolean Wykonaj(IKontekstWykonywaniaCzynnosciRegulyAutomatyzacji kontekst)
        {
            int liczbaDniDoWyczyszczenia = (int)kontekst.WybraneParametry[0];
            DateTime ostatniaDopuszczalnaData = _dataSystemowa().AktualnyCzas().AddDays(-liczbaDniDoWyczyszczenia);


            List<ZdarzenieSladuRewizyjnego> zdarzeniaDoUsuniecia = _zdarzeniaSladuRewizyjnego().Dane.Wszystkie(z => z.Data > ostatniaDopuszczalnaData).ToList();
            if (!UsunZdarzenia(zdarzeniaDoUsuniecia, out int liczbaNieusunietych))
            {
                return DescriptiveBoolean.Error(UtworzKomunikatBledu(liczbaNieusunietych, liczbaNieusunietych != zdarzeniaDoUsuniecia.Count));
            }
            else
            {
                return DescriptiveBoolean.True;
            }
        }

        private bool UsunZdarzenia(List<ZdarzenieSladuRewizyjnego> zdarzeniaDoUsuniecia, out int liczbaNieusunietych)
        {
            liczbaNieusunietych = 0;
            foreach (var zdarzenie in zdarzeniaDoUsuniecia)
            {
                IZdarzenieSladuRewizyjnego bo = _zdarzeniaSladuRewizyjnego().Znajdz(zdarzenie);

                if (!bo.MoznaUsunac)
                {
                    liczbaNieusunietych++;
                }
                else
                {
                    bo.Usun();
                }
            }

            return liczbaNieusunietych == 0;
        }

        private string UtworzKomunikatBledu(int liczbaNieusunietych, bool czyCzesciowaPorazka)
        {
            if (czyCzesciowaPorazka)
            {
                return $"Operacja częściowo nieudana. Nie udało się usunąć {liczbaNieusunietych} {(liczbaNieusunietych == 1 ? "zdarzenia" : "zdarzeń")} śladu rewizyjnego.";
            }
            else
            {
                return "Operacja nieudana. Zdarzenia śladu rewizyjnego nie zostały usunięte.";
            }
        }
    }
}