using InsERT.Moria.DeklaracjeZUS;
using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Raporty;
using InsERT.Moria.Rozszerzanie;
using RaportySferyczne.Narzedzia;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RaportySferyczne.Platnik
{
    public class PorownanieDanychPracownikowRaport : IDaneRaportu
    {
        private const string PARAMETR_NAZWA_PLATNIKA = "NAZWA_PLATNIKA";
        private const string PARAMETR_RODZAJ_BAZY_DANYCH = "RODZAJ_BAZY_DANYCH";
        private const string PARAMETR_SQL_NAZWA_SERWERA = "SQL_NAZWA_SERWERA";
        private const string PARAMETR_SQL_NAZWA_BAZY_DANYCH = "SQL_NAZWA_BAZY_DANYCH";
        private const string PARAMETR_SQL_AUTENTYKACJA_WINDOWS = "SQL_AUTENTYKACJA_WINDOWS";
        private const string PARAMETR_SQL_LOGIN = "SQL_LOGIN";
        private const string PARAMETR_SQL_HASLO = "SQL_HASLO";
        private const string PARAMETR_ACCESS_SCIEZKA_DO_PLIKU = "ACCESS_SCIEZKA_DO_PLIKU";
        private const string PARAMETR_ACCESS_HASLO = "ACCESS_HASLO";

        private readonly IPodmioty _podmioty;
        private readonly IParametryDeklaracjiZUS _parametryDeklaracjiZUS;

        public PorownanieDanychPracownikowRaport(
            IPodmioty podmioty,
            IParametryDeklaracjiZUS parametryDeklaracjiZUS)
        {
            _podmioty = podmioty;
            _parametryDeklaracjiZUS = parametryDeklaracjiZUS;
        }

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("25fdc979-777b-4ddf-b7b6-6bb0f282c3d6");

        public string Nazwa => "Płatnik - porównanie danych";

        public string Opis => "Raport porównuje dane ubezpieczonego (pracownika) w nexo do danych zawartych w programie Płatnik.";

        public Type TypWyniku => typeof(DanePracownika);

        public Type TypGlownegoElementu => typeof(PracownikGr);

        public void DefiniujParametryRaportu(IBudowniczyParametrowRaportu fabrykaParametrowRaportu)
        {
            fabrykaParametrowRaportu.DodajTekstowy(PARAMETR_NAZWA_PLATNIKA, "Nazwa płatnika", _parametryDeklaracjiZUS.NazwaPlatnikaZUS);
            fabrykaParametrowRaportu.Dodaj(DefinicjaParametru.UtworzParametrLista(PARAMETR_RODZAJ_BAZY_DANYCH, "Rodzaj bazy danych")
                .DodajElementListy((int)RodzajBazyDanych.SQL, "SQL")
                .DodajElementListy((int)RodzajBazyDanych.Access, "Access")
                .NieZezwalajNaBrakWartosci()
                .UstawDomyslnieWybranyElement((int)RodzajBazyDanych.SQL));
            fabrykaParametrowRaportu.DodajTekstowy(PARAMETR_SQL_NAZWA_SERWERA, "(SQL) Nazwa serwera");
            fabrykaParametrowRaportu.DodajTekstowy(PARAMETR_SQL_NAZWA_BAZY_DANYCH, "(SQL) Nazwa bazy danych");
            fabrykaParametrowRaportu.Dodaj(DefinicjaParametru.UtworzParametrLista(PARAMETR_SQL_AUTENTYKACJA_WINDOWS, "(SQL) Autentykacja Windows")
                .DodajElementListy((int)TakNie.Nie, "Nie")
                .DodajElementListy((int)TakNie.Tak, "Tak")
                .NieZezwalajNaBrakWartosci()
                .UstawDomyslnieWybranyElement((int)TakNie.Tak));
            fabrykaParametrowRaportu.DodajTekstowy(PARAMETR_SQL_LOGIN, "(SQL) Login");
            fabrykaParametrowRaportu.DodajTekstowy(PARAMETR_SQL_HASLO, "(SQL) Hasło");
            fabrykaParametrowRaportu.DodajTekstowy(PARAMETR_ACCESS_SCIEZKA_DO_PLIKU, "(Access) Ścieżka do pliku");
            fabrykaParametrowRaportu.DodajTekstowy(PARAMETR_ACCESS_HASLO, "(Access) Hasło");
        }

        public IQueryable PodajDaneRaportu(IParametryDanychRaportu parametryDanychRaportu)
        {
            var connection = GetPlatnikConnection(parametryDanychRaportu);

            if (connection == null)
                return KomunikatJakoWynik("Nie wskazano danych połączenia z bazą Płatnika.");

            var nazwaPlatnika = parametryDanychRaportu.PodajWartoscParametruTekstowego(PARAMETR_NAZWA_PLATNIKA);
            try
            {
                var idPlatnika = PobierzIdPlatnika(connection, nazwaPlatnika);
                if (!idPlatnika.HasValue)
                    return KomunikatJakoWynik($"Nie znaleziono Płatnika o nazwie {nazwaPlatnika}.");

                var pracownicyNexo = PobierzDanePracownikowNexo();
                var pracownicyPlatnik = PobierzDanePracownikowPlatnik(connection, idPlatnika.Value);

                var pracownicy = Polacz(pracownicyNexo, pracownicyPlatnik);

                return pracownicy.AsQueryable();
            }
            catch (Exception e)
            {
                return KomunikatJakoWynik($"Wystąpił błąd. {e.MostInnerException().Message}");
            }
        }

        private IEnumerable<DanePracownika> PobierzDanePracownikowNexo()
        {
            return _podmioty.Dane.WszyscyPracownicy()
                .Where(x => x.Osoba.Pracownik.Aktywny)
                .Select(x => new
                {
                    Podmiot = x,
                    Osoba = x.Osoba
                })
                .Select(x => new DanePracownika()
                {
                    Id = x.Podmiot.Id,
                    NexoImie = x.Osoba.Imie,
                    NexoNazwisko = x.Osoba.Nazwisko,
                    NexoPesel = x.Osoba.PESEL,
                    NexoDataUrodzenia = x.Osoba.DataUrodzenia,
                }).ToList();
        }

        private IPlatnikDbConnection GetPlatnikConnection(IParametryDanychRaportu parametryDanychRaportu)
        {
            var rodzajBazyDanych = (RodzajBazyDanych?)parametryDanychRaportu.PodajWartoscParametruLista(PARAMETR_RODZAJ_BAZY_DANYCH);
            var nazwaPlatnika = parametryDanychRaportu.PodajWartoscParametruTekstowego(PARAMETR_NAZWA_PLATNIKA);
            if (String.IsNullOrWhiteSpace(nazwaPlatnika) || !rodzajBazyDanych.HasValue)
                return null;

            if (rodzajBazyDanych.Value == RodzajBazyDanych.SQL)
            {
                var nazwaSerwera = parametryDanychRaportu.PodajWartoscParametruTekstowego(PARAMETR_SQL_NAZWA_SERWERA);
                var nazwaBazyDanych = parametryDanychRaportu.PodajWartoscParametruTekstowego(PARAMETR_SQL_NAZWA_BAZY_DANYCH);
                var autentykacjaWindows = (TakNie?)parametryDanychRaportu.PodajWartoscParametruLista(PARAMETR_SQL_AUTENTYKACJA_WINDOWS);

                if (String.IsNullOrWhiteSpace(nazwaSerwera) || String.IsNullOrWhiteSpace(nazwaBazyDanych) || !autentykacjaWindows.HasValue)
                    return null;

                if (autentykacjaWindows == TakNie.Tak)
                {
                    return new PlatnikSqlDbConnection(nazwaSerwera, nazwaBazyDanych);
                }
                else
                {
                    var login = parametryDanychRaportu.PodajWartoscParametruTekstowego(PARAMETR_SQL_LOGIN);
                    var haslo = parametryDanychRaportu.PodajWartoscParametruTekstowego(PARAMETR_SQL_HASLO);

                    if (String.IsNullOrWhiteSpace(login) || String.IsNullOrWhiteSpace(haslo))
                        return null;

                    return new PlatnikSqlDbConnection(nazwaSerwera, nazwaBazyDanych, login, haslo);
                }
            }
            else
            {
                var sciezkaDoPliku = parametryDanychRaportu.PodajWartoscParametruTekstowego(PARAMETR_ACCESS_SCIEZKA_DO_PLIKU);
                var haslo = parametryDanychRaportu.PodajWartoscParametruTekstowego(PARAMETR_ACCESS_HASLO);

                if (String.IsNullOrWhiteSpace(sciezkaDoPliku) || String.IsNullOrWhiteSpace(haslo))
                    return null;

                return new PlatnikAccessDbConnection(sciezkaDoPliku, haslo);
            }
        }

        private int? PobierzIdPlatnika(IPlatnikDbConnection connection, string nazwaPlatnika)
        {
            return connection.ExecuteScalar<int?>(@"
                SELECT TOP 1
	                P.ID
                FROM PLATNIK AS P
                WHERE P.NAZWASKR = @NazwaPlatnika", 
                ("@NazwaPlatnika", nazwaPlatnika));
        }

        private IEnumerable<DanePracownika> PobierzDanePracownikowPlatnik(IPlatnikDbConnection connection, int idPlatnika)
        {
            var pracownicy = new List<DanePracownika>();
            connection.ExecuteReader(@"
                SELECT 
	                U_Ident.IMIEPIERW,
	                U_Ident.NAZWISKO,
	                U_Ident.DATAURODZ,
	                U_Ident.PESEL
                FROM (UBEZPIECZONY AS U
	                INNER JOIN UBEZP_IDENT AS U_Ident
		                ON U_Ident.ID_UBEZPIECZONY = U.ID)
                WHERE U.ID_PLATNIK = @IdPlatnika AND 
                        U_Ident.STATUS_DANE = 'K'",
                reader =>
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            var dane = new DanePracownika();
                            if (!reader.IsDBNull(0))
                                dane.PlatnikImie = reader.GetString(0);
                            if (!reader.IsDBNull(1))
                                dane.PlatnikNazwisko = reader.GetString(1);
                            if (!reader.IsDBNull(2))
                                dane.PlatnikDataUrodzenia = reader.GetDateTime(2);
                            if (!reader.IsDBNull(3))
                                dane.PlatnikPesel = reader.GetString(3);

                            pracownicy.Add(dane);
                        }
                    }
                },
                ("@IdPlatnika", idPlatnika));

            return pracownicy;
        }

        private IEnumerable<DanePracownika> Polacz(IEnumerable<DanePracownika> pracownicyNexo, IEnumerable<DanePracownika> pracownicyPlatnik)
        {
            var listaPracownikowNexo = pracownicyNexo.ToList();
            var listaPracownikowPlatnik = pracownicyPlatnik.ToList();
            var listaWynikowa = new List<DanePracownika>();

            foreach (var pracownikNexo in listaPracownikowNexo.ToList())
            {
                var pracownikPlatnik = listaPracownikowPlatnik.FirstOrDefault(x => pracownikNexo.NexoPesel == x.PlatnikPesel);

                if (pracownikPlatnik != null)
                {
                    listaWynikowa.Add(Polacz(pracownikNexo, pracownikPlatnik));
                    listaPracownikowNexo.Remove(pracownikNexo);
                    listaPracownikowPlatnik.Remove(pracownikPlatnik);
                }
            }

            int id = -1;
            foreach (var pracownikPlatnik in listaPracownikowPlatnik)
            {
                pracownikPlatnik.Id = id--;
            }

            return listaWynikowa.Concat(listaPracownikowNexo).Concat(listaPracownikowPlatnik);
        }

        private DanePracownika Polacz(DanePracownika pracownikNexo, DanePracownika pracownikPlatnik)
        {
            return new DanePracownika()
            {
                Id = pracownikNexo.Id,
                NexoImie = pracownikNexo.NexoImie,
                NexoNazwisko = pracownikNexo.NexoNazwisko,
                NexoDataUrodzenia = pracownikNexo.NexoDataUrodzenia,
                NexoPesel = pracownikNexo.NexoPesel,
                PlatnikImie = pracownikPlatnik.PlatnikImie,
                PlatnikNazwisko = pracownikPlatnik.PlatnikNazwisko,
                PlatnikDataUrodzenia = pracownikPlatnik.PlatnikDataUrodzenia,
                PlatnikPesel = pracownikPlatnik.PlatnikPesel
            };
        }

        private IQueryable KomunikatJakoWynik(string komunikat)
        {
            return (new DanePracownika[]
            {
                new DanePracownika()
                {
                    NexoImie = komunikat
                }
            }).AsQueryable();
        }
    }
}