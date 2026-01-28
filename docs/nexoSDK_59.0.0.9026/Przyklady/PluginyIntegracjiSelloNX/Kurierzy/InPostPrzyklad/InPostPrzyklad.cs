using InPostPrzyklad.DTO;
using InsERT.Moria.Finanse;
using InsERT.Moria.HandelElektroniczny;
using InsERT.Moria.HandelElektroniczny.Rozszerzenia;
using InsERT.Moria.HandelElektroniczny.Rozszerzenia.HTTP;
using InsERT.Moria.HandelElektroniczny.Rozszerzenia.HTTP.Extensions;
using InsERT.Moria.HandelElektroniczny.Rozszerzenia.Kurierzy;
using InsERT.Moria.HandelElektroniczny.Rozszerzenia.UI;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Sfera;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace InPostPrzyklad
{
    public class InPostPrzyklad : IIntegracjaKuriera
    {
        private readonly Lazy<ObslugaKontaIntegracji> _obslugaKontaIntegracji;

        /// <summary>
        ///     Zalecamy korzystanie z klasy IKlientHttpSello do łączenia się z serwisami zewnętrznymi.
        ///     Zapewnia ona pełną obsługę zapytań przez Sello NX, uwzględniając obsługę błędów, logowanie komunikacji czy odświeżanie tokenów.
        /// </summary>
        private readonly IKlientHttpSello _klientHttpSello;

        /// <summary>
        ///     Adres uri API kuriera wskazuje na testowy serwis API ShipX, do którego można zalogować się pod adresem https://sandbox-manager.paczkomaty.pl/auth/login
        ///     Stamtąd też należy pozyskać dane pozwalające dodać konto tej przykładowej integracji.
        /// </summary>
        public const string UriString = "https://sandbox-api-shipx-pl.easypack24.net";

        public InPostPrzyklad(IKlientHttpSello klientHttpSello)
        {
            _klientHttpSello = klientHttpSello;
            _obslugaKontaIntegracji = new Lazy<ObslugaKontaIntegracji>(() => new ObslugaKontaIntegracji());

            //Inicjalizacja HttpClient.
            klientHttpSello.InicjalizujHTTPClientBasic(Nazwa, UriString, accept: new MediaTypeWithQualityHeaderValue("application/json"), acceptLanguage: new StringWithQualityHeaderValue("pl-PL"));
        }

        // Interfejs bazowy plugina nexo, zawiera podstawowe informacje dotyczące plugina systemu nexo. Należy je uzupełnić odpowiednio do potrzeb.
        // Dane te będą prezentowane użytkownikowi w systemie nexo
        #region IFunkcja

        /// <summary>
        ///     Unikalny identyfikator pluginu nexo. Każdy nowy plugin musi zawierać tutaj nowy wygenerowany GUID.
        ///     Jeśli planujesz opublikować swój plugin na podstawie tego przykładu, koniecznie wygeneruj tutaj nową wartość, np. za pomocą generatorów online.
        /// </summary>
        public Guid Identyfikator => new Guid("94C8C7DB-DE59-45F7-B2E5-58178E311122");

        /// <summary>
        ///     Nazwa plugina, będąca zarazem nazwą integracji.
        ///     Podaj tutaj nazwę firmy kurierskiej, z którą plugin współpracuje. Nazwa ta wyświetlana jest użytkownikowi w postaci kolorowego stempelka 
        ///     identyfikującego dostawcę paczek. Zadbaj o to, aby nazwa była krótka a wielkość liter odpowiadała realnej nazwie jaką stosuje firma kurierska.
        ///     Dobre nazwy to: DPD, InPost, Poczta Polska, Pocztex, DHL, itp.
        /// </summary>
        public string Nazwa => "InPost przykład";

        /// <summary>
        ///     Krótki opis plugina. informacje te mogą być widoczne dla użytkownika programu nexo.
        /// </summary>

        public string Opis => "Przykładowa integracja kurierska z testowym serwisem InPost API ShipX";
        /// <summary>
        ///     Informacje o dostawcy plugina (autorze). Informacje te mogą być widoczne dla użytkownika programu nexo.
        ///     Uzupełnij je w pliku <code>DostawcaPluginow.cs</code>.
        /// </summary>
        public IDostawcaPluginow Dostawca => new DostawcaPluginow();
        #endregion IFunkcja

        // Interfejs bazowy plugina integracji Sello NX, zawiera podstawowe informacje na temat kuriera (m.in. kolory identyfikujące)
        #region IIntegracja
        /// <summary>
        ///     Trzyliterowy symbol integracji, widoczny w module konta integracji, wykorzystywany w module numeracja w nexo jako składnik numeru.
        ///     Zadbaj o to, aby symbol ten kojarzył się z firmą kurierską.
        ///     Dobre nazwy to: DPD, INP (InPost), PPL (Poczta Polska), PCX (Pocztex), DHL itp.
        /// </summary>
        public string Symbol => "INP";

        /// <summary>
        ///     Numer wersji plugina integracji. Wersja ta może być widoczne dla użytkowników systemu nexo.
        ///     W przyszłości pozwoli nam zrealizować system aktualizacji pluginów u użytkowników.
        ///     Uwaga! Wersja w przyszłości zostanie zamieniona na klasę Version w formacie (1.0.0)
        /// </summary>
        public int Wersja => 1;

        /// <summary>
        ///     Główny kolor identyfikujący integrację. Jest to kolor stempelka (tła) prezentowanego użytkownikowi w programie, na którym widoczna jest <see cref="Nazwa"/> integracji.
        ///     Jeśli kolor ten będzie ustawiony na biały (#ffffff), stempelek w Sello NX będzie zawierał ramkę w kolorze tekstu (<see cref="KolorDodatkowy"/>).
        ///     Zadbaj o to, aby było to pierwszy kolor, który kojarzy się z daną firmą kurierską. Jako wyznacznik weź pod uwagę kolor wyróżnień ze strony internetowej kuriera czy kolor logotypu.
        ///     Identyfikacja kolorystyczna jest w Sello NX stosowana zamiast logotypów, dlatego ważne jest, aby zachować spójność ze światem zewnętrznym.
        ///     Dobre kolory to: #dc0032 (czerwony, DPD), #ffcb04 (złoty, InPost), #e71905 (czerwony, Poczta Polska, Pocztex), #ffcc00 (złoty, DHL) 
        /// </summary>
        public string KolorGlowny => "#FF009900";

        /// <summary>
        ///     Dodatkowy kolor identyfikujący integrację. Jest to kolor tekstu (<see cref="Nazwa"/>) stempelka prezentowanego użytkownikowi w programie.
        ///     Zadbaj o to, aby kolor dodatkowy dopełniał kolor główny (<see cref="KolorGlowny"/>) tak, aby cały stempelek kojarzył się z daną firmą kurierską.
        ///     Jako wyznacznik weź pod uwagę kolor dodatkowy ze strony internetowej kuriera czy kolor czcionki logotypu.
        ///     Identyfikacja kolorystyczna jest w Sello NX stosowana zamiast logotypów, dlatego ważne jest, aby zachować spójność ze światem zewnętrznym.
        ///     Dobre kolory to: #ffffff (biały, DPD, ponieważ szary na czerwonym jest nieczytelny), 
        ///     #3c3c3c (ciemnoszary, InPost), #ffffff (biały, Poczta Polska, Pocztex), #d40511 (czerwony, DHL)
        /// </summary>
        public string KolorDodatkowy => "#FFFFFFFF";

        /// <summary>
        ///     Ustawienie klasy umożliwiającej zarządzaniem kontem integracji
        /// </summary>
        public IObslugaKontaIntegracji ObslugaKontaIntegracji => _obslugaKontaIntegracji.Value;

        /// <summary>
        ///     Czy integracja komunikuje się ze środowiskiem testowym kuriera?
        ///     Jeśli tak, zwróć tutaj wartość <c>true</c>, dzięki czemu Sello NX wyświetli taką informację w programie, a użytkownik będzie wiedział, że 
        ///     korzystanie z tego plugina integracji nie spowoduje naliczenia opłat w serwisie kurierskim z tytułu utworzenia paczki, bądź też wezwania kuriera po jej odbiór.
        ///     Gdy plugin integracji przeznaczony jest do pracy produkcyjnej, zwróć <c>false</c>.
        /// </summary>
        public bool IntegracjaTestowa => true;
        #endregion IIntegracja

        //Interfejs integracji z kurierem, zawiera m.in. implementację metod wykorzystywanych przez Sello NX do komunikacji z serwisem kurierskim.
        #region IIntegracjaKuriera
        /// <summary>
        ///     Guid typu obiektu wykorzystywanego do zarządzania oknem konfiguracji sposobów dostawy dla plugina.
        ///     Poniżej wykorzystany własny typ.
        /// </summary>
        public DeklaracjaTypuObiektu KonfiguracjaSposobuDostawyPaczkiUI => new DeklaracjaTypuObiektu(IdyTypowWbudowanych.KonfiguracjaBazowaSposobuDostawyPaczkiKurierowUI);

        /// <summary>
        ///     Mapowanie sposobów dostawy z serwisu na sposoby dostawy paczki w Sello NX z uwzględnieniem konta sprzedażowego.
        /// </summary>
        ///<remarks>
        ///     <para><see langword="true"/> jeżeli sposoby dostawy w serwisie zależą od konta sprzedaży (np. Allegro i Wysyłam z Allegro). W tym przypadku należy dodać pole <see cref="PolaIntegracjiSelloStale.KontaSprzedazy.IdUzytkownikaWSerwisie"/> w koncie sprzedaży internetowej oraz w koncie kuriera.<br/>
        ///     <see langword="false"/> nie uwzględnia konta, domyślne zachowanie.</para>
        ///</remarks>
        public bool MapowanieSposobuDostawyZUwzglednieniemKonta => false;

        /// <summary>
        ///     Lista obsługiwanych kurierów przez integrację.
        ///     W przypadku brokerów jak Wysyłam z Allegro zwraca listę wszystkich obsługiwanych kurierów (np. DPD, Allegro, DHL...)
        /// </summary>
        public List<string> Kurierzy => new List<string> { "InPost" };

        public List<DaneKuriera> DaneKurierow => new List<DaneKuriera>();

        /// <summary>
        ///     Identyfikator powiązanej integracji sprzedaży internetowej.
        /// </summary>
        public Guid? IdentyfikatorPowiazanejIntegracjiSprzedazyInternetowej => null;

        /// <summary>
        ///     Metoda, której zadaniem jest zwrócenie listy usług dostawy oferowanych przez kuriera.
        ///     Zadanie Sello NX wywołujące tę metodę jest przystosowane do obsługi komunikacji z serwisem zewnętrznym (pasek postępu, obsługa błędów itp)
        ///     ale nie jest to wymagane i plugin może te dane zwrócić jako statyczną listę usług. 
        ///     Dane zwracane z tej metody są przechowywane przez Sello NX w dedykowanej pamięci podręcznej a metoda ta jest wywoływana tylko jeśli następuje konieczność
        ///     odświeżenia listy usług (np. pierwsze wejście do okna z listą usług, ręczne wywołanie odświeżenia przez użytkownika).
        ///     Użytkownik programu będzie mógł wybrać jedną z tych usług wraz z opcjami dodatkowymi w konfiguracji sposobu wysyłki paczki w Sello NX.
        /// </summary>
        /// <param name="dane">Dane przekazane przez zadanie pobierania usług oferowanych przez kuriera z Sello NX, 
        /// na podstawie których plugin integracji może pobrać te usługi z serwisu kurierskiego.</param>
        /// <returns>Lista usług kurierskich w ustalonym formacie wynikowym</returns>
        public PobranieUslugWynik PobierzUslugi(PobranieUslugDane dane)
        {
            // Lista usług oferowanych przez kuriera jest w miarę stałym zbiorem więc w tym przypadku plugin nie
            // komunikuje się z serwisem aby tę listę pobrać, tylko zwraca statyczną listę wybranych usług, które będą przez plugin obsługiwane.
            // Lista usług pobranych od kuriera często zawiera dużo szerszy zakres niż ten, który faktycznie będzie używany w programie.

            List<UslugaKurierska> uslugi = new List<UslugaKurierska>();

            // Lista usług uzupełniana statycznymi danymi wg dokumentacji kuriera.
            // https://dokumentacja-inpost.atlassian.net/wiki/spaces/PL/pages/11731062/Rozmiary+i+us+ugi+dla+przesy+ek
            // Użytkownik programu będzie mógł wybrać jedną z tych usług w konfiguracji sposobu wysyłki paczki w Sello NX.
            uslugi.Add(new UslugaKurierska()
            {
                Id = "inpost_courier_standard",
                Nazwa = "Przesyłka kurierska standardowa",
                IdKuriera = "INPOST",
                // Lista usług dodatkowych oferowanych dla danej usługi.
                // Użytkownik programu będzie mógł zaznaczyć dowolne usługi dodatkowe z poniższej listy w postaci checkboxów.
                // Są to takie usługi jak: dodatkowy SMS, e-mail, dostawa przed południem. itp.
                // WAŻNE! Nie umieszczaj tutaj usług COD (pobranie) oraz INSURANCE (ubezpieczenie), ponieważ są one traktowane 
                // indywidualnie przez Sello NX a w oknie konfiguracji sposobu dostawy są wyniesione do osobnych pól.
                UslugiDodatkowe = new List<UslugaKurierskaDodatkowa>()
                {
                    new UslugaKurierskaDodatkowa()
                    {
                        Id = "sms",
                        Nazwa = "SMS",
                        Opis = "Powiadomienie o przesyłce via SMS."
                    },
                    new UslugaKurierskaDodatkowa()
                    {
                        Id = "email",
                        Nazwa = "EMAIL",
                        Opis = "Powiadomienie o przesyłce via e-mail."
                    },
                    new UslugaKurierskaDodatkowa()
                    {
                        Id = "saturday",
                        Nazwa = "Doręczenie w sobotę",
                        Opis = "Doręczenie przesyłki w sobotę."
                    }
                },

                // Lista dostępnych metod nadania paczki dla wybranej usługi
                // https://dokumentacja-inpost.atlassian.net/wiki/spaces/PL/pages/11731047/Spos+b+nadania
                // Użytkownik programu będzie mógł wybrać jedną z tych metod
                MetodyNadania = new List<MetodaNadania>()
                {
                    new MetodaNadania()
                    {
                        IdKuriera = "dispatch_order",
                        Nazwa = "Odbiór przez Kuriera",
                        Opis = "Utworzę zelecenie odbioru - przesyłkę odbierze Kurier InPost"
                    },
                    new MetodaNadania()
                    {
                        IdKuriera = "pop",
                        Nazwa = "Nadanie w POP",
                        Opis = "Nadam przesyłkę w Punkcie Obsługi Przesyłek"
                    }
                },
            });

            // Usługi należy zapisać w strukturze wynikowej tego zadania. 
            return new PobranieUslugWynik()
            {
                Uslugi = uslugi
            };
        }

        /// <summary>
        ///     Metoda, której zadaniem jest zwrócenie listy propozycji podjazdów kuriera z serwisu
        /// </summary>
        /// <param name="dane"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public PobraniePropozycjiPodjazduKurieraWynik PobierzPropozycjiPodjazduKuriera(PobraniePropozycjiPodjazduKurieraDane dane)
        {
            var propozycjaPodjazduKuriera = new PobraniePropozycjiPodjazduKurieraWynik()
            {
                PropozycjePodjazdow = new List<PropozycjaPodjazdu>()
            };

            foreach (var item in dane.IdentyfikatoryPaczekWSystemieKuriera)
            {
                var propozycjaPodjazdu = new PropozycjaPodjazdu()
                {
                    IdentyfikatorPaczkiWSystemieKuriera = item,
                    DanePodjazdu = new List<DanePodjazdu>()
                    {
                        new DanePodjazdu()
                        {
                            Id = "InPost",
                            Nazwa = "Zamówienie kuriera",
                            Opis = "Zamówienie kuriera"
                        }
                    }
                };

                propozycjaPodjazduKuriera.PropozycjePodjazdow.Add(propozycjaPodjazdu);
            }

            return propozycjaPodjazduKuriera;
        }

        /// <summary>
        ///     Metoda, której zadaniem jest zamówienie kuriera dla listy przesyłek na dany dzień i daną godzinę (jeśli serwis to obsługuje)
        /// </summary>
        /// <param name="dane"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ZamowieniePodjazduKurieraWynik ZamowPodjazdKuriera(ZamowieniePodjazduKurieraDane dane)
        {
            var paczka = dane.IdentyfikatoryPaczekWSystemieKuriera.FirstOrDefault();
            var zamowienieId = dane.Uchwyt.ZamowieniaWysylkowe().Znajdz(z => z.Paczki.Select(p => p.IdentyfikatorPaczkiWSystemieKuriera).Contains(paczka)).Dane.Id;

            if (paczka == null)
                throw new BladKomunikacjiZSerwisemException("Nie znaleziono paczek do zamówienia podjazdu kuriera.");

            var sposobDostawyPaczki = dane.Uchwyt.SposobyDostawyPaczki().Dane.Pierwszy(s => s.ZamowieniaWysylkowe.Select(z => z.Id).Contains(zamowienieId));

            var podmiot = dane.Uchwyt.Podmioty().ZnajdzMojaFirme()?.Dane;

            if (podmiot == null)
                throw new BladKomunikacjiZSerwisemException("Nie znaleziono mojej firmy w podmiotach Sello. Spróbuj ponownie.");

            var idNumeruTelefonu = sposobDostawyPaczki.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.IdNumeruTelefonu);
            var telefon = podmiot.Kontakty.Where(k => k.Id == idNumeruTelefonu).Select(x => x.Wartosc).FirstOrDefault() ?? sposobDostawyPaczki.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.NumerTelefonu);

            var idAdresuEmail = sposobDostawyPaczki.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.IdAdresuEmail);
            var email = podmiot.Kontakty.Where(k => k.Id == idAdresuEmail).Select(x => x.Wartosc).FirstOrDefault() ?? sposobDostawyPaczki.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.AdresEmail);

            var idAdresu = sposobDostawyPaczki.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.IdAdresuNadawcy);
            var adresNadawcy = podmiot.Adresy.Where(x => x.Id == idAdresu).FirstOrDefault();

            if (adresNadawcy == null)
                throw new BladKomunikacjiZSerwisemException($"Nie znaleziono adresu nadawcy w sposobie dostawy paczki: '{sposobDostawyPaczki.Nazwa}'.");

            var buildingNumber = adresNadawcy.Szczegoly.NrDomu;
            if (!string.IsNullOrEmpty(adresNadawcy.Szczegoly.NrLokalu))
                buildingNumber += "/" + adresNadawcy.Szczegoly.NrLokalu;

            string senderName = string.Empty;
            if (string.IsNullOrEmpty(sposobDostawyPaczki.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.NazwaFirmy)))
            {
                if (!string.IsNullOrEmpty(sposobDostawyPaczki.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.Imie)))
                    senderName = sposobDostawyPaczki.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.Imie);

                if (!string.IsNullOrEmpty(sposobDostawyPaczki.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.Nazwisko)))
                {
                    if (!string.IsNullOrEmpty(senderName))
                        senderName += " ";
                    senderName += sposobDostawyPaczki.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.Nazwisko);
                }
            }
            else
                senderName = sposobDostawyPaczki.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.NazwaFirmy);

            var inpostDispatchOrder = new InpostDispatchOrderRequest()
            {
                Shipments = dane.IdentyfikatoryPaczekWSystemieKuriera,
                Comment = "Automatyczne zamówienie podjazdu",
                Address = new InpostAddressDto
                {
                    Street = adresNadawcy.Szczegoly.Ulica,
                    City = adresNadawcy.Szczegoly.Miejscowosc,
                    CountryCode = adresNadawcy.Panstwo.KodPanstwaUE,
                    ZipCode = adresNadawcy.Szczegoly.KodPocztowy,
                    BuildingNumber = buildingNumber,
                },
                Phone = telefon,
                Email = email,
                Name = senderName
            };

            var response = _klientHttpSello.Post<InpostCreateDispatchOrderResponseDto>(Nazwa, $"/v1/organizations/{dane.DaneUwierzytelnienia.TokenDostepuLubLogin}/dispatch_orders", new StringContent(JsonConvert.SerializeObject(inpostDispatchOrder), System.Text.Encoding.UTF8, "application/json"), dane.KontekstKonta, authenticationHeader: new AuthenticationHeaderValue("Bearer", dane.DaneUwierzytelnienia.TokenOdswiezeniaLubHaslo))
                                            .ReturnOrThrowException(HandleError<InpostCreateDispatchOrderResponseDto>());

            return new ZamowieniePodjazduKurieraWynik()
            {
                IdentyfikatorZamowieniaPodjazduKurieraWSystemieKuriera = response.Id.ToString(),
                WewnetrznyIdentyfikatorZamowieniaKuriera = dane.WewnetrznyIdentyfikatorZamowieniaKuriera,
                DataPropozycji = dane.DataPropozycji,
                StatusZamowieniaPodjazduKuriera = response.Status == InpostDispatchOrderStatusDto.Sent ? StatusZamowieniaPodjazduKuriera.Stworzone : StatusZamowieniaPodjazduKuriera.NadaneDoStworzenia,
                NazwaPropozycji = dane.NazwaPropozycji
            };
        }

        /// <summary>
        ///     Metoda, której zadaniem jest zwrócienie statusu podjazdu kuriera z serwisu
        ///     Status należy zamapować na statusy SelloNX (StatusZamowieniaPodjazduKuriera)
        /// </summary>
        /// <param name="dane"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public PobranieStatusZamowieniaPodjazduWynik PobierzStatusZamowieniaPodjazdu(PobranieStatusZamowieniaPodjazduDane dane)
        {
            var listaWysylkowa = dane.Uchwyt.ListyWysylkowe().Znajdz(l => l.IdentyfikatorWewnetrznyZamowienia == dane.IdentyfikatorZamowieniePodjazdu)?.Dane;

            if (listaWysylkowa == null)
                throw new BladKomunikacjiZSerwisemException($"Nie znaleziono listy wysyłkowej o identyfikatorze wewnętrznym '{dane.IdentyfikatorZamowieniePodjazdu}'.");

            var response = _klientHttpSello.Get<InpostDispatchOrderResponseDto>(Nazwa, $"/v1/dispatch_orders/{listaWysylkowa.IdentyfikatorZamowieniaWSystemieKuriera}", dane.KontekstKonta, authenticationHeader: new AuthenticationHeaderValue("Bearer", dane.DaneUwierzytelnienia.TokenOdswiezeniaLubHaslo))
                                           .ReturnOrThrowException(HandleError<InpostDispatchOrderResponseDto>());

            return new PobranieStatusZamowieniaPodjazduWynik()
            {
                StatusZamowieniaPodjazduKuriera = response.Status == InpostDispatchOrderStatusDto.Sent ? StatusZamowieniaPodjazduKuriera.Stworzone : StatusZamowieniaPodjazduKuriera.NadaneDoStworzenia
            };
        }

        /// <summary>
        ///     Metoda, której zadaniem jest zwrócenie szczegółów podjazdu zamówionego kuriera
        /// </summary>
        /// <param name="dane"></param>
        /// <returns></returns>
        public PobranieSzczegolowZamowieniaPodjazduWynik PobierzSzczegolyPodjazduKuriera(PobranieSzczegolowZamowieniaPodjazduDane dane)
        {
            var listaWysylkowa = dane.Uchwyt.ListyWysylkowe().Znajdz(l => l.IdentyfikatorWewnetrznyZamowienia == dane.IdentyfikatorZamowieniePodjazdu)?.Dane;

            if (listaWysylkowa == null)
                throw new BladKomunikacjiZSerwisemException($"Nie znaleziono listy wysyłkowej o identyfikatorze wewnętrznym '{dane.IdentyfikatorZamowieniePodjazdu}'.");

            var response = _klientHttpSello.Get<InpostDispatchOrderResponseDto>(Nazwa, $"/v1/dispatch_orders/{listaWysylkowa.IdentyfikatorZamowieniaWSystemieKuriera}", dane.KontekstKonta, authenticationHeader: new AuthenticationHeaderValue("Bearer", dane.DaneUwierzytelnienia.TokenOdswiezeniaLubHaslo))
                                           .ReturnOrThrowException(HandleError<InpostDispatchOrderResponseDto>());

            return new PobranieSzczegolowZamowieniaPodjazduWynik()
            {
                DataPropozycji = response.CreatedAt,
                NazwaPropozycji = response.Comments?.FirstOrDefault()?.Comment,
                IdentyfikatorZamowieniaPodjazduKurieraWSystemieKuriera = response.Id.ToString()
            };

        }

        /// <summary>
        ///     Metoda, której zadaniem jest pobieranie protokołu wysyłki, który możemy przekazać kurierowi do podpisu
        /// </summary>
        /// <param name="dane"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public PobranieProtokoluWysylkiWynik PobierzProtokolWysylki(PobranieProtokoluWysylkiDane dane)
        {
            var response = _klientHttpSello.Get<byte[]>(Nazwa, $"/v1/organizations/{dane.DaneUwierzytelnienia.TokenDostepuLubLogin}/dispatch_orders/printouts?shipment_ids[]={string.Join("&shipment_ids[]=", dane.IdentyfikatoryPaczekWSystemieKuriera.Select(x => x))}", dane.KontekstKonta, authenticationHeader: new AuthenticationHeaderValue("Bearer", dane.DaneUwierzytelnienia.TokenOdswiezeniaLubHaslo), isBinaryData: true)
                                           .ReturnOrThrowException(HandleError<byte[]>());

            return new PobranieProtokoluWysylkiWynik()
            {
                ProtokolWysylki = response
            };
        }

        /// <summary>
        ///     Metoda, której zadaniem jest stworzenie wysyłki w serwisie kuriera
        /// </summary>
        /// <param name="dane">Dane przekazane przez zadanie stworzenia wysyłki w serwisie kuriera z Sello NX, 
        /// na podstawie których plugin integracji może taką wysyłkę utworzyć.</param>
        /// <returns>Informacje o stworzonej u kuriera wysyłce w ustalonym formacie wynikowym</returns>
        public StworzenieWysylkiWynik StworzWysylkeWSerwisie(StworzenieWysylkiDane dane)
        {
            //Tworzymy paczkę w serwisie kuriera
            var content = StworzWysylke(dane.DanePaczki, dane.DaneUwierzytelnienia, dane.KontekstKonta);

            // Informacje o stworzonej przesyłkce należy zapisać w strukturze wynikowej tego zadania. 
            return new StworzenieWysylkiWynik()
            {
                Wysylka = new DaneUtworzonejWysylki()
                {
                    Id = content.Id,
                    Status = StatusMapper.MapToApiStatus(content.Status, !string.IsNullOrEmpty(content.TrackingNumber))
                }
            };
        }

        /// <summary>
        ///     Metoda, której zadaniem jest zaktualizowanie wysyłki w serwisie kuriera
        /// </summary>
        /// <param name="dane">Dane przekazane przez zadanie stworzenia wysyłki w serwisie kuriera z Sello NX, 
        /// na podstawie których plugin integracji może taką wysyłkę zaktualizować.</param>
        /// <returns>Informacje o stworzonej u kuriera wysyłce w ustalonym formacie wynikowym</returns>
        public AktualizacjaWysylkiWynik AktualizujWysylkeWSerwisie(AktualizacjaWysylkiDane dane)
        {
            //W naszym przykładzie aktualizacja wysyłki będzie polegać na ponownej próbie jej utworzenia
            var content = StworzWysylke(dane.DanePaczki, dane.DaneUwierzytelnienia, dane.KontekstKonta);

            return new AktualizacjaWysylkiWynik()
            {
                Wysylka = new DaneUtworzonejWysylki()
                {
                    Id = content.Id,
                    Status = StatusMapper.MapToApiStatus(content.Status, !string.IsNullOrEmpty(content.TrackingNumber))
                }
            };
        }

        /// <summary>
        ///     Metoda, której zadaniem jest pobranie etykiety z serwisu kuriera dla konkretnej paczki
        /// </summary>
        /// <param name="dane">Dane przekazane przez zadanie pobierania etykiety wysyłki z Sello NX, 
        /// na podstawie których plugin integracji może pobrać plik etykiety z serwisu kurierskiego.</param>
        /// <returns>Etykieta nadawcza w ustalonym formacie wynikowym</returns>
        public PobranieEtykietyWynik PobierzEtykiete(PobranieEtykietyDane dane)
        {
            byte[] etykieta = _klientHttpSello.Get<byte[]>(Nazwa, $"/v1/shipments/{dane.IdentyfikatorPaczkiWSystemieKuriera}/label?format=pdf", dane.KontekstKonta, authenticationHeader: new AuthenticationHeaderValue("Bearer", dane.DaneUwierzytelnienia.TokenOdswiezeniaLubHaslo), isBinaryData: true)
                                              .ReturnOrThrowException(HandleError<byte[]>());

            // Dane pobrane z serwisu, należy zapisać w strukturze wynikowej tego zadania.
            return new PobranieEtykietyWynik()
            {
                Etykieta = etykieta
            };
        }

        /// <summary>
        ///     Metoda, której zadaniem jest pobranie numeru nadawczego z serwisu kuriera dla konkretnej paczki identyfikowanej zewnętrznym identyfikatorem kuriera
        /// </summary>
        /// <param name="dane">Dane przekazane przez zadanie pobierania numeru nadawczego paczki z Sello NX, 
        /// na podstawie których plugin integracji może pobrać numer nadawczy (z danych o paczce) z serwisu kurierskiego.</param>
        /// <returns>Numer nadania przesyłki w ustalonym formacie wynikowym</returns>
        public PobranieNumeruNadawczegoWynik PobierzNumerNadawczy(PobranieNumeruNadawczegoDane dane)
        {
            var content = _klientHttpSello.Get<InpostShipmentDto>(Nazwa, $"/v1/shipments/{dane.IdentyfikatorPaczkiWSystemieKuriera}", dane.KontekstKonta, authenticationHeader: new AuthenticationHeaderValue("Bearer", dane.DaneUwierzytelnienia.TokenOdswiezeniaLubHaslo))
                .ReturnOrThrowException(HandleError<InpostShipmentDto>());

            // Dane pobrane z serwisu, należy zapisać w strukturze wynikowej tego zadania. 
            return new PobranieNumeruNadawczegoWynik()
            {
                NumerNadania = content.TrackingNumber
            };
        }

        /// <summary>
        ///     Aktualnie nieużywana jeszcze metoda.
        /// </summary>
        /// <param name="dane"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public PobranieListyPunktowNadaniaWynik PobierzPunktyNadania(PobranieListyPunktowNadaniaDane dane)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Metoda, której zadaniem jest weryfikacja istnienia paczki w serwisie kuriera
        /// </summary>
        /// <param name="dane">Dane przekazane przez zadanie pobierania etykiety z Sello NX, 
        /// na podstawie których plugin integracji może zweryfikować istnienie paczki w serwisie kurierskim.</param>
        /// <returns>Informacja, czy przesyłka istnieje</returns>
        public WeryfikacjaIstnieniaPaczkiWynik WeryfikujIstnieniePaczki(WeryfikacjaIstnieniaPaczkiDane dane)
        {
            var weryfikacjaIstnieniaPaczkiWynik = new WeryfikacjaIstnieniaPaczkiWynik();
            InpostShipmentDto content = null;
            try
            {
                content = _klientHttpSello.Get<InpostShipmentDto>(Nazwa, $"/v1/shipments/{dane.IdentyfikatorPaczkiWSystemieKuriera}", dane.KontekstKonta, authenticationHeader: new AuthenticationHeaderValue("Bearer", dane.DaneUwierzytelnienia.TokenOdswiezeniaLubHaslo))
                                             .ReturnOrThrowException(HandleError<InpostShipmentDto>());
            }
            catch
            {
                weryfikacjaIstnieniaPaczkiWynik.CzyPaczkaIstnieje = false;
            }

            if (content != null)
            {
                weryfikacjaIstnieniaPaczkiWynik.CzyPaczkaIstnieje = content.Status == "created";
            }
            else
            {
                weryfikacjaIstnieniaPaczkiWynik.CzyPaczkaIstnieje = false;
            }

            return weryfikacjaIstnieniaPaczkiWynik;
        }

        /// <summary>
        ///     Metoda wyznaczająca wartość ubezpieczenia.
        /// </summary>
        /// <param name="paczkaWysylkowa">Dane paczki wysyłkowej.</param>
        /// <returns></returns>
        public Kwota WyznaczWartoscUbezpieczenia(IPaczkaWysylkowa paczkaWysylkowa)
        {
            return new Kwota
            {
                Wartosc = paczkaWysylkowa.Dane.KwotaPobrania ?? 0m,
                Waluta = paczkaWysylkowa.Dane.WalutaPobrania,
            };
        }

        /// <summary>
        ///     Metoda wyznaczająca wartość ubezpieczenia.
        /// </summary>
        /// <param name="paczkaWysylkowa"></param>
        /// <returns></returns>
        public Kwota WyznaczWartoscUbezpieczenia(PaczkaWysylkowa paczkaWysylkowa)
        {
            return new Kwota
            {
                Wartosc = paczkaWysylkowa.KwotaPobrania ?? 0m,
                Waluta = paczkaWysylkowa.WalutaPobrania,
            };
        }

        /// <summary>
        ///     Link do wyszukiwarki punktów nadania.
        ///     W przypadku brokerów jak WzA dodatkowo informacja o identyfikatorze kuriera.
        /// </summary>
        /// <param name="idKuriera">Identyfikator kuriera.</param>
        /// <returns></returns>
        public string LinkWyszukiwarkiPunktowNadania(string idKuriera)
        {
            return "https://inpost.pl/znajdz-paczkomat";
        }

        /// <summary>
        ///     Link do wyszukiwarki punktów odbioru.
        ///     W przypadku brokerów jak WzA dodatkowo informacja o identyfikatorze kuriera.
        /// </summary>
        /// <param name="idKuriera">Identyfikator kuriera.</param>
        /// <returns></returns>
        public string LinkWyszukiwarkiPunktowOdbioru(string idKuriera)
        {
            return "https://inpost.pl/znajdz-paczkomat";
        }

        /// <summary>
        ///     Metoda, której zadaniem jest pobranie statusu przesyłki z serwisu kuriera.
        /// </summary>
        /// <param name="dane">Dane przekazane przez zadanie pobierania statusu wysyłki z Sello NX, 
        /// na podstawie których plugin integracji może pobrać status wysyłki z serwisu kurierskiego.</param>
        /// <returns>Status przesyłki w ustalonym formacie wynikowym</returns>
        public PobranieStatusuWynik PobierzStatusWysylki(PobranieStatusuDane dane)
        {
            var content = _klientHttpSello.Get<InpostShipmentDto>(Nazwa, $"/v1/shipments/{dane.IdentyfikatorPaczkiWSystemieKuriera}", dane.KontekstKonta, authenticationHeader: new AuthenticationHeaderValue("Bearer", dane.DaneUwierzytelnienia.TokenOdswiezeniaLubHaslo))
                                          .ReturnOrThrowException(HandleError<InpostShipmentDto>());

            // Dane pobrane z serwisu, należy zapisać w strukturze wynikowej tego zadania. 
            // W przykładzie używamy klasy dodatkowej StatusMapper, która definiuje mapowania statusów oraz opisów, dla czytelności kodu.
            return new PobranieStatusuWynik()
            {
                // Status z serwisu kuriera należy zmapować na jeden ze statusów dostępnych w Sello NX
                // Statusy te są oznaczone różnymi atrybutami, które są wykorzystywane przez Sello NX do jednolitego prezentowania informacji w interfejsie
                // ale też bazują na nich różne algorytmy, dostępne funkcje itp.
                Status = StatusMapper.MapToApiStatus(content.Status, !string.IsNullOrEmpty(content.TrackingNumber)),

                // Dodatkowy opis słowny danego statusu paczki, również wyświetlany w interfejsie użytkownika.
                // Zalecamy stosowanie możliwie krótkich opisów, które wyjaśnią co dokładnie dzieje się z paczką.
                StatusSlowny = StatusMapper.GetStatusDescription(content.Status),
            };
        }

        /// <summary>
        ///     Metoda, której zadaniem jest pobranie statusów przesyłek z serwisu kuriera.
        /// </summary>
        /// <param name="dane">Dane przekazane przez zadanie pobierania statusów wysyłek z Sello NX, 
        /// na podstawie których plugin integracji może pobrać statusy wysyłek z serwisu kurierskiego.</param>
        /// <returns>Statusy przesyłek w ustalonym formacie wynikowym</returns>
        public PobranieStatusowWysylekWynik PobierzStatusyWysylek(PobranieStatusowWysylekDane dane)
        {
            var listaStatusow = new List<PobranieStatusuWynik>();

            foreach (var id in dane.IdentyfikatoryPaczekWSystemieKuriera)
            {
                var content = _klientHttpSello.Get<InpostShipmentDto>(Nazwa, $"/v1/shipments/{id}", dane.KontekstKonta, authenticationHeader: new AuthenticationHeaderValue("Bearer", dane.DaneUwierzytelnienia.TokenOdswiezeniaLubHaslo))
                                              .ReturnOrThrowException(HandleError<InpostShipmentDto>());

                listaStatusow.Add(new PobranieStatusuWynik()
                {
                    // Status z serwisu kuriera należy zmapować na jeden ze statusów dostępnych w Sello NX
                    // Statusy te są oznaczone różnymi atrybutami, które są wykorzystywane przez Sello NX do jednolitego prezentowania informacji w interfejsie
                    // ale też bazują na nich różne algorytmy, dostępne funkcje itp.
                    Status = StatusMapper.MapToApiStatus(content.Status, !string.IsNullOrEmpty(content.TrackingNumber)),

                    // Dodatkowy opis słowny danego statusu paczki, również wyświetlany w interfejsie użytkownika.
                    // Zalecamy stosowanie możliwie krótkich opisów, które wyjaśnią co dokładnie dzieje się z paczką.
                    StatusSlowny = StatusMapper.GetStatusDescription(content.Status),
                });
            }

            return new PobranieStatusowWysylekWynik()
            {
                StatusyPaczek = listaStatusow
            };
        }

        #endregion IIntegracjaKuriera

        #region Metody pomocnicze
        /// <summary>
        ///     Metoda pomocnicza tworząca paczkę w systemie kuriera
        /// </summary>
        /// <param name="paczkaWysylkowa">Paczka wysyłkowa</param>
        /// <param name="daneUwierzytelnienia">Dane uwierzytelniania</param>
        /// <returns>Odpowiedź z serwisu kuriera jako dto</returns>
        private InpostShipmentDto StworzWysylke(IAktualneDanePaczkiWysylkowej paczkaWysylkowa, DaneUwierzytelnienia daneUwierzytelnienia, IKontekstKontaIntegracji kontekstKonta)
        {
            // Tworzymy strukturę definiującą nową przesyłkę wg kuriera (w tym przypadku do InPostu)
            var wysylka = new InpostCreateShipmentDto();

            var zamowienie = paczkaWysylkowa.Zamowienie;
            var sposobDostawy = zamowienie.SposobDostawy;

            // Uzupełniamy pola struktury paczki dannymi potrzebnymi do jej stworzenia

            // Dane paczki
            InpostParcelDto paczkaInpost = new InpostParcelDto();
            paczkaInpost.Template = "small";
            wysylka.Parcels = new InpostParcelDto[] { paczkaInpost };

            // Ze sposobu dostawy za pomocą metody PodajPole() sięgamy po parametry jakie ustawił użytkownik, poniżej pole z wybraną usługą
            wysylka.Service = sposobDostawy.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.Usluga);

            // Dodajemy wybrane usługi dodatkowe
            wysylka.AdditionalServices = sposobDostawy.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.UslugiDodatkowe)?.ToList() ?? new List<string>();


            wysylka.CustomAttributes = new InpostAttributesDto();

            // Ustawiamy sposób wysyłki (nadania paczki)
            wysylka.CustomAttributes.SendingMethod = sposobDostawy.PodajPole(PolaIntegracjiSelloStale.SposobyDostawy.SposobWysylki);

            // Pole z numerem referencyjnym, umożliwiające przypisanie później wygenerowanej etykiety do zamówienia wysyłkowego w Sello NX
            wysylka.Reference = zamowienie.Tytul;


            // Dane odbiorcy
            wysylka.Receiver = new InpostPeerDto();
            if (!string.IsNullOrEmpty(zamowienie.FirmaOdbiorcy))
                wysylka.Receiver.CompanyName = zamowienie.FirmaOdbiorcy;
            else
            {
                wysylka.Receiver.FirstName = zamowienie.ImieOdbiorcy;
                wysylka.Receiver.LastName = zamowienie.NazwiskoOdbiorcy;
            }
            wysylka.Receiver.Email = zamowienie.Email;
            wysylka.Receiver.Phone = zamowienie.Telefon;

            // W zależności od typu wybranej usługi, należy przekazać różne dane
            // Kurier wymaga podania adresu dostawy, odbiór w punkcie - identyfikatora tego punktu
            if (wysylka.Service == "inpost_courier_standard")
            {
                var buildingNumber = zamowienie.NumerDomuOdbiorcy;
                if (!string.IsNullOrEmpty(zamowienie.NumerLokaluOdbiorcy))
                    buildingNumber += "/" + zamowienie.NumerLokaluOdbiorcy;

                wysylka.Receiver.Address = new InpostAddressDto()
                {
                    Street = zamowienie.UlicaOdbiorcy,
                    BuildingNumber = buildingNumber,
                    City = zamowienie.MiejscowoscOdbiorcy,
                    ZipCode = zamowienie.KodPocztowyOdbiorcy,
                    CountryCode = zamowienie.PanstwoOdbiorcy
                };
            }
            var idOrganizacji = paczkaWysylkowa.Zamowienie.SposobDostawy.KontoIntegracji.PodajPole(PolaIntegracjiSelloStale.NazwaKonta);

            return _klientHttpSello.Post<InpostShipmentDto>(Nazwa, $"/v1/organizations/{idOrganizacji}/shipments", new StringContent(JsonConvert.SerializeObject(wysylka), System.Text.Encoding.UTF8, "application/json"), kontekstKonta, authenticationHeader: new AuthenticationHeaderValue("Bearer", daneUwierzytelnienia.TokenOdswiezeniaLubHaslo))
                             .ReturnOrThrowException(HandleError<InpostShipmentDto>());
        }

        /// <summary>
        ///     Przykładowa metoda rozszerzająca wbudowaną obsługę błędów.
        /// </summary>
        /// <typeparam name="T">Typ danych.</typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        private Action<ApiResponse<T>> HandleError<T>()
        {
            return (apiResponse) =>
            {
                if (apiResponse.OK)
                {
                    return;
                }

                // W zależności od statusu błędu HTTP można zwrócić konkretną informację rzucając wyjątek. 
                // Zamówienia wysyłkowe będą wtedy podświetlone na kolor czerwony wskazując problem z synchronizacją
                // a treść przekazana w wyjątku zostanie dołączona do informacji prezentowanej użytkownikowi programu.
                switch (apiResponse.HttpStatusCode)
                {
                    // Typowy błąd 404
                    case HttpStatusCode.NotFound: throw new InvalidOperationException("Nie znaleziono przesyłki w serwisie kurierskim");

                    // Błędy związane z brakiem autoryzacji, odmową dostępu, niepoprawnymi danymi logowania należy zwrócić rzucając wyjątek UnauthorizedAccessException
                    // Dzięki temu Sello NX oznaczy konto integracji, jako niezweryfikowane i nie będzie ponawiać prób komunikacji z tym kontem, do czasu, aż
                    // użytkownik nie poprawi danych autoryzujących. Sytuacja taka występuje np po zmianie hasła dostępowego po stronie serwisu kuriera.
                    case HttpStatusCode.Unauthorized:
                    case HttpStatusCode.Forbidden: throw new UnauthorizedAccessException();

                }

                // W przypadkach gdy serwis kurierski zwraca dodatkowo informacje na temat błędnie wysłanego żadania (np. niepoprawne dane w paczce)
                // należy je również wyciągnąć z odpowiedzi serwera i rzucić wyjątkiem z podaniem treści tych błędów.
                // Zostaną one wyświetlone użytkownikowi, aby miał szansę poprawić dane i ponowić żądanie
                var content = apiResponse.DataRaw;
                var errorObj = JsonConvert.DeserializeObject<InpostBadRequestDto>(content);
                if (errorObj != null)
                {
                    throw new ArgumentException(errorObj.Errors != null ? errorObj.Errors.ToString() : errorObj.Error);
                }
                else
                {
                    throw new InvalidOperationException("Problem z odczytem informacji z serwera.");
                }
            };
        }
        #endregion
    }
}