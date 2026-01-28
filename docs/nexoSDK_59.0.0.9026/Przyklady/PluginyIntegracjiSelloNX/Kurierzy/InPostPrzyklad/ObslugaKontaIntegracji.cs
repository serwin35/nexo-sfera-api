using InsERT.Moria.HandelElektroniczny;
using InsERT.Moria.HandelElektroniczny.Rozszerzenia;
using InsERT.Moria.HandelElektroniczny.Rozszerzenia.KontaIntegracji;
using InsERT.Moria.HandelElektroniczny.Rozszerzenia.UI;
using InsERT.Mox.Formatting;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace InPostPrzyklad
{
    /// <summary>
    /// Klasa obsługi konta integracji, umożliwia walidację danych konta oraz dodatkową konfigurację wykraczającą poza domyślne możliwości jakie oferuje Sello NX
    /// </summary>
    internal class ObslugaKontaIntegracji : ObslugaBazowaKontaIntegracji, IObslugaKontaIntegracji, IObslugaKontaIntegracjiBasic
    {
        public override TypIntegracji TypIntegracji => TypIntegracji.DostawcyPaczek;

        /// <summary>
        /// Treść wyświetlana w okienku informacyjnym przy dodawaniu konta.
        /// Zalecamy podanie tutaj informacji, skąd pozyskać dane potrzebne do zalogowania się do serwisu kurierskiego
        /// (np. z umowy z kurierem, ze strony konta w serwisie kurierskim itp)
        /// </summary>
        public override string Info
        {
            get => "To jest plugin przykładowy do Sello NX.\r\n" +
                "Podaj tutaj dane logowania do testowego serwisu InPost.\r\n\r\n" +
                "Jako login podaj identyfikator firmy (numer)\r\n" +
                "Jako hasło podaj token autoryzujący";
        }

        /// <summary>
        /// Metoda pozwalająca na zweryfikowanie wprowadzanych danych przy dodawaniu nowego konta integracji z kurierem
        /// Zadbaj o to, aby dodawane konto było zawsze zweryfikowane w systemie kurierskim poprzez pobranie danych logowania wpisanych przez użytkownika
        /// i weryfikację ich, np. przez wysłanie jakiegokolwiek zapytania do serwisu kurierskiego i sprawdzenie statusu odpowiedzi serwera.
        /// </summary>
        /// <param name="sekcjaKonfiguracyjna">Sekcja formatki do wprowadzania danych logowania do serwisu</param>
        /// <returns>DescriptiveBool.true jeśli dane logowania są poprawne (zostały zweryfikowane), DescriptiveBool.Error(treść błędu)</returns>
        public override DescriptiveBoolean WalidujDodawanieKontaIntegracji(ISekcjaWlasna sekcjaKonfiguracyjna)
        {
            // Plugin korzysta z wbudowanej obsługi autoryzacji typu basic, która eksponuje login i hasło
            if (sekcjaKonfiguracyjna is IBazoweUwierzytelnienieKonfiguracjaSekcjaWlasna basicAuth)
            {
                return WalidujKonto(basicAuth.Login.Wartosc, basicAuth.Haslo.Wartosc);
            }
            return DescriptiveBoolean.Error("Wystąpił błąd przy weryfikacji konta. Spróbuj ponownie za chwilę");
        }

        /// <summary>
        /// Metoda pozwalająca na zweryfikowanie wprowadzanych danych przy zapisywaniu konta integracji z kurierem
        /// Zadbaj o to, aby zapisywane konto było zawsze zweryfikowane w systemie kurierskim poprzez pobranie danych logowania wpisanych przez użytkownika
        /// i weryfikację ich, np. przez wysłanie jakiegokolwiek zapytania do serwisu kurierskiego i sprawdzenie statusu odpowiedzi serwera.
        /// </summary>
        /// <param name="kontoIntegracji">Obiekt biznesowy konta integracji</param>
        /// <returns>DescriptiveBool.true jeśli dane logowania są poprawne (zostały zweryfikowane), DescriptiveBool.Error(treść błędu)</returns>
        public override DescriptiveBoolean WalidujZapisanieKontaIntegracji(IKontoIntegracji kontoIntegracji)
        {
            var login = kontoIntegracji.Dane.PodajPole(PolaIntegracjiSelloStale.NazwaKonta);
            var haslo = kontoIntegracji.DeszyfrujPole(PolaIntegracjiSelloStale.KontaSprzedazy.Haslo);

            return WalidujKonto(login, haslo);
        }

        /// <summary>
        ///     Obsługa sekcji własnych 
        /// </summary>
        public override IObslugaSekcjiWlasnychUI SekcjeWlasneKontaIntegracji => null;

        /// <summary>
        /// Metoda pomocnicza służaca walidacji konta w serwisie zewnętrznym
        /// </summary>
        /// <param name="login">Nazwa konta użytkownika</param>
        /// <param name="haslo">Hasło konta integracji</param>
        /// <returns></returns>
        private DescriptiveBoolean WalidujKonto(string login, string haslo)
        {
            // Na początek trzeba zweryfikować, czy cokolwiek jest wpisane w te pola
            if (string.IsNullOrEmpty(login))
                return DescriptiveBoolean.Error("Podanie loginu jest wymagane");

            if (string.IsNullOrEmpty(haslo))
                return DescriptiveBoolean.Error("Podanie hasła jest wymagane");

            // wykonanie testowego żądania do serwisu pod jakiejkolwiek zasób wymagający (!) podania loginu i hasła
            // np: https://dokumentacja-inpost.atlassian.net/wiki/spaces/PL/pages/11731073/Wyszukiwanie+i+sortowanie+przesy+ek
            using (HttpClient validatorClient = new HttpClient())
            {
                validatorClient.BaseAddress = new Uri(InPostPrzyklad.UriString);
                validatorClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                validatorClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("pl-PL"));
                validatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", haslo);

                var response = validatorClient.GetAsync($"/v1/organizations/{login}/shipments").Result;

                // Sprawdzenie czy odpowiedź serrwera jest niepoprawna i zwrócenie odpowiedniej informacji
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        return DescriptiveBoolean.Error("Podane hasło jest niepoprawne");
                    }
                    else if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        return DescriptiveBoolean.Error($"Brak uprawnień do dostępu do podanego konta: {login}");
                    }
                    else
                    {
                        return DescriptiveBoolean.Error("Wystąpił błąd dostępu do serwisu");
                    }
                }
                return DescriptiveBoolean.True;
            }
        }
    }
}
