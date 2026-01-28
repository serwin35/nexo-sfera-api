using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using ObslugaLogistyki.Parametry;
using ObslugaLogistyki.Pomocnicze;
using ObslugaLogistyki.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ObslugaLogistyki.Automaty
{
    public class AutomatycznaZmianaSamochoduDostawczegoPlugin : KlientSferyZdarzeniowej<IDokument>
    {
        private readonly ObslugaPolWlasnychLogistyki _obslugaPolWlasnych;

        public AutomatycznaZmianaSamochoduDostawczegoPlugin(Func<IPolaWlasneAdv2AccessorFactory> polaWlasneAccessorFactory
            , Func<ISlownikiWlasne> slownikiWlasne
            , Func<IZaawansowanePolaWlasne> zaawansowanePolaWlasne)
        {
            _obslugaPolWlasnych = new ObslugaPolWlasnychLogistyki(polaWlasneAccessorFactory, slownikiWlasne, zaawansowanePolaWlasne);
        }

        public override void PoZmianieWlasciwosciObiektu(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IDokument> kontekst)
        {
            if (!ObslugaLogistykiHelper.SprawdzWarunkiWstepne(kontekst, ParametryObslugi.Instance.AutomatycznieUstawiajSamochodDostawczy))
                return;
            if (kontekst.SposobEdycji != SposobEdycji.Okno)
                return;
            if (!kontekst.NazwaWlasciwosci.Equals(nameof(Dokument.SposobDostawy)))
                return;
            if (kontekst.Dane is Dokument dokument)
            {
                if (ObslugaLogistykiHelper.CzyTransportFirmowy(dokument)) // jeśli transport firmowy -> wyświetlamy okno z wyborem samochodu dostawczego
                {
                    if (!_obslugaPolWlasnych.SprawdzCzyPoleIstniejeIPobierzWybranySamochodDostawczy(dokument).HasValue)
                    {
                        WyborSamochoduDostawczego dialog = UtworzDialogWyboruSamochodu(dokument.SposobDostawy.Nazwa, out string komunikatBledu);
                        if (dialog == null)
                            kontekst.DodajBlad(komunikatBledu ?? "Wystąpił błąd podczas tworzenia okna wyboru samochodu dostawczego.", dokument, nameof(Dokument.SposobDostawy));
                        else if (dialog.ShowDialog() == true && dialog.Item != null)
                            _obslugaPolWlasnych.UstawSamochodDostawczy(dokument, dialog.Item.Klucz, kontekst);
                    }
                }
                else // w przeciwnym wypadku -> w polu własnym 'Samochód dostawczy' ustawiamy (brak)
                {
                    _obslugaPolWlasnych.UstawSamochodDostawczy(dokument, null, kontekst);
                }
            }
            else
                throw new ArgumentException(); // sytuacja niespodziewana
        }     

        /// <summary>
        /// Tworzy okno wyboru samochodu dostawczego.
        /// </summary>
        /// <param name="nazwaSposobuDostawy">Nazwa sposobu dostawy na dokumencie.</param>
        /// <param name="komunikatBledu">Komunikat błędu w przypadku niepoprawnie zdefiniowanego słownika samochodów dostawczych.</param>
        /// <returns>Obiekt okna wyboru samochodu dostawczego lub <c>null</c> w przypadku gdy słownik samochodów dostawczych nie został zdefiniowany lub został zdefiniowany niepoprawnie.</returns>
        private WyborSamochoduDostawczego UtworzDialogWyboruSamochodu(string nazwaSposobuDostawy, out string komunikatBledu)
        {
            List<ElementSlownikowegoZrodlaDanych<int>> dataSource = _obslugaPolWlasnych.PobierzZrodloDanychSlownikaSamochodowDostawczych(out komunikatBledu);
            return dataSource?.Any() == true
                ? new WyborSamochoduDostawczego(dataSource, nazwaSposobuDostawy)
                : null;
        }
    }
}
