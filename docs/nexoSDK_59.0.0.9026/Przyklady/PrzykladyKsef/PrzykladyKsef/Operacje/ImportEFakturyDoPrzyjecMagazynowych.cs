using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Mox.Validation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrzykladyKsef.Operacje
{
    public class ImportEFakturyDoPrzyjecMagazynowych : BazowaOperacja, IPostepImportuEFaktury
    {
        public ImportEFakturyDoPrzyjecMagazynowych(Uchwyt sfera) : base(sfera) { }

        public void RaportujPostep(PostepImportuEFakturyEventArgs args)
        {
            Console.WriteLine($"{args.Opis} - {args.DodatkowyOpis}. {(args.Niedeterministyczny ? string.Empty : $"{args.Wartosc} z {args.MaksymalnaWartosc}")}");
        }

        public override void Wykonaj() => ImportEFakturyDoPrzyjec();

        /// <summary>
        /// Wyszukuje niezafakturowane przyjęcia od klienta wskazanego na e-Fakturze z uwzględnieniem numerów wydań wykazanych w danych XML.
        /// </summary>
        /// <param name="menedzerImportu">Menedżer importu e-Faktur.</param>
        /// <param name="dokumentElektroniczny">Importowana e-Faktura.</param>
        /// <returns>Dane niezafakturowanych przyjęć.</returns>
        private IEnumerable<IDaneNiezafakturowanegoPrzyjecia> ZnajdzNiezafakturowanePrzyjecia(IMenedzerImportuEFaktur menedzerImportu, DokumentElektroniczny dokumentElektroniczny)
        {
            IDaneZnalezionychPrzyjec daneNiezafakturowanychPrzyjec = menedzerImportu.WyszukajNiezafakturowanePrzyjecia(dokumentElektroniczny);
            if (daneNiezafakturowanychPrzyjec.WykazaneNaFakturze.Any())
            {
                // W danych e-Faktury wykazano pewien zbiór numerów dokumentów magazynowych:
                Console.WriteLine($"W danych e-Faktury wykazano następujące dokumenty magazynowe: {string.Join(", ", daneNiezafakturowanychPrzyjec.WykazaneNaFakturze)}");
            }
            if (daneNiezafakturowanychPrzyjec.Brakujace.Any())
            {
                // W danych e-Faktury wykazano dokumenty, które nie zostały wprowadzone do ewidencji dokumentów magazynowych przyjęć:
                Console.WriteLine($"Dokumenty, które nie zostały wykazane w ewidencji przyjęć: {string.Join(", ", daneNiezafakturowanychPrzyjec.Brakujace)}");
            }
            Console.WriteLine($"Dla klienta rozpoznanego na e-Fakturze istnieje {daneNiezafakturowanychPrzyjec.WszystkieNiezafakturowane.Count()} niezafakturowanych dokumentów magazynowych przyjęć.");
            if (!daneNiezafakturowanychPrzyjec.ZnalezioneDlaEFaktury.Any())
            {
                Console.WriteLine("W ewidencji dokumentów przyjęć nie znaleziono żadnych dokumentów z numerem oryginału wykazanym w danych e-Faktury");
            }
            // Tutaj może być konieczne udostępnienie użytkownikowi możliwości wyboru przyjęć, które chce zafakturować.
            // Wybieramy te przyjęcia, które zostały wykazane w danych e-Faktury.
            return daneNiezafakturowanychPrzyjec.ZnalezioneDlaEFaktury;
            // Można tutaj wybrać dowolny podzbiór z pola IDaneZnalezionychPrzyjec.WszystkieNiezafakturowane:
            //return daneNiezafakturowanychPrzyjec.WszystkieNiezafakturowane; // uwzględniamy wszystkie niezafakturowane przyjęcia bez względu na to czy zostały wykazane w danych XML e-Faktury
            //return daneNiezafakturowanychPrzyjec.WszystkieNiezafakturowane.Where(p => p.WalutaSymbol == "PLN"); // uwzględniamy wszystkie niezafakturowane przyjęcia w walucie PLN
            //return daneNiezafakturowanychPrzyjec.Pozostale; // uwzgledniamy przyjęcia niewykazane w danych e-Faktury
            // etc.
        }

        /// <summary>
        /// Dopasowuje asortyment z kartoteki w Subiekcie do danych pozycji e-Faktury z uwzględnieniem wybranych przyjęć do zafakturowania.
        /// </summary>
        /// <param name="menedzerImportu">Menedżer importu e-Faktur.</param>
        /// <param name="dokumentElektroniczny">Importowana e-Faktura.</param>
        /// <param name="przyjeciaDoZafakturowania">Wybrane przyjęcia magazynowe do zafakturowania.</param>
        /// <returns>Dane pozycji e-Faktury wraz z dopasowaniami asortymentu.</returns>
        private IDaneZnalezionegoAsortymentu DopasujAsortyment(IMenedzerImportuEFaktur menedzerImportu, DokumentElektroniczny dokumentElektroniczny, IEnumerable<IDaneNiezafakturowanegoPrzyjecia> przyjeciaDoZafakturowania)
        {
            IDaneZnalezionegoAsortymentu dane = menedzerImportu.WyszukajAsortyment(dokumentElektroniczny
                , przyjeciaDoZafakturowania
                , this); // implementacja IPostepImportuEFaktury
            Console.WriteLine($"Na wybranych przyjęciach wykazano {dane.AsortymentFakturowanychPrzyjec.Count()} pozycji asortymentowych.");
            Console.WriteLine($"Na e-Fakturze istnieje {dane.PozycjeFaktury.Count()} pozycji.");
            Console.WriteLine($"Automatycznie dopasowano asortyment oraz jednostki miary dla {dane.PozycjeFaktury.Count(p => p.Poprawna)} pozycji.");            
            // Tutaj może być konieczne udostępnienie użytkownikowi możliwości ręcznego dopasowania towarów do pozycji e-Faktury.
            foreach (IDanePozycjiFakturujacej danePozycji in dane.PozycjeFaktury)
            {
                // Pozycja może przechowywać różne błędy/ostrzeżenia/informacje na temat dopasowania asortymentu lub jednostki miary:
                var store = _sfera.PodajObiektTypu<IValidationMetadataStore>();
                foreach (IDataError obiektBledu in danePozycji.Errors.Concat(danePozycji.MemberErrors.Select(me => me.Key)))
                {
                    DataErrorType errorType = store.GetEntryForClrType(obiektBledu.GetType());
                    DataErrorSeverity istotnosc = errorType.Severity;
                    Console.WriteLine($"{(istotnosc == DataErrorSeverity.Error ? "Błąd" : istotnosc == DataErrorSeverity.Warning ? "Ostrzeżenie" : "Informacja")} na pozycji {danePozycji.LP}: {obiektBledu}");
                }

                if (danePozycji.Poprawna)
                {
                    // Pozycja jest "Poprawna" gdy na pozycji dopasowano asortyment oraz wybrana jednostka miary jest zgodna z jednostką wykazaną na e-Fakturze.
                    Console.WriteLine($"Automatycznie dopasowano asortyment oraz jednostkę dla pozycji e-Faktury LP {danePozycji.LP}: {danePozycji.SymbolWybranegoAsortymentu} - {danePozycji.NazwaWybranegoAsortymentu} - {danePozycji.WybranaJednostkaMiary.Symbol}");
                }
                else
                {
                    if (danePozycji.OkreslonoAsortyment)
                    {
                        // Jeśli określono asortyment na pozycji, ale pozycja nie jest poprawna to znaczy, że problemem jest niezgodność jednostki miary.
                        // Użytkownik może:
                        // 1. Dodać synonim globalny lub indywidualny wybranej jednostki miary.
                        // 2. Dodać nową jednostkę miary asortymentu.
                        // Po czym należy odświeżyć kolekcję dostępnych jednostek:
                        danePozycji.OdswiezJednostkiMiary();
                        // i ewentualnie wybrać inną jednostkę miary na pozycji:
                        danePozycji.WybranaJednostkaMiary = danePozycji.DostepneJednostkiMiar.FirstOrDefault(jm => jm.Symbol == danePozycji.DaneWiersza.JednostkaMiary);
                    }
                    else
                    {
                        // Na pozycji nie udało się automatycznie rozpoznać asortymentu i należy go dopasować ręcznie.
                        // Użytkownik może:
                        // 1. Wybrać asortyment z kolekcji asortymentów, które występują na fakturowanych przyjęciach:
                        danePozycji.Wybrany = danePozycji.AsortymentFakturowanychPrzyjec.FirstOrDefault();
                        // 2. Wybrać asortyment z kolekcji asortymentów pasujących do danych wiersza e-Faktury (znalezionych zgodnie z wybranymi algorytmami wyszukiwania określonych w parametrach e-Faktur):
                        danePozycji.Wybrany = danePozycji.Znaleziony.FirstOrDefault();
                        // 3. Wybrać asortyment z kartoteki asortymentu:
                        danePozycji.Wybrany = _sfera.Asortymenty().Dane.Wszystkie().FirstOrDefault();
                        // 4. Zdecydować, że na danej pozycji znajdzie się usługa jednorazowa:
                        danePozycji.UslugaJednorazowa = true;
                    }
                }
            }
            return dane;
        }

        /// <summary>
        /// Tworzy parametry importu e-Faktury.
        /// </summary>
        /// <returns>Obiekt parametrów.</returns>
        private ParametryGrupowaniaImportuEFaktury StworzParametry() => new ParametryGrupowaniaImportuEFaktury()
        {
            // Inny dostawca na importowanym dokumencie zakupu:
            Dostawca = null,
            // Metoda grupowania pozycji, które zostały wykazane na dokumentach magazynowych, ale nie zostały uwzględnione na e-Fakturze:
            MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.BezKonsolidacji,
            // Inne parametry:
            MetodaPrzenoszeniaKategorii = MetodaPrzenoszeniaKategorii.ZWyroznionegoDokumentu,
            MetodaPrzenoszeniaUwag = MetodaPrzenoszeniaUwag.Skonsoliduj,
            MetodaWyliczeniaCen = MetodaWyliczeniaCen.WyliczaniePoSredniej
        };

        /// <summary>
        /// Wykonuje import e-Faktury z uwzględnieniem wybranych niezafakturowanych przyjęć jako korekta dokumentu zakupu.
        /// </summary>
        /// <param name="dokumentElektroniczny">Importowana e-Faktura.</param>
        /// <param name="pozycjeFaktury">Pozycje e-Faktury wraz z dopasowanym asortymentem.</param>
        /// <param name="przyjeciaDoZafakturowania">Wybrane przyjęcia magazynowe do zafakturowania.</param>
        private void ImportJakoKorekta(DokumentElektroniczny dokumentElektroniczny, IEnumerable<IDanePozycjiFakturujacej> pozycjeFaktury, IEnumerable<IDaneNiezafakturowanegoPrzyjecia> przyjeciaDoZafakturowania)
        {
            Console.WriteLine("Import e-Faktury jako korekta zakupu.");
            IKorektyDokumentowZakupu korektyZakupu = _sfera.KorektyDokumentowZakupu();
            using (IKorektaDokumentuZakupu korekta = korektyZakupu.UtworzKorekteFakturyZakupu())
            {
                IEnumerable<IWynikFakturowaniaPozycji> wynikFakturowania = korekta.ObslugaImportuEFaktur.WypelnijNaPodstawieDokumentuElektronicznego(dokumentElektroniczny
                    , pozycjeFaktury
                    , przyjeciaDoZafakturowania.FirstOrDefault()
                    , przyjeciaDoZafakturowania
                    , StworzParametry()
                    , this); // implementacja IPostepImportuEFaktury
                // Sprawdzenie rozbieżności, które wynikają z różnic pomiędzy pozycjami dokumentów magazynowych, a pozycjami e-Faktury.
                if (wynikFakturowania.Any(w => w.Rozbieznosc != RozbieznoscFakturowania.Brak))
                {
                    Console.WriteLine($"Podczas fakturowania wystąpiło {wynikFakturowania.Count(w => w.Rozbieznosc != RozbieznoscFakturowania.Brak)} rozbieżności.");
                    foreach (IWynikFakturowaniaPozycji rozbieznosc in wynikFakturowania.Where(w => w.Rozbieznosc != RozbieznoscFakturowania.Brak))
                        Console.WriteLine($"{(rozbieznosc.LpEFaktury.HasValue ? $"Pozycja e-Faktury LP: {rozbieznosc.LpEFaktury.Value}" : "Pozycja niepowiązana")} - {rozbieznosc.Opis}");
                }
                if (korekta.Zapisz())
                {
                    Console.WriteLine($"Poprawnie zapisano importowaną korektę dokumentu zakupu: {korekta.Dane.NumerWewnetrzny.PelnaSygnatura}");
                }
                else
                {
                    korekta.WypiszBledy();
                }
            }
        }

        /// <summary>
        /// Wykonuje import e-Faktury z uwzględnieniem wybranych niezafakturowanych przyjęć jako dokument zakupu.
        /// </summary>
        /// <param name="dokumentElektroniczny">Importowana e-Faktura.</param>
        /// <param name="pozycjeFaktury">Pozycje e-Faktury wraz z dopasowanym asortymentem.</param>
        /// <param name="przyjeciaDoZafakturowania">Wybrane przyjęcia magazynowe do zafakturowania.</param>
        private void Import(DokumentElektroniczny dokumentElektroniczny, IEnumerable<IDanePozycjiFakturujacej> pozycjeFaktury, IEnumerable<IDaneNiezafakturowanegoPrzyjecia> przyjeciaDoZafakturowania)
        {
            Console.WriteLine("Import e-Faktury jako faktura zakupu.");
            IDokumentyZakupu dokumentyZakupu = _sfera.DokumentyZakupu();
            using (IDokumentZakupu dokument = dokumentyZakupu.UtworzFaktureZakupu())
            {
                IEnumerable<IWynikFakturowaniaPozycji> wynikFakturowania = dokument.ObslugaImportuEFaktur.WypelnijNaPodstawieDokumentuElektronicznego(dokumentElektroniczny
                    , pozycjeFaktury
                    , przyjeciaDoZafakturowania.FirstOrDefault()
                    , przyjeciaDoZafakturowania
                    , StworzParametry()
                    , this); // implementacja IPostepImportuEFaktury
                // Sprawdzenie rozbieżności, które wynikają z różnic pomiędzy pozycjami dokumentów magazynowych, a pozycjami e-Faktury.
                if (wynikFakturowania.Any(w => w.Rozbieznosc != RozbieznoscFakturowania.Brak))
                {
                    Console.WriteLine($"Podczas fakturowania wystąpiło {wynikFakturowania.Count(w => w.Rozbieznosc != RozbieznoscFakturowania.Brak)} rozbieżności.");
                }
                if (dokument.Zapisz())
                {
                    Console.WriteLine($"Poprawnie zapisano importowany dokument zakupu: {dokument.Dane.NumerWewnetrzny.PelnaSygnatura}");
                }
                else
                {
                    dokument.WypiszBledy();
                }
            }
        }

        /// <summary>
        /// Metoda importuje ostatnią nieprzetworzoną e-Fakturę z uwzględnieniem niezafakturowanych dokumentów magazynowych.
        /// </summary>
        private void ImportEFakturyDoPrzyjec()
        {
            IMenedzerImportuEFaktur menedzer = _sfera.MenedzerImportuEFaktur();
            DokumentElektroniczny importowany = ZnajdzEFaktureDoPrzetworzenia();
            if (importowany == null)
            {
                Console.WriteLine("Brak e-Faktur do przetworzenia.");
                return;
            }
            Console.WriteLine($"Fakturowanie przyjęć magazynowych na podstawie danych e-Faktury {importowany.NumerDokumentu}.");
            // Pobieramy wszystkie niezafakturowane przyjęcia dla klienta rozpoznanego na e-Fakturze:
            IEnumerable<IDaneNiezafakturowanegoPrzyjecia> przyjeciaDoZafakturowania = ZnajdzNiezafakturowanePrzyjecia(menedzer, importowany);
            if (!przyjeciaDoZafakturowania.Any())
            {
                Console.WriteLine($"Nie znaleziono żadnych przyjęć do zafakturowania - należy wykonać import nowego dokumentu zakupu (patrz przykład {nameof(ImportOdebranejEFaktury)}).");
                return;
            }
            // Wyszukiwanie asortymentu wg wybranego podzbioru dokumentów magazynowych:
            IDaneZnalezionegoAsortymentu daneAsortymentu = DopasujAsortyment(menedzer, importowany, przyjeciaDoZafakturowania);
            if (daneAsortymentu.PozycjeFaktury.Any(p => !p.Poprawna))
            {
                Console.WriteLine("Nie udało się dopasować asortymentu i jednostek dla wszystkich pozycji. Nie można kontynuować importu.");
                return;
            }
            if (!daneAsortymentu.CzyAsortymentPrzyjecZamapowany())
            {
                Console.WriteLine("Nie dopasowano całego asortymentu z dokumentów magazynowych do pozycji e-Faktury.");
            }
            // Tworzenie dokumentu zakupu odpowiedniego typu i wypełnianie go danymi e-Faktury z uwzględnieniem wybranych niezafakturowanych dokumentów magazynowych:
            if (importowany.RodzajFaktury == (byte)RodzajEFaktury.KOR
                || importowany.RodzajFaktury == (byte)RodzajEFaktury.KOR_ROZ
                || importowany.RodzajFaktury == (byte)RodzajEFaktury.KOR_ZAL)
            {
                ImportJakoKorekta(importowany, daneAsortymentu.PozycjeFaktury, przyjeciaDoZafakturowania);
            }
            else
            {
                Import(importowany, daneAsortymentu.PozycjeFaktury, przyjeciaDoZafakturowania);
            }
        }
    }
}
