using InsERT.Moria.Finanse;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ZarzadcaPlatnosciCesyjnej
{
    public class ZarzadcaPlatnosciCesyjnej : IZarzadcaPlatnosciCesyjnej
    {
        private readonly IScramblerDanychDostawcySzybkiejPlatnosci _scramblerDanychDostawcySzybkiejPlatnosci;
        private readonly string IdSprzedawcy = "Id sprzedawcy";
        private readonly string KluczApi = "Klucz API";

        public ZarzadcaPlatnosciCesyjnej(
            IScramblerDanychDostawcySzybkiejPlatnosci scramblerDanychDostawcySzybkiejPlatnosci)
        {
            _scramblerDanychDostawcySzybkiejPlatnosci = scramblerDanychDostawcySzybkiejPlatnosci;
        }

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("254d681d-cc13-49fa-b794-e371fa06dd40");

        public string Nazwa => "Pośrednik Przykładowy";

        public string Opis => "Zarządca dla Pośrednika Przykładowego";

        public bool WymagaIdentyfikatoraStatusuPlatnosci => false;

        public List<string> DaneAutoryzujace => new List<string>() { IdSprzedawcy, KluczApi };

        public string NIPPodmiotuCesyjnego => "1234567890";

        public void WypelnijPodmiotDanymi(Podmiot podmiot)
        {
            podmiot.NIP = NIPPodmiotuCesyjnego;
            podmiot.NazwaSkrocona = "Pośrednik Przykładowy";
            podmiot.Firma.Nazwa = "Pośrednik Przykładowy";
        }

        public bool TestujPolaczenie(FormaPlatnosci formaPlatnosci)
        {
            var idSprzedawcy = PodajOdszyfrowanyIdSprzedawcy(formaPlatnosci);
            var kluczAPI = PodajOdszyfrowanyKluczAPI(formaPlatnosci);

            return idSprzedawcy == "Poprawne" && kluczAPI == "dane";
        }

        public IDanePlatnosciCesyjnej PobierzDaneTransakcji(FormaPlatnosci formaPlatnosci, string identyfikatorPlatnosci)
        {
            ElektronicznyStatusPlatnosciCesyjnej status;
            switch (identyfikatorPlatnosci)
            {
                case "Niezaplacone":
                    status = ElektronicznyStatusPlatnosciCesyjnej.Niezaplacone;
                    break;
                case "Przedplata":
                    status = ElektronicznyStatusPlatnosciCesyjnej.Przedplata;
                    break;
                case "Zaplacone":
                    status = ElektronicznyStatusPlatnosciCesyjnej.Zaplacone;
                    break;
                case "Zwrocone":
                    status = ElektronicznyStatusPlatnosciCesyjnej.Zwrocone;
                    break;
                default: throw new InvalidOperationException("Nieprawidłowy status");

            } 
            return new DanePlatnosciCesyjnej
            {             
                Status = status,
                Kwota = 10M
            };
        }

        #region Pomocnicze

        private string PodajOdszyfrowanyIdSprzedawcy(FormaPlatnosci formaPlatnosci)
        {
            if (formaPlatnosci == null)
                throw new ArgumentNullException(nameof(formaPlatnosci));

            var idSprzedawcy = formaPlatnosci.DaneAutoryzacyjnePlatnosciCesyjnej.FirstOrDefault(f => f.Nazwa == IdSprzedawcy);

            if (idSprzedawcy == null)
                throw new InvalidOperationException("Brak id sprzedawcy.");

            return _scramblerDanychDostawcySzybkiejPlatnosci.Odszyfruj(formaPlatnosci.ZarzadcaPlatnosciCesyjnejId.Value, idSprzedawcy.Wartosc);
        }

        private string PodajOdszyfrowanyKluczAPI(FormaPlatnosci formaPlatnosci)
        {
            if (formaPlatnosci == null)
                throw new ArgumentNullException(nameof(formaPlatnosci));

            var kluczAPI = formaPlatnosci.DaneAutoryzacyjnePlatnosciCesyjnej.FirstOrDefault(f => f.Nazwa == KluczApi);

            if (kluczAPI == null)
                throw new InvalidOperationException("Brak klucza API.");

            return _scramblerDanychDostawcySzybkiejPlatnosci.Odszyfruj(formaPlatnosci.ZarzadcaPlatnosciCesyjnejId.Value, kluczAPI.Wartosc);
        }

        #endregion
    }

    public class DanePlatnosciCesyjnej : IDanePlatnosciCesyjnej
    {
        public ElektronicznyStatusPlatnosciCesyjnej Status { get; set; }
        public decimal Kwota { get; set; }
        public string IdTransakcji { get; set; }
        public DateTime? DataTransakcji { get; set; }
    }
}