using System;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using System.Linq;
using InsERT.Moria.Klienci;

namespace KsefPluginy
{
    /// <summary>
    /// Funkcja generuje sekcję Transport w pliku faktury ustrukturyzowanej, która nie jest wypełniana mechanizmami Subiekta nexo.
    /// Zakłada ona istnienie następujących pól własnych zaawansowanych w dokumencie sprzedaży:
    /// 1. Rodzaj transportu, typu słownik własny. Z wartościami w postaci "1 - Transport morski", "2 - Transport kolejowy", "3 - Transport drogowy" etc. Wartości liczbowe elementów słownika są ustawione wg schematu XSD faktury ustrukturyzowanej.
    /// 2. Numer zlecenia transportu, typu tekst.
    /// 3. Opis ładunku, typu słownik własny. Z wartościami w postaci "01 - Bańka", "02 - Beczka", "03 - Butla" etc. Wartości liczbowe elementów słownika są ustawione wg schematu XSD faktury ustrukturyzowanej.
    /// 4. Jednostka opakowania, typu tekst.
    /// 5. Data rozpoczęcia transportu, typu data.
    /// 6. Data zakończenia transportu typu data.
    /// 7. Przewoźnik, typu słownik własny SQL opierający się na tabeli ModelDanychContainer.Podmioty, w którym identyfikator elementu słownika będzie identyfikatorem podmiotu zaś wartością będzie nazwa skrócona podmiotu.
    /// Podsekcje WysylkaZ oraz WysylkaDo są wypełniane odpowiednio adresem podstawowym mojej firmy (historiowanym na dokumencie) oraz adresem dostawy wybranym na dokumencie.
    /// Wykorzystana jest również ścieżka do pola z kontekstu pola celem sprawdzenia czy wypełniane są pola z odpowiedniej sekcji.
    /// </summary>
    public class GenerowanieSekcjiTransport : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private readonly Guid _id = new Guid("A5D0DF1D-E4DA-4EA9-8F8D-269B2BC644C2");
        private readonly IPolaWlasneAdv2AccessorFactory _polaWlasneAccessorFactory;
        private readonly IPodmioty _podmioty;

        public GenerowanieSekcjiTransport(IPolaWlasneAdv2AccessorFactory polaWlasneAccessorFactory
            , IPodmioty podmioty)
        {
            _polaWlasneAccessorFactory = polaWlasneAccessorFactory ?? throw new ArgumentNullException(nameof(polaWlasneAccessorFactory));
            _podmioty = podmioty ?? throw new ArgumentNullException(nameof(podmioty));
        }

        public Guid Identyfikator => _id;

        public string Nazwa => "Generowanie sekcji transport wg pól własnych";

        public string Opis => "Funkcja generuje sekcję transport na podstawie pól własnych dokumentu sprzedaży";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            string wartosc = null;
            ignorujPole = false;

            IPolaWlasneAdv2Accessor accessor = _polaWlasneAccessorFactory.Utworz(kontekst.Dokument, false);
            // wypełnianie podsekcji Przewoznik:
            if (kontekst.SciezkaPola.EndsWith("\\Transport\\Przewoznik"))
            {
                switch (pole)
                {
                    case PolaEFaktury.KodKraju:
                        wartosc = "PL"; // dla uproszczenia zakładamy, że przewoźnik jest z Polski
                        break;
                    case PolaEFaktury.BrakID:
                        wartosc = string.IsNullOrEmpty(PobierzPolePrzewoznika(accessor, PolaEFaktury.NIP)) ? "1" : null;
                        break;
                    case PolaEFaktury.NIP:
                        {
                            string nip = PobierzPolePrzewoznika(accessor, PolaEFaktury.NIP);
                            wartosc = string.IsNullOrEmpty(nip) ? null : nip;
                        }
                        break;
                    case PolaEFaktury.PelnaNazwa:
                    case PolaEFaktury.Nazwa:
                    case PolaEFaktury.Miejscowosc:
                    case PolaEFaktury.Ulica:
                    case PolaEFaktury.NrDomu:
                    case PolaEFaktury.KodPocztowy:
                    case PolaEFaktury.AdresL1:
                        wartosc = PobierzPolePrzewoznika(accessor, pole);
                        break;


                }
            }
            else if (kontekst.SciezkaPola.EndsWith("\\Transport\\WysylkaZ")) // podsekcje WysylkaZ oraz WysylkaDo są wypełniane na podstawie wybranych adresów
                wartosc = PobierzPoleAdresu(pole, kontekst.Dokument.AdresMojejFirmy);
            else if (kontekst.SciezkaPola.EndsWith("\\Transport\\WysylkaDo"))
                wartosc = PobierzPoleAdresu(pole, kontekst.Dokument.MiejsceDostawyZewnetrzne);
            else if (kontekst.SciezkaPola.EndsWith("\\Transport")) // pozostałe pola w sekcji Transport są wypełniane na podstawie pozostałych pól własnych
            {
                switch (pole)
                {
                    // dla pól tekstowych pobieramy wartość tekstową
                    case PolaEFaktury.NrZleceniaTransportu:
                        wartosc = accessor.PobierzWartoscTypuTekst("Numer zlecenia transportu");
                        break;
                    case PolaEFaktury.JednostkaOpakowania:
                        wartosc = accessor.PobierzWartoscTypuTekst("Jednostka opakowania");
                        break;

                    // dla pól słownikowych schema XSD wymaga podania "numeru" elementu słownika co jest realizowane poprzez wyodrębnienie numeru z wartości tekstowej słownika
                    case PolaEFaktury.RodzajTransportu:
                        {
                            var rodzajTransportu = accessor.PobierzWartoscTypuSlownikWlasny("Rodzaj transportu");
                            string[] split = rodzajTransportu.Wartosc.Split('-');
                            if (split.Length == 2 && int.TryParse(split[0].Trim(), out int numer))
                                wartosc = numer.ToString();
                        }
                        break;
                    case PolaEFaktury.OpisLadunku:
                        {
                            var opisLadunku = accessor.PobierzWartoscTypuSlownikWlasny("Opis ładunku");
                            string[] split = opisLadunku.Wartosc.Split('-');
                            if (split.Length == 2 && int.TryParse(split[0].Trim(), out int numer))
                                wartosc = numer.ToString();
                        }
                        break;

                    // daty muszą być formatowane z częścią godzinową co jest wymogiem schematu XSD faktury ustrukturyzowanej
                    case PolaEFaktury.DataGodzRozpTransportu:
                        wartosc = accessor.PobierzWartoscTypuData("Data rozpoczęcia transportu")?.ToString("yyyy-MM-ddTHH:mm:ss") ?? string.Empty;
                        break;
                    case PolaEFaktury.DataGodzZakTransportu:
                        wartosc = accessor.PobierzWartoscTypuData("Data zakończenia transportu")?.ToString("yyyy-MM-ddTHH:mm:ss") ?? string.Empty;
                        break;
                }
            }

            return wartosc;
        }

        /// <summary>
        /// Pomocnicza metoda pobierająca poszczególne pola z adresu.
        /// </summary>
        private string PobierzPoleAdresu(string pole, AdresHistoria adres)
        {
            switch (pole)
            {
                case PolaEFaktury.KodKraju:
                    return adres.Panstwo?.KodPanstwaUE ?? "PL";
                case PolaEFaktury.Miejscowosc:
                    return string.IsNullOrEmpty(adres.AdresSzczegolyHistoria.Miejscowosc) ? null : adres.AdresSzczegolyHistoria.Miejscowosc;
                case PolaEFaktury.Ulica:
                    return string.IsNullOrEmpty(adres.AdresSzczegolyHistoria.Ulica) ? null : adres.AdresSzczegolyHistoria.Ulica;
                case PolaEFaktury.NrDomu:
                    return string.IsNullOrEmpty(adres.AdresSzczegolyHistoria.NrDomu) ? null : adres.AdresSzczegolyHistoria.NrDomu;
                case PolaEFaktury.KodPocztowy:
                    return string.IsNullOrEmpty(adres.AdresSzczegolyHistoria.KodPocztowy) ? null : adres.AdresSzczegolyHistoria.KodPocztowy;
                case PolaEFaktury.AdresL1:
                    return string.IsNullOrEmpty(adres.LiniaCalosc) ? null : adres.LiniaCalosc;
            }
            return null;
        }

        /// <summary>
        /// Pomocnicza metoda pobierająca pole ze słownika przewoźników.
        /// </summary>
        private string PobierzPolePrzewoznika(IPolaWlasneAdv2Accessor accessor, string nazwaPola)
        {
            var przewoznik = accessor.PobierzWartoscTypuSlownikWlasnySqlByInt2("Przewoźnik");
            if (przewoznik.HasValue)
            {
                switch (nazwaPola)
                {
                    case PolaEFaktury.PelnaNazwa:
                    case PolaEFaktury.Nazwa:
                        return _podmioty.Dane.Wszystkie().Where(pdm => pdm.Id == przewoznik.Value).Select(p => p.NazwaSkrocona).FirstOrDefault() ?? string.Empty;
                    case PolaEFaktury.NIP:
                        return _podmioty.Dane.Wszystkie().Where(pdm => pdm.Id == przewoznik.Value).Select(p => p.NIP).FirstOrDefault() ?? string.Empty;
                    case PolaEFaktury.Miejscowosc:
                        return _podmioty.Dane.Wszystkie().Where(pdm => pdm.Id == przewoznik.Value).Select(p => p.AdresPodstawowy.Szczegoly.Miejscowosc).FirstOrDefault() ?? string.Empty;
                    case PolaEFaktury.Ulica:
                        return _podmioty.Dane.Wszystkie().Where(pdm => pdm.Id == przewoznik.Value).Select(p => p.AdresPodstawowy.Szczegoly.Ulica).FirstOrDefault() ?? string.Empty;
                    case PolaEFaktury.NrDomu:
                        return _podmioty.Dane.Wszystkie().Where(pdm => pdm.Id == przewoznik.Value).Select(p => p.AdresPodstawowy.Szczegoly.NrDomu).FirstOrDefault() ?? string.Empty;
                    case PolaEFaktury.KodPocztowy:
                        return _podmioty.Dane.Wszystkie().Where(pdm => pdm.Id == przewoznik.Value).Select(p => p.AdresPodstawowy.Szczegoly.KodPocztowy).FirstOrDefault() ?? string.Empty;
                    case PolaEFaktury.AdresL1:
                        return _podmioty.Dane.Wszystkie().Where(pdm => pdm.Id == przewoznik.Value).Select(p => p.AdresPodstawowy.LiniaCalosc).FirstOrDefault() ?? string.Empty;
                }
            }
            return null;
        }
    }
}
