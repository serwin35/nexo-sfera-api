using InsERT.Moria.Archiwa;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DaneArchiwalne
{
    public class DaneArchiwalneInsertNexo : IFunkcjaPobieraniaDanychArchiwalnych
    {
        private static readonly List<TypDanych> _obslugiwaneTypy = new List<TypDanych>
        {
            TypDanych.FakturySprzedazy,
            TypDanych.FakturyZakupu,
            TypDanych.KorektySprzedazy,
            TypDanych.KorektyZakupu,
            TypDanych.PrzyjeciaMagazynowe,
            TypDanych.SprzedazDetaliczna,
            TypDanych.WydaniaMagazynowe,
            TypDanych.ZamowieniaDoDostawcow,
            TypDanych.ZamowieniaOdKlientow,
            TypDanych.ZwrotyDetaliczne,
            TypDanych.Magazyny,
            TypDanych.Kategorie,
        };

        private const string _podgladPlaceholder = "###PODGLAD###";

        #region Stałe - konfiguracja

        private const string _nazwaWezlaKonfiguracji = "Konfiguracja";
        private const string _nazwaWezlaSerwera = "sql_server";
        private const string _nazwaWezlaBazy = "database";
        private const string _nazwaWezlaLoginu = "sql_login";
        private const string _nazwaWezlaAutentykacji = "auth_mode";

        private const string _autentykacjaWindows = "WINDOWS";
        private const string _autentykacjaMixed = "MIXED";

        #endregion

        #region Stałe - SQL

        private static readonly string _sqlWersja11 = @"SELECT TOP 1 CASE WHEN VersionMajor=11 THEN 1 WHEN VersionMajor>11 THEN 2 ELSE 0 END FROM InsLauncher.InstalledProducts WHERE Name='Nexo';";

        private static readonly string _sqlPodmiotNexo = @"
IF EXISTS(
    SELECT * FROM sys.objects 
    INNER JOIN sys.schemas ON objects.schema_id=schemas.schema_id 
    WHERE schemas.name='InsLauncher' AND objects.name='InstalledProducts')
        SELECT TOP 1 'true' FROM [InsLauncher].[InstalledProducts] WHERE Name='Nexo'";

        private static readonly string _sqlMagazyny = @"
select mag.Id, mag.Nazwa, mag.Symbol, mag.Opis,
CASE (select 1 from ModelDanychContainer.JednostkiOrganizacyjne jo
    INNER JOIN ModelDanychContainer.JednostkiOrganizacyjne_Centrala c ON c.Id=jo.Id
    WHERE jo.GlownyMagazyn_Id=mag.Id) 
WHEN 1 THEN 1 ELSE 0 END glowny
FROM ModelDanychContainer.Magazyny mag;
";

        private static readonly string _sqlKategorie = @"
SELECT Id, Nazwa, Podtytul FROM ModelDanychContainer.KategorieDokumentow;
";

        private static readonly string _sqlDokumentyFS_pre12 = @"
SELECT dok.Id Id, dok.DataWprowadzenia DataWystawienia, dok.NumerWewnetrzny_PelnaSygnatura NumerPelny, ph.Nazwa Kontrahent, dok.KwotaDoZaplaty Wartosc, 
CASE dok.StatusDokumentuId WHEN 15 THEN 1 WHEN 16 THEN 3 WHEN 17 THEN 3 WHEN 18 THEN 0 WHEN 19 THEN 2 WHEN 46 THEN 3 WHEN 53 THEN 3 WHEN 138 THEN 0 END Status, 
CASE WHEN dok.DokumentDS_StatusFiskalizacji IS NULL THEN 0 ELSE dok.DokumentDS_StatusFiskalizacji END StatusFiskalny, 2 Typ, 
CASE WHEN dok.KonfiguracjaId='7818DEFE-5037-4F8B-B05A-95B8EF6B94D2' THEN 1 -- faktura detaliczna
WHEN dok.KonfiguracjaId='F5CE821F-ED81-4654-A45B-C6BCC5E474DB' THEN 6 -- faktura uprosczona
WHEN dok.DokumentHandlowy_SprzedazZaliczkowa=2 THEN 3
WHEN dok.DokumentHandlowy_SprzedazZaliczkowa=3 THEN 5
WHEN dok.DokumentHandlowy_SprzedazZaliczkowa=4 THEN 4
ELSE 0 END Podtyp,
0 TransakcjaVat, 
CASE dok.KonfiguracjaId WHEN 'F5CE821F-ED81-4654-A45B-C6BCC5E474DB' THEN CAST(1 as bit) ELSE CAST (0 as bit) END FakturaUproszczona, 
dok.NumerWewnetrzny_Numer Numer, dok.Symbol Symbol
FROM ModelDanychContainer.Dokumenty dok 
INNER JOIN ModelDanychContainer.PodmiotHistorie ph ON dok.PodmiotWybranyId = ph.Id 
INNER JOIN ModelDanychContainer.Konfiguracje kf ON dok.KonfiguracjaId=kf.Id 
WHERE kf.TypDokumentu=64 AND dok.DokumentKDS_SposobKorygowania IS NULL
AND kf.SposobKorygowania=1
--AND dok.KonfiguracjaId IN ('E1AD62BC-6AC7-4171-B879-60580CD48EC4','7818DEFE-5037-4F8B-B05A-95B8EF6B94D2',
--'EA5DA62D-BAE6-4F93-9B53-5B406296CE3C','8F95C107-22B7-47E8-BED1-AAAFCCB2789A','F5CE821F-ED81-4654-A45B-C6BCC5E474DB');
";
        private static readonly string _sqlDokumentyFS_v12 = _sqlDokumentyFS_pre12.Replace("DokumentKDS_SposobKorygowania", "SposobKorygowania");
        private static readonly string[] _sqlDokumentyFS = new string[] { _sqlDokumentyFS_pre12, _sqlDokumentyFS_pre12, _sqlDokumentyFS_v12 };

        private static readonly string _sqlDokumentyFZ_pre12 = @"
SELECT dok.Id Id, CASE dok.StatusDokumentuId WHEN 20 THEN 1 WHEN 21 THEN 3 WHEN 22 THEN 0 END Status,
dok.DataWydaniaWystawienia DataOtrzymania, dok.DataWprowadzenia DataWystawienia, 1 Typ, 
CASE dok.DokumentHandlowy_OdpisNaFunduszePromocji WHEN 1 THEN 1 ELSE 0 END Podtyp,
dok.NumerWewnetrzny_PelnaSygnatura NumerPelny, dok.NumerZewnetrzny NumerOryginalu, ph.Nazwa Kontrahent, dok.KwotaDoZaplaty Wartosc, 
0 TransakcjaVat, dok.NumerWewnetrzny_Numer Numer, dok.Symbol Symbol
FROM ModelDanychContainer.Dokumenty dok 
INNER JOIN ModelDanychContainer.PodmiotHistorie ph ON dok.PodmiotWybranyId = ph.Id 
INNER JOIN ModelDanychContainer.Konfiguracje kf ON dok.KonfiguracjaId=kf.Id 
WHERE kf.TypDokumentu=1024 AND dok.DokumentKDZ_SposobKorygowania IS NULL;
";
        private static readonly string _sqlDokumentyFZ_v12 = _sqlDokumentyFZ_pre12.Replace("DokumentKDZ_SposobKorygowania", "SposobKorygowania");
        private static readonly string[] _sqlDokumentyFZ = new string[] { _sqlDokumentyFZ_pre12, _sqlDokumentyFZ_pre12, _sqlDokumentyFZ_v12 };

        private static readonly string _sqlDokumentyKFS_base = @"
SELECT dok.Id Id, CASE dok.StatusDokumentuId WHEN 15 THEN 1 WHEN 16 THEN 3 WHEN 17 THEN 3 WHEN 18 THEN 0 WHEN 19 THEN 2 WHEN 46 THEN 3 WHEN 53 THEN 3 WHEN 138 THEN 0 END Status, 
dok.DataWprowadzenia DataWystawienia, 6 Typ, 
CASE WHEN dok.DokumentKDS_DokumentKorygowany_Id IS NULL THEN 1 WHEN dok.KonfiguracjaId='8F95C107-22B7-47E8-BED1-AAAFCCB2789A' THEN 2 ELSE 0 END Podtyp,
dok.NumerWewnetrzny_PelnaSygnatura NumerPelny, dok.DokumentKDS_DaneKorygowanego_NumerKorygowanego NumerOryginalu, ph.Nazwa Kontrahent, dok.KwotaDoZaplaty Wartosc, 
0 TransakcjaVat, dok.NumerWewnetrzny_Numer Numer, dok.Symbol Symbol
FROM ModelDanychContainer.Dokumenty dok 
INNER JOIN ModelDanychContainer.PodmiotHistorie ph ON dok.PodmiotWybranyId = ph.Id 
INNER JOIN ModelDanychContainer.Konfiguracje kf ON dok.KonfiguracjaId=kf.Id 
{DaneKorygowanego}
WHERE kf.TypDokumentu=64 AND dok.DokumentKDS_SposobKorygowania=1;
";
        private static readonly string _sqlDokumentyKFS_pre12 = _sqlDokumentyKFS_base
            .Replace("{DaneKorygowanego}", string.Empty);
        private static readonly string _sqlDokumentyKFS_v12 = _sqlDokumentyKFS_base
            .Replace("DokumentKDS_SposobKorygowania", "SposobKorygowania")
            .Replace("DokumentKDS_DokumentKorygowany_Id", "DokumentKorygowanyId")
            .Replace("dok.DokumentKDS_DaneKorygowanego_NumerKorygowanego", "kor.NumerKorygowanego")
            .Replace("{DaneKorygowanego}", "LEFT JOIN ModelDanychContainer.DaneKorygowanychDokumentow kor ON kor.Id=dok.Id");
        private static readonly string[] _sqlDokumentyKFS = new string[] { _sqlDokumentyKFS_pre12, _sqlDokumentyKFS_pre12, _sqlDokumentyKFS_v12 };

        private static readonly string _sqlDokumentyPA_pre12 = @"
SELECT dok.Id Id, CASE dok.StatusDokumentuId WHEN 15 THEN 1 WHEN 16 THEN 3 WHEN 17 THEN 3 WHEN 18 THEN 0 WHEN 19 THEN 2 WHEN 46 THEN 3 WHEN 53 THEN 3 WHEN 138 THEN 0 END Status, 
CASE WHEN dok.DokumentDS_StatusFiskalizacji IS NULL THEN 0 ELSE dok.DokumentDS_StatusFiskalizacji END StatusFiskalny,
dok.DataWprowadzenia DataWystawienia, 21 Typ,
CASE dok.KonfiguracjaId 
WHEN 'C59DD1BC-15FD-4276-B001-31A68C8B3831' THEN 2 -- paragon imienny
WHEN '1F30DF50-81BE-4139-87BA-62A4E4B21B22' THEN 1 -- paragon fiskalny
ELSE 0 END Podtyp,
dok.NumerWewnetrzny_PelnaSygnatura NumerPelny, dok.KwotaDoZaplaty Wartosc, dok.Wystawil, 
0 TransakcjaVat, dok.NumerWewnetrzny_Numer Numer, dok.Symbol Symbol
FROM ModelDanychContainer.Dokumenty dok 
LEFT JOIN ModelDanychContainer.PodmiotHistorie ph ON dok.PodmiotWybranyId = ph.Id 
INNER JOIN ModelDanychContainer.Konfiguracje kf ON dok.KonfiguracjaId=kf.Id 
WHERE kf.TypDokumentu=64 AND dok.DokumentKDS_SposobKorygowania IS NULL
AND kf.SposobKorygowania=2
--AND dok.KonfiguracjaId IN ('15B484F1-F4FD-4514-B736-3148361C8C10','C59DD1BC-15FD-4276-B001-31A68C8B3831','1F30DF50-81BE-4139-87BA-62A4E4B21B22');
";
        private static readonly string _sqlDokumentyPA_v12 = _sqlDokumentyPA_pre12.Replace("DokumentKDS_SposobKorygowania", "SposobKorygowania");
        private static readonly string[] _sqlDokumentyPA = new string[] { _sqlDokumentyPA_pre12, _sqlDokumentyPA_pre12, _sqlDokumentyPA_v12 };

        private static readonly string _sqlDokumentyZW_pre12 = @"
SELECT dok.Id Id, CASE dok.StatusDokumentuId WHEN 15 THEN 1 WHEN 16 THEN 3 WHEN 17 THEN 3 WHEN 18 THEN 0 WHEN 19 THEN 2 WHEN 46 THEN 3 WHEN 53 THEN 3 WHEN 138 THEN 0 END Status, 
dok.DataWprowadzenia DataWystawienia, 14 Typ, 
CASE WHEN dok.DokumentKDS_DokumentKorygowany_Id IS NULL THEN 2 ELSE 0 END Podtyp,
dok.NumerWewnetrzny_PelnaSygnatura NumerPelny, dok.KwotaDoZaplaty Wartosc, dok.Wystawil, 
0 TransakcjaVat, dok.NumerWewnetrzny_Numer Numer, dok.Symbol Symbol
FROM ModelDanychContainer.Dokumenty dok 
LEFT JOIN ModelDanychContainer.PodmiotHistorie ph ON dok.PodmiotWybranyId = ph.Id 
INNER JOIN ModelDanychContainer.Konfiguracje kf ON dok.KonfiguracjaId=kf.Id 
WHERE kf.TypDokumentu=64 AND dok.DokumentKDS_SposobKorygowania=2;
";
        private static readonly string _sqlDokumentyZW_v12 = _sqlDokumentyZW_pre12
            .Replace("DokumentKDZ_SposobKorygowania", "SposobKorygowania")
            .Replace("DokumentKDS_SposobKorygowania", "SposobKorygowania")
            .Replace("DokumentKDS_DokumentKorygowany_Id", "DokumentKorygowanyId");
        private static readonly string[] _sqlDokumentyZW = new string[] { _sqlDokumentyZW_pre12, _sqlDokumentyZW_pre12, _sqlDokumentyZW_v12 };

        private static readonly string _sqlDokumentyKFZ_pre12 = @"
SELECT dok.Id Id, CASE dok.StatusDokumentuId WHEN 20 THEN 1 WHEN 21 THEN 3 WHEN 22 THEN 0 END Status,
dok.DataWydaniaWystawienia DataOtrzymania, dok.DataWprowadzenia DataWystawienia, 5 Typ, 
CASE WHEN dok.DokumentKDZ_DokumentKorygowany_Id IS NULL THEN 1 ELSE 0 END Podtyp,
dok.NumerWewnetrzny_PelnaSygnatura NumerPelny, dok.NumerZewnetrzny NumerOryginalu, ph.Nazwa Kontrahent, dok.KwotaDoZaplaty Wartosc, 
0 TransakcjaVat, dok.NumerWewnetrzny_Numer Numer, dok.Symbol Symbol
FROM ModelDanychContainer.Dokumenty dok 
INNER JOIN ModelDanychContainer.PodmiotHistorie ph ON dok.PodmiotWybranyId = ph.Id 
INNER JOIN ModelDanychContainer.Konfiguracje kf ON dok.KonfiguracjaId=kf.Id 
WHERE kf.TypDokumentu=1024 AND dok.DokumentKDZ_SposobKorygowania=1;
";
        private static readonly string _sqlDokumentyKFZ_v12 = _sqlDokumentyKFZ_pre12
            .Replace("DokumentKDZ_SposobKorygowania", "SposobKorygowania")
            .Replace("DokumentKDZ_DokumentKorygowany_Id", "DokumentKorygowanyId");
        private static readonly string[] _sqlDokumentyKFZ = new string[] { _sqlDokumentyKFZ_pre12, _sqlDokumentyKFZ_pre12, _sqlDokumentyKFZ_v12 };

        private static readonly string _sqlDokumentyZK_pre11 = @"
SELECT dok.Id Id, 
CASE WHEN st.Zamkniety=1 THEN 2 ELSE 
	CASE WHEN EXISTS(
		SELECT poz.Id FROM ModelDanychContainer.RealizacjePozycji 
		INNER JOIN ModelDanychContainer.PozycjeDokumentu poz ON PozycjaRealizowanaId=poz.Id
		WHERE PozycjaDokumentu_Dokument_Id = dok.Id) 
	THEN 1
	ELSE 5 END
END StatusRealizacji,
CASE WHEN dok.DokumentZK_TypRezerwacji IS NULL THEN 0 ELSE 1 END StatusRezerwacji,
dok.DataWprowadzenia DataWystawienia, 16 Typ, 0 Podtyp, dok.NumerWewnetrzny_PelnaSygnatura NumerPelny, dok.NumerZewnetrzny NumerOryginalu,
ph.Nazwa Kontrahent, dok.KwotaDoZaplaty Wartosc, 
CASE WHEN EXISTS(
		SELECT poz.Id FROM ModelDanychContainer.RealizacjePozycji 
		INNER JOIN ModelDanychContainer.PozycjeDokumentu poz ON PozycjaRealizowanaId=poz.Id
		WHERE PozycjaDokumentu_Dokument_Id = dok.Id) THEN
		(SELECT SUM(poz.Wartosc_BruttoPoRabacie) FROM ModelDanychContainer.RealizacjePozycji 
		INNER JOIN ModelDanychContainer.PozycjeDokumentu poz ON PozycjaRealizowanaId=poz.Id
		WHERE PozycjaDokumentu_Dokument_Id = dok.Id) 
	ELSE 0 END ZealizowanaWartosc, dok.TerminRealizacji TerminRealizacji, 
(select TOP 1 dokr.DataWydaniaWystawienia from ModelDanychContainer.RealizacjePozycji 
INNER JOIN ModelDanychContainer.PozycjeDokumentu poz ON PozycjaRealizowanaId=poz.Id
INNER JOIN ModelDanychContainer.PozycjeDokumentu rea ON PozycjaRealizujacaId=rea.Id
--INNER JOIN ModelDanychContainer.Dokumenty dokp ON dokp.id=poz.PozycjaDokumentu_Dokument_Id
INNER JOIN ModelDanychContainer.Dokumenty dokr ON dokr.id=rea.PozycjaDokumentu_Dokument_Id
--INNER JOIN ModelDanychContainer.Konfiguracje kfr ON dokr.KonfiguracjaId=kfr.Id 
WHERE poz.PozycjaDokumentu_Dokument_Id=dok.Id AND TypDokumentuRealizujacego IN (64, 4) -- sprzedaż
ORDER BY dokr.DataWydaniaWystawienia DESC) DataRealizacji,
0 TransakcjaVat, 
CAST(CASE WHEN EXISTS(SELECT TOP 1 dokr.DataWydaniaWystawienia from ModelDanychContainer.RealizacjePozycji 
INNER JOIN ModelDanychContainer.PozycjeDokumentu poz ON PozycjaRealizowanaId=poz.Id
INNER JOIN ModelDanychContainer.PozycjeDokumentu rea ON PozycjaRealizujacaId=rea.Id
--INNER JOIN ModelDanychContainer.Dokumenty dokp ON dokp.id=poz.PozycjaDokumentu_Dokument_Id
INNER JOIN ModelDanychContainer.Dokumenty dokr ON dokr.id=rea.PozycjaDokumentu_Dokument_Id
WHERE poz.PozycjaDokumentu_Dokument_Id=dok.Id AND TypDokumentuRealizujacego=2)
 THEN 1 ELSE 0 END as bit) CzyPrzetwarzaneNaZD,
dok.NumerWewnetrzny_Numer Numer, dok.Symbol Symbol
FROM ModelDanychContainer.Dokumenty dok 
INNER JOIN ModelDanychContainer.PodmiotHistorie ph ON dok.PodmiotWybranyId = ph.Id 
INNER JOIN ModelDanychContainer.StatusyDokumentow st ON dok.StatusDokumentuId=st.Id 
INNER JOIN ModelDanychContainer.Konfiguracje kf ON dok.KonfiguracjaId=kf.Id 
WHERE kf.TypDokumentu=1;
";
        private static readonly string _sqlDokumentyZK_v11 = _sqlDokumentyZK_pre11.Replace("PozycjaDokumentu_Dokument_Id", "Dokument_Id");
        private static readonly string[] _sqlDokumentyZK = new string[] { _sqlDokumentyZK_pre11, _sqlDokumentyZK_v11, _sqlDokumentyZK_v11 };

        private static readonly string _sqlDokumentyZD_pre11 = @"
SELECT dok.Id Id, 
CASE WHEN st.Zamkniety=1 THEN 2 ELSE 
	CASE WHEN EXISTS(
		SELECT poz.Id FROM ModelDanychContainer.RealizacjePozycji 
		INNER JOIN ModelDanychContainer.PozycjeDokumentu poz ON PozycjaRealizowanaId=poz.Id 
		WHERE PozycjaDokumentu_Dokument_Id = dok.Id AND TypRealizacjiPozycji=0) 
	THEN 1
	ELSE 5 END
END StatusRealizacji,
dok.DataWprowadzenia DataWystawienia, 15 Typ, 0 Podtyp, dok.NumerWewnetrzny_PelnaSygnatura NumerPelny, 
ph.Nazwa Kontrahent, dok.KwotaDoZaplaty Wartosc, 
CASE WHEN EXISTS(
		SELECT poz.Id FROM ModelDanychContainer.RealizacjePozycji 
		INNER JOIN ModelDanychContainer.PozycjeDokumentu poz ON PozycjaRealizowanaId=poz.Id
		WHERE PozycjaDokumentu_Dokument_Id = dok.Id) THEN
		(SELECT SUM(poz.Wartosc_BruttoPoRabacie) FROM ModelDanychContainer.RealizacjePozycji 
		INNER JOIN ModelDanychContainer.PozycjeDokumentu poz ON PozycjaRealizowanaId=poz.Id
		WHERE PozycjaDokumentu_Dokument_Id = dok.Id) 
	ELSE 0 END ZealizowanaWartosc, dok.TerminRealizacji TerminRealizacji, 
(SELECT TOP 1 dokr.DataWydaniaWystawienia FROM ModelDanychContainer.RealizacjePozycji
INNER JOIN ModelDanychContainer.PozycjeDokumentu poz ON PozycjaRealizowanaId=poz.Id
INNER JOIN ModelDanychContainer.PozycjeDokumentu rea ON PozycjaRealizujacaId=rea.Id
INNER JOIN ModelDanychContainer.Dokumenty dokr ON dokr.id=rea.PozycjaDokumentu_Dokument_Id
WHERE poz.PozycjaDokumentu_Dokument_Id=dok.Id AND TypDokumentuRealizujacego IN (2, 8) -- sprzedaż, przyjęcia magazynowe
ORDER BY dokr.DataWydaniaWystawienia DESC) DataRealizacji,
0 TransakcjaVat, dok.NumerWewnetrzny_Numer Numer, dok.Symbol Symbol
FROM ModelDanychContainer.Dokumenty dok 
INNER JOIN ModelDanychContainer.PodmiotHistorie ph ON dok.PodmiotWybranyId = ph.Id 
INNER JOIN ModelDanychContainer.StatusyDokumentow st ON dok.StatusDokumentuId=st.Id 
INNER JOIN ModelDanychContainer.Konfiguracje kf ON dok.KonfiguracjaId=kf.Id 
WHERE kf.TypDokumentu=2;
";
        private static readonly string _sqlDokumentyZD_v11 = _sqlDokumentyZD_pre11.Replace("PozycjaDokumentu_Dokument_Id", "Dokument_Id");
        private static readonly string[] _sqlDokumentyZD = new string[] { _sqlDokumentyZD_pre11, _sqlDokumentyZD_v11, _sqlDokumentyZD_v11 };

        private static readonly string _sqlDokumentyWZ_pre12 = @"
SELECT dok.Id Id, CASE dok.StatusDokumentuId WHEN 9 THEN 1 WHEN 10 THEN 1 WHEN 11 THEN 3 WHEN 12 THEN 2 ELSE 1 END Status,
dok.DataWprowadzenia DataWystawienia, CASE kf.TypDokumentu WHEN 4 THEN 11 ELSE 13 END Typ, 
CASE WHEN dok.DokumentMagazynowy_PowstalZHandlowego=1 THEN 1 WHEN dok.WyliczenieVAT=1 THEN 2 ELSE 0 END Podtyp, 
dok.NumerWewnetrzny_PelnaSygnatura NumerPelny, ph.Nazwa Kontrahent, 
dok.Wartosc_NettoPoRabacie Wartosc, dok.WartoscTowarowNetto Koszt, pow.NumerWewnetrzny_PelnaSygnatura NumerDokumentuPowiazanego,
dok.NumerWewnetrzny_Numer Numer, dok.Symbol Symbol
FROM ModelDanychContainer.Dokumenty dok 
INNER JOIN ModelDanychContainer.PodmiotHistorie ph ON dok.PodmiotWybranyId = ph.Id 
INNER JOIN ModelDanychContainer.Konfiguracje kf ON dok.KonfiguracjaId=kf.Id 
LEFT JOIN ModelDanychContainer.Dokumenty pow ON pow.Id=dok.Dokument_DokumentPowiazany_Id
WHERE kf.TypDokumentu IN (4,256) AND dok.DokumentKWZ_SposobKorygowania IS NULL AND dok.DokumentKRW_SposobKorygowania IS NULL;
";
        private static readonly string _sqlDokumentyWZ_v12 = _sqlDokumentyWZ_pre12
            .Replace("dok.DokumentKWZ_SposobKorygowania IS NULL AND dok.DokumentKRW_SposobKorygowania IS NULL", "dok.SposobKorygowania IS NULL");
        private static readonly string[] _sqlDokumentyWZ = new string[] { _sqlDokumentyWZ_pre12, _sqlDokumentyWZ_pre12, _sqlDokumentyWZ_v12 };

        private static readonly string _sqlDokumentyPZ_pre12 = @"
SELECT dok.Id Id, CASE dok.StatusDokumentuId WHEN 13 THEN 1 WHEN 14 THEN 3 WHEN 51 THEN 2 ELSE 0 END Status,
dok.DataWprowadzenia DataWystawienia, CASE kf.TypDokumentu WHEN 8 THEN 10 ELSE 12 END Typ, 
CASE WHEN dok.DokumentKPZ_SposobKorygowania=1 THEN 3 WHEN dok.DokumentMagazynowy_PowstalZHandlowego=1 THEN 1 WHEN dok.KonfiguracjaId='10F152C0-AFE9-4757-A3C9-366B1DEADF7E' THEN 2 ELSE 0 END Podtyp, 
dok.NumerWewnetrzny_PelnaSygnatura NumerPelny, dok.NumerZewnetrzny NumerOryginalu, ph.Nazwa Kontrahent, 
dok.Wartosc_NettoPoRabacie Wartosc, dok.WartoscTowarowNetto Koszt, pow.NumerWewnetrzny_PelnaSygnatura NumerDokumentuPowiazanego,
dok.NumerWewnetrzny_Numer Numer, dok.Symbol Symbol
FROM ModelDanychContainer.Dokumenty dok 
INNER JOIN ModelDanychContainer.PodmiotHistorie ph ON dok.PodmiotWybranyId = ph.Id 
INNER JOIN ModelDanychContainer.Konfiguracje kf ON dok.KonfiguracjaId=kf.Id 
LEFT JOIN ModelDanychContainer.Dokumenty pow ON pow.Id=dok.Dokument_DokumentPowiazany_Id
WHERE kf.TypDokumentu IN (8,128);
";
        private static readonly string _sqlDokumentyPZ_v12 = _sqlDokumentyPZ_pre12.Replace(".DokumentKPZ_SposobKorygowania", ".SposobKorygowania");
        private static readonly string[] _sqlDokumentyPZ = new string[] { _sqlDokumentyPZ_pre12, _sqlDokumentyPZ_pre12, _sqlDokumentyPZ_v12 };

        private static readonly string _sqlNaglowekHandlowy_base = @"
SELECT dok.Id, CASE WHEN kf.TypDokumentu&(8+16+1024+2048)>0 THEN dok.DataWydaniaWystawienia ELSE dok.DataWprowadzenia END DataWystawienia, 
dok.DokumentHandlowy_DataSprzedazy DataZakonczeniaDostawy, dok.NumerZewnetrzny, 
CASE WHEN LEN(dok.MiejsceWydaniaWystawienia)>0 THEN dok.MiejsceWydaniaWystawienia ELSE dok.MiejsceWprowadzeniaTekst END MiejsceWystawienia, 
dok.NumerWewnetrzny_PelnaSygnatura, dok.KwotaDoZaplaty, dok.Wartosc_NettoPoRabacie, dok.Wartosc_BruttoPoRabacie, dok.Wartosc_VatPoRabacie, 
wa.Precyzja PrecyzjaWaluty, ph.Nazwa Nabywca_PelnaNazwa, ph.NazwaSkrocona Nabywca_Nazwa, ph.NIPSformatowany Nabywca_NIP, ah.Linia1 Nabywca_Adres1, ah.Linia2 Nabywca_Adres2, 
dok.NumerWewnetrzny_Numer, dok.Symbol, dok.Wystawil, dok.Odebral, dok.Tytul, dok.Podtytul, dok.Uwagi, wa.Symbol Waluta,
{Korekta}, 
{Zwrot}, 
CAST(CASE WHEN kf.TypDokumentu&(2+8+16+1024+2048)>0 THEN 1 ELSE 0 END as bit) Zakupowy,
{NumerKorygowanego}, 
{DataKorygowanego}, 
0.0 NettoPrzedKorekta, 0.0 BruttoPrzedKorekta, 0.0 VatPrzedKorekta, 
0.0 NettoPoKorekcie, 0.0 BruttoPoKorekcie, 0.0 VatPoKorekcie 
FROM ModelDanychContainer.Dokumenty dok 
LEFT JOIN ModelDanychContainer.PodmiotHistorie ph ON dok.PodmiotWybranyId = ph.Id 
INNER JOIN ModelDanychContainer.Konfiguracje kf ON dok.KonfiguracjaId=kf.Id 
LEFT JOIN ModelDanychContainer.AdresHistorie ah ON dok.AdresKontrahentaId = ah.Id 
INNER JOIN ModelDanychContainer.Waluty wa ON dok.Dokument_Waluta_Id=wa.Id 
{DaneKorygowanego}
WHERE dok.Id=@id
";
        private static readonly string _sqlNaglowekHandlowy_pre12 = _sqlNaglowekHandlowy_base
            .Replace("{Korekta}", @"CAST(CASE WHEN dok.DokumentKDZ_SposobKorygowania=1 THEN 1 
WHEN dok.DokumentKDS_SposobKorygowania=1 THEN 1 
ELSE 0 END as bit) Korekta")
            .Replace("{Zwrot}", @"CAST(CASE WHEN dok.DokumentKDZ_SposobKorygowania=2 THEN 1 
WHEN dok.DokumentKDS_SposobKorygowania=2 THEN 1 
ELSE 0 END as bit) Zwrot")
            .Replace("{NumerKorygowanego}", @"CASE WHEN NOT(dok.DokumentKDS_DaneKorygowanego_NumerKorygowanego IS NULL) THEN dok.DokumentKDS_DaneKorygowanego_NumerKorygowanego 
WHEN NOT(dok.DokumentKDZ_DaneKorygowanego_NumerKorygowanego IS NULL) THEN dok.DokumentKDZ_DaneKorygowanego_NumerKorygowanego END NumerKorygowanego")
            .Replace("{DataKorygowanego}", @"CASE WHEN NOT(dok.DokumentKDS_DaneKorygowanego_DataKorygowanego IS NULL) THEN dok.DokumentKDS_DaneKorygowanego_DataKorygowanego 
WHEN NOT(dok.DokumentKDZ_DaneKorygowanego_DataKorygowanego IS NULL) THEN dok.DokumentKDZ_DaneKorygowanego_DataKorygowanego END DataKorygowanego")
            .Replace("{DaneKorygowanego}", @"");
        private static readonly string _sqlNaglowekHandlowy_v12 = _sqlNaglowekHandlowy_base
            .Replace("{Korekta}", "CAST(CASE dok.SposobKorygowania WHEN 1 THEN 1 ELSE 0 END as bit) Korekta")
            .Replace("{Zwrot}", "CAST(CASE dok.SposobKorygowania WHEN 2 THEN 1 ELSE 0 END as bit) Zwrot")
            .Replace("{NumerKorygowanego}", @"kor.NumerKorygowanego")
            .Replace("{DataKorygowanego}", @"kor.DataKorygowanego")
            .Replace("{DaneKorygowanego}", @"LEFT JOIN ModelDanychContainer.DaneKorygowanychDokumentow kor ON kor.Id=dok.Id")
            .Replace(".DokumentHandlowy_DataSprzedazy", ".DataSprzedazy");
        private static readonly string[] _sqlNaglowekHandlowy = new string[] { _sqlNaglowekHandlowy_pre12, _sqlNaglowekHandlowy_pre12, _sqlNaglowekHandlowy_v12 };

        // odpowiada za nagłówki dokumentów magazynowych oraz korekt magazynowych
        private static readonly string _sqlNaglowekMagazynowy_base = @"
SELECT dok.Id, dok.DataWprowadzenia DataWystawienia, dok.DokumentHandlowy_DataSprzedazy DataZakonczeniaDostawy, dok.NumerZewnetrzny, 
CASE WHEN LEN(dok.MiejsceWydaniaWystawienia)>0 THEN dok.MiejsceWydaniaWystawienia ELSE dok.MiejsceWprowadzeniaTekst END MiejsceWystawienia, 
dok.NumerWewnetrzny_PelnaSygnatura, dok.KwotaDoZaplaty, dok.Wartosc_NettoPoRabacie, dok.Wartosc_BruttoPoRabacie, dok.Wartosc_VatPoRabacie, 
wa.Precyzja PrecyzjaWaluty, ph.Nazwa Nabywca_PelnaNazwa, ph.NazwaSkrocona Nabywca_Nazwa, ph.NIPSformatowany Nabywca_NIP, ah.Linia1 Nabywca_Adres1, ah.Linia2 Nabywca_Adres2, 
dok.NumerWewnetrzny_Numer, dok.Symbol, dok.Wystawil, dok.Odebral, dok.Tytul, dok.Podtytul, dok.Uwagi, wa.Symbol Waluta,
dok.DokumentMagazynowy_PowstalZHandlowego PowstalZHandlowego, pow.NumerWewnetrzny_PelnaSygnatura NumerPowiazanego, pow.DataWydaniaWystawienia DataWystawieniaPowiazanego,
{Korekta},
CAST(CASE WHEN kf.TypDokumentu&(8+16+1024+2048)>0 THEN 1 ELSE 0 END as bit) Zakupowy,
{DataKorygowanego},
{NumerKorygowanego}, 
dok.DataWydaniaWystawienia DataOryginalu,
0.0 NettoPrzedKorekta, 0.0 BruttoPrzedKorekta, 0.0 VatPrzedKorekta,
0.0 NettoPoKorekcie, 0.0 BruttoPoKorekcie, 0.0 VatPoKorekcie
FROM ModelDanychContainer.Dokumenty dok 
LEFT JOIN ModelDanychContainer.Dokumenty pow ON pow.Id=dok.Dokument_DokumentPowiazany_Id
LEFT JOIN ModelDanychContainer.PodmiotHistorie ph ON dok.PodmiotWybranyId = ph.Id 
INNER JOIN ModelDanychContainer.Konfiguracje kf ON dok.KonfiguracjaId=kf.Id 
LEFT JOIN ModelDanychContainer.AdresHistorie ah ON dok.AdresKontrahentaId = ah.Id 
INNER JOIN ModelDanychContainer.Waluty wa ON dok.Dokument_Waluta_Id=wa.Id 
{DaneKorygowanego}
WHERE dok.Id=@id
";
        private static readonly string _sqlNaglowekMagazynowy_pre12 = _sqlNaglowekMagazynowy_base
            .Replace("{Korekta}", @"CAST(CASE WHEN dok.DokumentKPW_SposobKorygowania=1 THEN 1 
WHEN dok.DokumentKRW_SposobKorygowania=1 THEN 1 
WHEN dok.DokumentKPZ_SposobKorygowania=1 THEN 1 
WHEN dok.DokumentKWZ_SposobKorygowania=1 THEN 1 
ELSE 0 END as bit) Korekta")
            .Replace("{DataKorygowanego}", @"CASE WHEN NOT(dok.DokumentKPW_DaneKorygowanego_DataKorygowanego IS NULL) THEN dok.DokumentKPW_DaneKorygowanego_DataKorygowanego 
WHEN NOT(dok.DokumentKRW_DaneKorygowanego_DataKorygowanego IS NULL) THEN dok.DokumentKRW_DaneKorygowanego_DataKorygowanego 
WHEN NOT(dok.DokumentKPZ_DaneKorygowanego_DataKorygowanego IS NULL) THEN dok.DokumentKPZ_DaneKorygowanego_DataKorygowanego 
WHEN NOT(dok.DokumentKWZ_DaneKorygowanego_DataKorygowanego IS NULL) THEN dok.DokumentKWZ_DaneKorygowanego_DataKorygowanego 
END DataWystawieniaKorygowanego")
            .Replace("{NumerKorygowanego}", @"CASE WHEN NOT(dok.DokumentKPW_DaneKorygowanego_NumerKorygowanego IS NULL) THEN dok.DokumentKPW_DaneKorygowanego_NumerKorygowanego 
WHEN NOT(dok.DokumentKRW_DaneKorygowanego_NumerKorygowanego IS NULL) THEN dok.DokumentKRW_DaneKorygowanego_NumerKorygowanego 
WHEN NOT(dok.DokumentKPZ_DaneKorygowanego_NumerKorygowanego IS NULL) THEN dok.DokumentKPZ_DaneKorygowanego_NumerKorygowanego 
WHEN NOT(dok.DokumentKWZ_DaneKorygowanego_NumerKorygowanego IS NULL) THEN dok.DokumentKWZ_DaneKorygowanego_NumerKorygowanego 
END NumerKorygowanego")
             .Replace("{DaneKorygowanego}", string.Empty);
        private static readonly string _sqlNaglowekMagazynowy_v12 = _sqlNaglowekMagazynowy_base
            .Replace("{Korekta}", "CAST(CASE dok.SposobKorygowania WHEN 1 THEN 1 ELSE 0 END as bit) Korekta")
            .Replace("{NumerKorygowanego}", @"kor.NumerKorygowanego")
            .Replace("{DataKorygowanego}", @"kor.DataKorygowanego")
            .Replace("{DaneKorygowanego}", @"LEFT JOIN ModelDanychContainer.DaneKorygowanychDokumentow kor ON kor.Id=dok.Id")
            .Replace(".DokumentHandlowy_DataSprzedazy", ".DataSprzedazy");
        private static readonly string[] _sqlNaglowekMagazynowy = new string[] { _sqlNaglowekMagazynowy_pre12, _sqlNaglowekMagazynowy_pre12, _sqlNaglowekMagazynowy_v12 };

        private static readonly string _sqlPozycje_base = @"
SELECT poz.Id, poz.LP, poz.Ilosc, jm.Symbol Jm, jma.Precyzja, ah.Nazwa, ah.Symbol, ah.PKWiU, poz.Opis, 
poz.Cena_NettoPoRabacie Cena, poz.Cena_RabatProcent RabatProcent, sv.Symbol Vat, 
poz.Wartosc_NettoPoRabacie WartoscNetto, poz.Wartosc_VatPoRabacie WartoscVat, poz.Wartosc_BruttoPoRabacie WartoscBrutto, prz.Nazwa PrzyczynaKorekty 
FROM ModelDanychContainer.PozycjeDokumentu poz 
INNER JOIN ModelDanychContainer.AsortymentyHistoria ah ON poz.AsortymentWybranyId=ah.Id 
INNER JOIN ModelDanychContainer.JednostkiMiarAsortymentow jma ON poz.JednostkaMiaryAsId=jma.Id 
INNER JOIN ModelDanychContainer.JednostkiMiar jm ON jma.JednostkaMiary_Id=jm.Id 
INNER JOIN ModelDanychContainer.StawkiVat sv ON poz.StawkaVatId=sv.Id 
{JOIN_PrzyczynaKorekty} 
WHERE poz.PozycjaDokumentu_Dokument_Id=@id ORDER BY poz.LP
";
        private static readonly string _sqlPozycje_pre11 = _sqlPozycje_base.Replace("{JOIN_PrzyczynaKorekty} ", 
@"LEFT JOIN ModelDanychContainer.PrzyczynyKorekt prz ON poz.PozycjaKorekty_PrzyczynaKorekty_Id=prz.Id");
        private static readonly string _sqlPozycje_v11 = _sqlPozycje_base
            .Replace("PozycjaDokumentu_Dokument_Id", "Dokument_Id")
            .Replace("{JOIN_PrzyczynaKorekty} ", 
@"LEFT JOIN ModelDanychContainer.PozycjeDokumentu_PozycjaKorekty pozk ON pozk.Id=poz.Id
LEFT JOIN ModelDanychContainer.PrzyczynyKorekt prz ON pozk.PrzyczynaKorekty_Id=prz.Id");
        private static readonly string[] _sqlPozycje = new string[] { _sqlPozycje_pre11, _sqlPozycje_v11, _sqlPozycje_v11 };

        private static readonly string _sqlPozycjeZwrotu_base = @"
SELECT poz.Id, ROW_NUMBER() OVER (ORDER BY poz.LP) LP, 
ABS(poz.PozycjaKorekty_IloscRoznica) Ilosc, jm.Symbol Jm, jma.Precyzja, ah.Nazwa, ah.Symbol, ah.PKWiU, poz.Opis, 
poz.PozycjaKorekty_CenaOryginalna_NettoPoRabacie Cena, poz.PozycjaKorekty_CenaOryginalna_RabatProcent RabatProcent, sv.Symbol Vat, 
ABS(poz.PozycjaKorekty_WartoscRoznica_NettoPoRabacie) WartoscNetto, ABS(poz.PozycjaKorekty_WartoscRoznica_VatPoRabacie) WartoscVat, 
ABS(poz.PozycjaKorekty_WartoscRoznica_BruttoPoRabacie) WartoscBrutto, prz.Nazwa PrzyczynaKorekty
FROM ModelDanychContainer.PozycjeDokumentu poz 
INNER JOIN ModelDanychContainer.AsortymentyHistoria ah ON poz.AsortymentWybranyId=ah.Id 
INNER JOIN ModelDanychContainer.JednostkiMiarAsortymentow jma ON poz.JednostkaMiaryAsId=jma.Id 
INNER JOIN ModelDanychContainer.JednostkiMiar jm ON jma.JednostkaMiary_Id=jm.Id 
INNER JOIN ModelDanychContainer.StawkiVat sv ON poz.StawkaVatId=sv.Id 
{JOIN_PrzyczynaKorekty}
WHERE poz.PozycjaDokumentu_Dokument_Id=@id ORDER BY poz.LP
";
        private static readonly string _sqlPozycjeZwrotu_pre11 = _sqlPozycjeZwrotu_base.Replace("{JOIN_PrzyczynaKorekty}",
 @"LEFT JOIN ModelDanychContainer.PrzyczynyKorekt prz ON poz.PozycjaKorekty_PrzyczynaKorekty_Id=prz.Id");
        private static readonly string _sqlPozycjeZwrotu_v11 = _sqlPozycjeZwrotu_base
            .Replace("PozycjaDokumentu_Dokument_Id", "Dokument_Id")
            .Replace("poz.PozycjaKorekty_", "pozk.")
            .Replace("{JOIN_PrzyczynaKorekty}",
@"LEFT JOIN ModelDanychContainer.PozycjeDokumentu_PozycjaKorekty pozk ON pozk.Id=poz.Id
LEFT JOIN ModelDanychContainer.PrzyczynyKorekt prz ON pozk.PrzyczynaKorekty_Id=prz.Id");
        private static readonly string[] _sqlPozycjeZwrotu = new string[] { _sqlPozycjeZwrotu_pre11, _sqlPozycjeZwrotu_v11, _sqlPozycjeZwrotu_v11 };

        private static readonly string _sqlPozycjeKorekty_base = @"
DECLARE @doki AS TABLE
	(
	id		int,
	nr		int
	)
declare @iddok int = @id;
DECLARE @nrid INT = 0;
DECLARE @kds_popId INT, @kdz_nastId INT, @kpw_nastId INT, @kpz_nastId INT, @krw_nastId INT, @kwz_nastId INT, @popId INT
DECLARE @kds_dokId INT, @kdz_dokId INT, @kpw_dokId INT, @kpz_dokId INT, @krw_dokId INT, @kwz_dokId INT, @dokId INT
SET @popId=NULL
SET @dokId=NULL
WHILE NOT (@iddok IS NULL)
BEGIN
	INSERT INTO @doki VALUES (@iddok, @nrid);
	SET @nrid=@nrid-1;
	SELECT @kds_popId=DokumentKDS_PoprzedniaKorekta_Id, 
		@kdz_nastId=DokumentKDZ_NastepnaKorekta_Id, 
		@kpw_nastId=DokumentKPW_NastepnaKorekta_Id, 
		@kpz_nastId=DokumentKPZ_NastepnaKorekta_Id, 
		@krw_nastId=DokumentKRW_NastepnaKorekta_Id, 
		@kwz_nastId=DokumentKWZ_NastepnaKorekta_Id,
		@kds_dokId=DokumentKDS_DokumentKorygowany_Id, 
		@kdz_dokId=DokumentKDZ_DokumentKorygowany_Id, 
		@kpw_dokId=DokumentKPW_DokumentKorygowany_Id, 
		@kpz_dokId=DokumentKPZ_DokumentKorygowany_Id, 
		@krw_dokId=DokumentKRW_DokumentKorygowany_Id, 
		@kwz_dokId=DokumentKWZ_DokumentKorygowany_Id
		FROM ModelDanychContainer.Dokumenty WHERE Id=@iddok
	SET @popId=CASE
		WHEN NOT(@kds_popId IS NULL) THEN @kds_popId 
		WHEN NOT(@kdz_nastId IS NULL) THEN @kdz_nastId 
		WHEN NOT(@kpw_nastId IS NULL) THEN @kpw_nastId 
		WHEN NOT(@kpz_nastId IS NULL) THEN @kpz_nastId 
		WHEN NOT(@krw_nastId IS NULL) THEN @krw_nastId 
		WHEN NOT(@kwz_nastId IS NULL) THEN @kwz_nastId 
		ELSE NULL
		END
	SET @dokId=CASE
		WHEN NOT(@kds_dokId IS NULL) THEN @kds_dokId 
		WHEN NOT(@kdz_dokId IS NULL) THEN @kdz_dokId 
		WHEN NOT(@kpw_dokId IS NULL) THEN @kpw_dokId 
		WHEN NOT(@kpz_dokId IS NULL) THEN @kpz_dokId 
		WHEN NOT(@krw_dokId IS NULL) THEN @krw_dokId 
		WHEN NOT(@kwz_dokId IS NULL) THEN @kwz_dokId 
		ELSE NULL
		END
	SET @iddok = CASE
		WHEN NOT (@popId IS NULL) THEN @popId
		ELSE @dokId END
END

SELECT poz.PozycjaDokumentu_Dokument_Id, poz.Id, poz.LP, poz.Ilosc, jm.Symbol Jm, jma.Precyzja, ah.Nazwa, ah.Symbol, ah.PKWiU, poz.Opis, prz.Nazwa PrzyczynaKorekty, 
poz.Cena_NettoPoRabacie Cena, poz.Cena_RabatProcent RabatProcent, sv.Symbol Vat, 
poz.Wartosc_NettoPoRabacie WartoscNetto, poz.Wartosc_VatPoRabacie WartoscVat, poz.Wartosc_BruttoPoRabacie WartoscBrutto, 
CASE WHEN poz.PozycjaKorekty_IloscOryginalna IS NULL THEN poz.Ilosc ELSE poz.PozycjaKorekty_IloscOryginalna END IloscPrzedKorekta, 
CASE WHEN jmo.Precyzja IS NULL THEN jma.Precyzja ELSE jmo.Precyzja END PrecyzjaPrzedKorekta, 
CASE WHEN jmp.Symbol IS NULL THEN jm.Symbol ELSE jmp.Symbol END JmPrzedKorekta,
CASE WHEN poz.PozycjaKorekty_IloscRoznica IS NULL THEN 0.0 ELSE poz.PozycjaKorekty_IloscRoznica END IloscKorekta, 
CASE WHEN jmr.Precyzja IS NULL THEN jma.Precyzja ELSE jmr.Precyzja END PrecyzjaKorekta, 
CASE WHEN jmra.Symbol IS NULL THEN jm.Symbol ELSE jmra.Symbol END JmKorekta,
poz.Ilosc IloscPoKorekcie, jma.Precyzja PrecyzjaPoKorekcie, jm.Symbol JmPoKorekcie,
CASE WHEN poz.PozycjaKorekty_CenaOryginalna_RabatProcent IS NULL THEN poz.Cena_RabatProcent ELSE poz.PozycjaKorekty_CenaOryginalna_RabatProcent END RabatProcentPrzedKorekta, 
poz.Cena_RabatProcent RabatProcentPoKorekcie, 
CASE WHEN poz.PozycjaKorekty_CenaOryginalna_RabatWartosc IS NULL THEN poz.Cena_RabatWartosc ELSE poz.PozycjaKorekty_CenaOryginalna_RabatWartosc END RabatWartoscPrzedKorekta, 
poz.Cena_RabatWartosc RabatWartoscPoKorekcie, 
CASE WHEN poz.PozycjaKorekty_CenaOryginalna_RodzajRabatu IS NULL THEN poz.Cena_RodzajRabatu ELSE poz.PozycjaKorekty_CenaOryginalna_RodzajRabatu END RodzajRabatuPrzedKorekta, 
poz.Cena_RodzajRabatu RodzajRabatuPoKorekcie, 
CASE WHEN poz.PozycjaKorekty_CenaOryginalna_NettoPrzedRabatem IS NULL THEN  CASE dok.WyliczenieVAT WHEN 1 THEN poz.Cena_NettoPrzedRabatem ELSE poz.Cena_BruttoPrzedRabatem END
ELSE CASE dok.WyliczenieVAT WHEN 1 THEN poz.PozycjaKorekty_CenaOryginalna_NettoPrzedRabatem ELSE poz.PozycjaKorekty_CenaOryginalna_BruttoPrzedRabatem END END CenaPrzedKorekta, 
CASE dok.WyliczenieVAT WHEN 1 THEN poz.Cena_NettoPrzedRabatem ELSE poz.Cena_BruttoPrzedRabatem END CenaPoKorekcie, 
CASE WHEN poz.PozycjaKorekty_CenaRoznica_NettoPrzedRabatem IS NULL THEN 0.0 ELSE
CASE dok.WyliczenieVAT WHEN 1 THEN poz.PozycjaKorekty_CenaRoznica_NettoPrzedRabatem ELSE poz.PozycjaKorekty_CenaRoznica_BruttoPrzedRabatem END END CenaKorekta,
CASE WHEN svo.Symbol IS NULL THEN sv.Symbol ELSE svo.Symbol END VatPrzedKorekta, sv.Symbol VatPoKorekcie,
CASE WHEN poz.PozycjaKorekty_WartoscOryginalna_NettoPoRabacie IS NULL THEN poz.Wartosc_NettoPoRabacie ELSE poz.PozycjaKorekty_WartoscOryginalna_NettoPoRabacie END WartoscNettoPrzedKorekta, 
CASE WHEN poz.PozycjaKorekty_WartoscRoznica_NettoPoRabacie IS NULL THEN 0.0 ELSE poz.PozycjaKorekty_WartoscRoznica_NettoPoRabacie END WartoscNettoKorekta, 
poz.Wartosc_NettoPoRabacie WartoscNettoPoKorekcie,
CASE WHEN poz.PozycjaKorekty_WartoscOryginalna_VatPoRabacie IS NULL THEN poz.Wartosc_VatPoRabacie ELSE poz.PozycjaKorekty_WartoscOryginalna_VatPoRabacie END WartoscVatPrzedKorekta, 
CASE WHEN poz.PozycjaKorekty_WartoscRoznica_VatPoRabacie IS NULL THEN 0.0 ELSE poz.PozycjaKorekty_WartoscRoznica_VatPoRabacie END WartoscVatKorekta, 
poz.Wartosc_VatPoRabacie WartoscVatPoKorekcie,
CASE WHEN poz.PozycjaKorekty_WartoscOryginalna_BruttoPoRabacie IS NULL THEN poz.Wartosc_BruttoPoRabacie ELSE poz.PozycjaKorekty_WartoscOryginalna_BruttoPoRabacie END WartoscBruttoPrzedKorekta, 
CASE WHEN poz.PozycjaKorekty_WartoscRoznica_BruttoPoRabacie IS NULL THEN 0.0 ELSE poz.PozycjaKorekty_WartoscRoznica_BruttoPoRabacie END WartoscBruttoKorekta, 
poz.Wartosc_BruttoPoRabacie WartoscBruttoPoKorekcie
, poz.PozycjaDokumentu_PozycjaZrodlowa_Id, poz.PozycjaDokumentu_PozycjaKorygujaca_Id

FROM ModelDanychContainer.PozycjeDokumentu poz 
INNER JOIN ModelDanychContainer.Dokumenty dok ON poz.PozycjaDokumentu_Dokument_Id=dok.Id 
INNER JOIN ModelDanychContainer.AsortymentyHistoria ah ON poz.AsortymentWybranyId=ah.Id 
INNER JOIN ModelDanychContainer.JednostkiMiarAsortymentow jma ON poz.JednostkaMiaryAsId=jma.Id 
INNER JOIN ModelDanychContainer.JednostkiMiar jm ON jma.JednostkaMiary_Id=jm.Id 
INNER JOIN ModelDanychContainer.StawkiVat sv ON poz.StawkaVatId=sv.Id 
{JOIN_PrzyczynaKorekty} 
LEFT JOIN ModelDanychContainer.StawkiVat svo ON poz.PozycjaKorekty_StawkaVatOryginalnaId=svo.Id 
LEFT JOIN ModelDanychContainer.PozycjeDokumentu oryg ON oryg.Id=poz.PozycjaKorekty_PozycjaOryginalnaId
LEFT JOIN ModelDanychContainer.JednostkiMiarAsortymentow jmo ON jmo.Id=poz.PozycjaKorekty_JednostkaMiaryAsOryginalnaId
LEFT JOIN ModelDanychContainer.JednostkiMiar jmp ON jmp.Id=jmo.JednostkaMiary_Id 
LEFT JOIN ModelDanychContainer.JednostkiMiarAsortymentow jmr ON jmr.Id=poz.PozycjaKorekty_JednostkaMiaryAsRoznicyId 
LEFT JOIN ModelDanychContainer.JednostkiMiar jmra ON jmra.Id=jmr.JednostkaMiary_Id 
WHERE poz.PozycjaDokumentu_Dokument_Id IN (SELECT id FROM @doki) 
--AND (NOT EXISTS(SELECT Id, LP FROM ModelDanychContainer.PozycjeDokumentu WHERE PozycjaDokumentu_Dokument_Id=@id AND LP=poz.LP) OR poz.PozycjaDokumentu_Dokument_Id=@id )
AND NOT EXISTS(SELECT Id FROM ModelDanychContainer.PozycjeDokumentu WHERE PozycjaDokumentu_Dokument_Id IN (SELECT id FROM @doki) AND Id=poz.PozycjaDokumentu_PozycjaKorygujaca_Id)
ORDER BY poz.LP
SELECT id FROM @doki
";
        private static readonly string _sqlPozycjeKorekty_pre11 = _sqlPozycjeKorekty_base
            .Replace("{JOIN_PrzyczynaKorekty}", "LEFT JOIN ModelDanychContainer.PrzyczynyKorekt prz ON poz.PozycjaKorekty_PrzyczynaKorekty_Id=prz.Id");
        private static readonly string _sqlPozycjeKorekty_v11 = _sqlPozycjeKorekty_base
            .Replace("PozycjaDokumentu_Dokument_Id", "Dokument_Id")
            .Replace("poz.PozycjaKorekty_", "pozk.")
            .Replace("poz.PozycjaDokumentu_", "poz.")
            .Replace("{JOIN_PrzyczynaKorekty}", @"LEFT JOIN ModelDanychContainer.PozycjeDokumentu_PozycjaKorekty pozk ON pozk.Id=poz.Id
LEFT JOIN ModelDanychContainer.PrzyczynyKorekt prz ON pozk.PrzyczynaKorekty_Id=prz.Id");
        private static readonly string _sqlPozycjeKorekty_v12 = _sqlPozycjeKorekty_v11
            .Replace("DokumentKDS_DokumentKorygowany_Id", "DokumentKorygowanyId")
            .Replace("DokumentKDZ_DokumentKorygowany_Id", "DokumentKorygowanyId")
            .Replace("DokumentKPW_DokumentKorygowany_Id", "DokumentKorygowanyId")
            .Replace("DokumentKPZ_DokumentKorygowany_Id", "DokumentKorygowanyId")
            .Replace("DokumentKRW_DokumentKorygowany_Id", "DokumentKorygowanyId")
            .Replace("DokumentKWZ_DokumentKorygowany_Id", "DokumentKorygowanyId");
        private static readonly string[] _sqlPozycjeKorekty = new string[] { _sqlPozycjeKorekty_pre11, _sqlPozycjeKorekty_v11, _sqlPozycjeKorekty_v12 };

        private static readonly string _sqlTabelaVat_base = @"
SELECT poz.Id, vat.Nazwa, poz.WartoscNetto, poz.WartoscVat, poz.WartoscBrutto FROM ModelDanychContainer.TabeleVat poz 
INNER JOIN ModelDanychContainer.StawkiVat vat ON poz.StawkaVat_Id=vat.Id 
WHERE poz.Dokument_Id=@id ORDER BY vat.NumerPorzadkowy
";
        private static readonly string[] _sqlTabelaVat = new string[] { _sqlTabelaVat_base, _sqlTabelaVat_base, _sqlTabelaVat_base };

        private static readonly string _sqlTabelaVatKorekty_pre12 = @"
DECLARE @wynik AS TABLE
	(
	nr		int,
	symbol	nvarchar(5),
	Nazwa	nvarchar(64),
	NettoKorekta	decimal(18,6),
	VatKorekta		decimal(18,6),
	BruttoKorekta	decimal(18,6),
	NettoPrzedKorekta	decimal(18,6),
	VatPrzedKorekta		decimal(18,6),
	BruttoPrzedKorekta	decimal(18,6),
	NettoPoKorekcie	decimal(18,6),
	VatPoKorekcie		decimal(18,6),
	BruttoPoKorekcie	decimal(18,6),
	modified	bit
	)
DECLARE @doki AS TABLE
	(
	id		int,
	nr		int
	)

DECLARE @nrid INT = 0;
DECLARE @kds_popId INT, @kdz_nastId INT, @kpw_nastId INT, @kpz_nastId INT, @krw_nastId INT, @kwz_nastId INT, @popId INT
DECLARE @kds_dokId INT, @kdz_dokId INT, @kpw_dokId INT, @kpz_dokId INT, @krw_dokId INT, @kwz_dokId INT, @dokId INT
SET @popId=NULL
SET @dokId=NULL
WHILE NOT (@id IS NULL)
BEGIN
	
	INSERT INTO @doki VALUES (@id, @nrid);
	SET @nrid=@nrid-1;

	SELECT @kds_popId=DokumentKDS_PoprzedniaKorekta_Id, 
		@kdz_nastId=DokumentKDZ_NastepnaKorekta_Id, 
		@kpw_nastId=DokumentKPW_NastepnaKorekta_Id, 
		@kpz_nastId=DokumentKPZ_NastepnaKorekta_Id, 
		@krw_nastId=DokumentKRW_NastepnaKorekta_Id, 
		@kwz_nastId=DokumentKWZ_NastepnaKorekta_Id,
		@kds_dokId=DokumentKDS_DokumentKorygowany_Id, 
		@kdz_dokId=DokumentKDZ_DokumentKorygowany_Id, 
		@kpw_dokId=DokumentKPW_DokumentKorygowany_Id, 
		@kpz_dokId=DokumentKPZ_DokumentKorygowany_Id, 
		@krw_dokId=DokumentKRW_DokumentKorygowany_Id, 
		@kwz_dokId=DokumentKWZ_DokumentKorygowany_Id
		FROM ModelDanychContainer.Dokumenty WHERE Id=@id
	SET @popId=CASE
		WHEN NOT(@kds_popId IS NULL) THEN @kds_popId 
		WHEN NOT(@kdz_nastId IS NULL) THEN @kdz_nastId 
		WHEN NOT(@kpw_nastId IS NULL) THEN @kpw_nastId 
		WHEN NOT(@kpz_nastId IS NULL) THEN @kpz_nastId 
		WHEN NOT(@krw_nastId IS NULL) THEN @krw_nastId 
		WHEN NOT(@kwz_nastId IS NULL) THEN @kwz_nastId 
		ELSE NULL
		END
	SET @dokId=CASE
		WHEN NOT(@kds_dokId IS NULL) THEN @kds_dokId 
		WHEN NOT(@kdz_dokId IS NULL) THEN @kdz_dokId 
		WHEN NOT(@kpw_dokId IS NULL) THEN @kpw_dokId 
		WHEN NOT(@kpz_dokId IS NULL) THEN @kpz_dokId 
		WHEN NOT(@krw_dokId IS NULL) THEN @krw_dokId 
		WHEN NOT(@kwz_dokId IS NULL) THEN @kwz_dokId 
		ELSE NULL
		END

	SET @id = CASE
		WHEN NOT (@popId IS NULL) THEN @popId
		ELSE @dokId END

END

DECLARE dokums SCROLL CURSOR FOR 
	SELECT id FROM @doki ORDER BY nr;
OPEN dokums;
FETCH NEXT FROM dokums INTO @id;
WHILE @@FETCH_STATUS=0
BEGIN
	UPDATE @wynik SET modified=0;

	DECLARE @nr		int;
	DECLARE @symbol	nvarchar(5);
	DECLARE @nazwa	nvarchar(64);
	DECLARE @netto	decimal(18,6);
	DECLARE @vat	decimal(18,6);
	DECLARE @brutto	decimal(18,6);

	DECLARE dokumenty SCROLL CURSOR FOR 
		SELECT vat.NumerPorzadkowy, vat.Symbol, vat.Nazwa, poz.WartoscNetto, poz.WartoscVat, poz.WartoscBrutto
		FROM ModelDanychContainer.TabeleVat poz 
		INNER JOIN ModelDanychContainer.StawkiVat vat ON poz.StawkaVat_Id=vat.Id 
		WHERE poz.Dokument_Id=@id
	OPEN dokumenty;
	FETCH NEXT FROM dokumenty INTO @nr, @symbol, @nazwa, @netto, @vat, @brutto;
	WHILE @@FETCH_STATUS=0
		BEGIN
		IF NOT EXISTS(SELECT symbol FROM @wynik WHERE symbol=@symbol)
			INSERT INTO @wynik VALUES(@nr, @symbol, @nazwa, @netto, @vat, @brutto, 0, 0, 0, @netto, @vat, @brutto, 1);
		ELSE
			UPDATE @wynik SET modified=1,
			NettoKorekta=@netto, VatKorekta=@vat, BruttoKorekta=@brutto,
			NettoPrzedKorekta=NettoPoKorekcie, VatPrzedKorekta=VatPoKorekcie, BruttoPrzedKorekta=BruttoPoKorekcie,
			NettoPoKorekcie=@netto+NettoPoKorekcie, VatPoKorekcie=@vat+VatPoKorekcie, BruttoPoKorekcie=@brutto+BruttoPoKorekcie
			WHERE symbol=@symbol;
		FETCH NEXT FROM dokumenty INTO @nr, @symbol, @nazwa, @netto, @vat, @brutto;
		END
	CLOSE dokumenty   
	DEALLOCATE dokumenty
	FETCH NEXT FROM dokums INTO @id;
END
CLOSE dokums
DEALLOCATE dokums

UPDATE @wynik SET
NettoKorekta=0, VatKorekta=0, BruttoKorekta=0,
NettoPrzedKorekta=NettoPoKorekcie, VatPrzedKorekta=VatPoKorekcie, BruttoPrzedKorekta=BruttoPoKorekcie
--NettoPoKorekcie=@netto+NettoPoKorekcie, VatPoKorekcie=@vat+VatPoKorekcie, BruttoPoKorekcie=@brutto+BruttoPoKorekcie
WHERE modified=0;

            SELECT * FROM @wynik ORDER BY nr;";
        private static readonly string _sqlTabelaVatKorekty_v12 = _sqlTabelaVatKorekty_pre12
    .Replace("DokumentKDS_DokumentKorygowany_Id", "DokumentKorygowanyId")
    .Replace("DokumentKDZ_DokumentKorygowany_Id", "DokumentKorygowanyId")
    .Replace("DokumentKPW_DokumentKorygowany_Id", "DokumentKorygowanyId")
    .Replace("DokumentKPZ_DokumentKorygowany_Id", "DokumentKorygowanyId")
    .Replace("DokumentKRW_DokumentKorygowany_Id", "DokumentKorygowanyId")
    .Replace("DokumentKWZ_DokumentKorygowany_Id", "DokumentKorygowanyId");
        private static readonly string[] _sqlTabelaVatKorekty = new string[] { _sqlTabelaVatKorekty_pre12, _sqlTabelaVatKorekty_pre12, _sqlTabelaVatKorekty_v12 };

        private static readonly string _sqlPlatnosci = @"
SELECT fp.Nazwa, pl.KwotaPlatnosci, tp.Gotowkowy|tp.Cesyjny|tp.Kasowy zaplacone
FROM ModelDanychContainer.PlatnosciDokumentow pl 
INNER JOIN ModelDanychContainer.FormyPlatnosci fp ON fp.Id=pl.FormaPlatnosci_Id 
INNER JOIN ModelDanychContainer.TypyPlatnosci tp ON tp.Id=fp.TypPlatnosci_Id
WHERE pl.DokumentId=@id
";

        #endregion

        #region Konstruktor

        public DaneArchiwalneInsertNexo()
        {
            _dostawca = new Producent();
        }

        #endregion

        #region IFunkcjaPobieraniaDanychArchiwalnych

        public XElement DomyslnaKonfiguracja
        {
            get
            {
                return UtworzKonfiguracje("Serwer", "BazaDanych", "Login", "Haslo");
            }
        }

        public string DefinicjaWidokuKonfiguracji
        {
            get
            {
                return null;
            }
        }

        public bool CzyTypDanychObslugiwany(TypDanych typDanych)
        {
            return _obslugiwaneTypy.Contains(typDanych);
        }

        public object PobierzDane(XElement konfiguracja, TypDanych typDanych)
        {
            switch (typDanych)
            {
                case TypDanych.FakturySprzedazy:
                    return PobierzDokumentySprzedazyWszystkie(konfiguracja);
                case TypDanych.FakturyZakupu:
                    return PobierzDokumentyZakupu(konfiguracja);
                case TypDanych.KorektySprzedazy:
                    return PobierzKorektySprzedazy(konfiguracja);
                case TypDanych.SprzedazDetaliczna:
                    return PobierzParagony(konfiguracja);
                case TypDanych.KorektyZakupu:
                    return PobierzKorektyZakupu(konfiguracja);
                case TypDanych.PrzyjeciaMagazynowe:
                    return PobierzPrzyjeciaMagazynowe(konfiguracja);
                case TypDanych.WydaniaMagazynowe:
                    return PobierzWydaniaMagazynowe(konfiguracja);
                case TypDanych.ZamowieniaDoDostawcow:
                    return PobierzZamowieniaDoDostawcow(konfiguracja);
                case TypDanych.ZamowieniaOdKlientow:
                    return PobierzZamowieniaOdKlientowNew(konfiguracja);
                case TypDanych.ZwrotyDetaliczne:
                    return PobierzZwrotyDoParagonu(konfiguracja);
                case TypDanych.Kategorie:
                    return PobierzKategorie(konfiguracja);
                case TypDanych.Magazyny:
                    return PobierzMagazyny(konfiguracja);
            }
            return null;
        }

        public string PobierzHTMLPodgladu(XElement konfiguracja, TypPodgladu typPodgladu, int idObiektu)
        {
            return PobierzHTMLPodgladuObiektu(konfiguracja, typPodgladu, idObiektu);
        }

        public IEnumerable<TypPodgladu> PodajTypyPodgladu(XElement konfiguracja, TypDanych typDanych)
        {
            yield return new TypPodgladu()
            {
                Id = 0,
                Nazwa = "podgląd dokumentu",
                TypDanych = typDanych,
            };
        }

        public IEnumerable<BladKonfiguracji> SprawdzKonfiguracje(XElement konfiguracja)
        {
            List<BladKonfiguracji> bledy = new List<BladKonfiguracji>();
            var elem = konfiguracja.Element(_nazwaWezlaAutentykacji);
            if (elem == null || !(elem.Value == _autentykacjaMixed || elem.Value == _autentykacjaWindows))
            {
                bledy.Add(new BladKonfiguracji
                {
                    NazwaElementu = _nazwaWezlaAutentykacji,
                    TrescBledu = string.Format("Dozwolone wartości to {0} lub {1}",
                        _autentykacjaMixed, _autentykacjaWindows)
                });
            }

            if (bledy.Count() == 0)
            {
                try
                {
                    if (!BazaInsertNexo(konfiguracja))
                        bledy.Add(new BladKonfiguracji { NazwaElementu = _nazwaWezlaBazy, TrescBledu = "Baza danych nie jest bazą programu InsERT nexo." });
                }
                catch
                {
                    bledy.Add(new BladKonfiguracji { TrescBledu = "Nie udało się nawiązać połączenia z bazą danych." });
                }
            }

            return bledy;
        }

        #endregion

        #region IFunkcja

        public Guid Identyfikator
        {
            get { return Guid.Parse("6900037B-E59C-416D-A5B7-D964C53D545D"); }
        }

        public string Nazwa
        {
            get { return "Dane archiwalne z InsERT nexo"; }
        }

        public string Opis
        {
            get { return "Dane archiwalne z InsERT nexo"; }
        }

        private IDostawcaPluginow _dostawca;
        public IDostawcaPluginow Dostawca
        {
            get { return _dostawca; }
        }

        #endregion

        #region Metody pomocnicze

        enum TypObiektuPodgladu
        {
            Nieznany, Dokument
        }

        private static TypObiektuPodgladu PodajTypPodgladu(TypDanych typDanych)
        {
            switch (typDanych)
            {
                case TypDanych.FakturySprzedazy:
                case TypDanych.FakturyZakupu:
                case TypDanych.KorektySprzedazy:
                case TypDanych.KorektyZakupu:
                case TypDanych.PrzyjeciaMagazynowe:
                case TypDanych.SprzedazDetaliczna:
                case TypDanych.WydaniaMagazynowe:
                case TypDanych.ZamowieniaDoDostawcow:
                case TypDanych.ZamowieniaOdKlientow:
                case TypDanych.ZwrotyDetaliczne:
                    return TypObiektuPodgladu.Dokument;
                default:
                    return TypObiektuPodgladu.Nieznany;
            }
        }

        private static IQueryable<PrzyjecieMagazynowe> PobierzPrzyjeciaMagazynowe(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = UtworzPolecenie(conn, _sqlWersja11))
            {
                conn.Open();
                int verPo11 = (int)cmd.ExecuteScalar();
                cmd.CommandText = _sqlDokumentyPZ[verPo11];
                using (var reader = cmd.ExecuteReader())
                {
                    List<PrzyjecieMagazynowe> lista = new List<PrzyjecieMagazynowe>();
                    while (reader.Read())
                    {
                        lista.Add(new PrzyjecieMagazynowe
                        {
                            Id = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            DataWystawienia = reader.GetDateTime(2),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(3),
                            PodtypRaw = reader.GetInt32(4),
                            NumerPelny = reader.GetString(5),
                            NumerOryginalu = reader.GetString(6),
                            Kontrahent = !reader.IsDBNull(7) ? reader.GetString(7) : null,
                            Wartosc = reader.GetDecimal(8),
                            Koszt = reader.GetDecimal(9),
                            NumerDokumentuPowiazanego = !reader.IsDBNull(10) ? reader.GetString(10) : null,
                            Numer = reader.GetInt32(11),
                            Symbol = reader.GetString(12)
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private static IQueryable<WydanieMagazynowe> PobierzWydaniaMagazynowe(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = UtworzPolecenie(conn, _sqlWersja11))
            {
                conn.Open();
                int verPo11 = (int)cmd.ExecuteScalar();
                cmd.CommandText = _sqlDokumentyWZ[verPo11];
                using (var reader = cmd.ExecuteReader())
                {
                    List<WydanieMagazynowe> lista = new List<WydanieMagazynowe>();
                    while (reader.Read())
                    {
                        lista.Add(new WydanieMagazynowe()
                        {
                            Id = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            DataWystawienia = reader.GetDateTime(2),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(3),
                            PodtypRaw = reader.GetInt32(4),
                            NumerPelny = reader.GetString(5),
                            Kontrahent = !reader.IsDBNull(6) ? reader.GetString(6) : null,
                            Wartosc = reader.GetDecimal(7),
                            Koszt = reader.GetDecimal(8),
                            NumerDokumentuPowiazanego = !reader.IsDBNull(9) ? reader.GetString(9) : null,
                            Numer = reader.GetInt32(10),
                            Symbol = reader.GetString(11)
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private static IQueryable<ZamowienieDoDostawcy> PobierzZamowieniaDoDostawcow(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = UtworzPolecenie(conn, _sqlWersja11))
            {
                conn.Open();
                int verPo11 = (int)cmd.ExecuteScalar();
                cmd.CommandText = _sqlDokumentyZD[verPo11];
                using (var reader = cmd.ExecuteReader())
                {
                    List<ZamowienieDoDostawcy> lista = new List<ZamowienieDoDostawcy>();
                    while (reader.Read())
                    {
                        lista.Add(new ZamowienieDoDostawcy
                        {
                            Id = reader.GetInt32(0),
                            StatusRealizacji = (StatusRealizacji)reader.GetInt32(1),
                            DataWystawienia = reader.GetDateTime(2),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(3),
                            PodtypRaw = reader.GetInt32(4),
                            NumerPelny = reader.GetString(5),
                            Kontrahent = !reader.IsDBNull(6) ? reader.GetString(6) : null,
                            Wartosc = reader.GetDecimal(7),
                            WartoscZrealizowana = !reader.IsDBNull(8) ? reader.GetDecimal(8) : (decimal?)null,
                            TerminRealizacji = !reader.IsDBNull(9) ? reader.GetDateTime(9) : (DateTime?)null,
                            DataZrealizowania = !reader.IsDBNull(10) ? reader.GetDateTime(10) : (DateTime?)null,
                            TransakcjaVatRaw = reader.GetInt32(11),
                            Numer = reader.GetInt32(12),
                            Symbol = reader.GetString(13)
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }


        private static IQueryable<ZamowienieOdKlienta> PobierzZamowieniaOdKlientowNew(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = UtworzPolecenie(conn, _sqlWersja11))
            {
                conn.Open();
                int verPo11 = (int)cmd.ExecuteScalar();
                cmd.CommandText = _sqlDokumentyZK[verPo11];
                using (var reader = cmd.ExecuteReader())
                {
                    List<ZamowienieOdKlienta> lista = new List<ZamowienieOdKlienta>();
                    while (reader.Read())
                    {
                        lista.Add(new ZamowienieOdKlienta
                        {
                            Id = reader.GetInt32(0),
                            StatusRealizacji = (StatusRealizacji)reader.GetInt32(1),
                            StatusRezerwacji = (StatusRezerwacji)reader.GetInt32(2),
                            DataWystawienia = reader.GetDateTime(3),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(4),
                            PodtypRaw = reader.GetInt32(5),
                            NumerPelny = reader.GetString(6),
                            NumerOryginalu = reader.GetString(7),
                            Kontrahent = !reader.IsDBNull(8) ? reader.GetString(8) : null,
                            Wartosc = reader.GetDecimal(9),
                            WartoscZrealizowana = !reader.IsDBNull(10) ? reader.GetDecimal(10) : (decimal?)null,
                            TerminRealizacji = !reader.IsDBNull(11) ? reader.GetDateTime(11) : (DateTime?)null,
                            DataZrealizowania = !reader.IsDBNull(12) ? reader.GetDateTime(12) : (DateTime?)null,
                            TransakcjaVatRaw = reader.GetInt32(13),
                            CzyPrzetwarzaneNaZD = reader.GetBoolean(14),
                            Numer = reader.GetInt32(15),
                            Symbol = reader.GetString(16)
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private static IQueryable<KorektaDokumentuZakupu> PobierzKorektyZakupu(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = UtworzPolecenie(conn, _sqlWersja11))
            {
                conn.Open();
                int verPo11 = (int)cmd.ExecuteScalar();
                cmd.CommandText = _sqlDokumentyKFZ[verPo11];
                using (var reader = cmd.ExecuteReader())
                {
                    List<KorektaDokumentuZakupu> lista = new List<KorektaDokumentuZakupu>();
                    while (reader.Read())
                    {
                        lista.Add(new KorektaDokumentuZakupu
                        {
                            Id = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            DataOtrzymania = !reader.IsDBNull(2) ? reader.GetDateTime(2) : (DateTime?)null,
                            DataWystawienia = reader.GetDateTime(3),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(4),
                            PodtypRaw = reader.GetInt32(5),
                            NumerPelny = reader.GetString(6),
                            NumerOryginalu = reader.GetString(7),
                            Kontrahent = !reader.IsDBNull(8) ? reader.GetString(8) : null,
                            Wartosc = reader.GetDecimal(9),
                            TransakcjaVatRaw = reader.GetInt32(10),
                            Numer = reader.GetInt32(11),
                            Symbol = reader.GetString(12)
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private static IQueryable<ZwrotDoParagonu> PobierzZwrotyDoParagonu(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = UtworzPolecenie(conn, _sqlWersja11))
            {
                conn.Open();
                int verPo11 = (int)cmd.ExecuteScalar();
                cmd.CommandText = _sqlDokumentyZW[verPo11];
                using (var reader = cmd.ExecuteReader())
                {
                    List<ZwrotDoParagonu> lista = new List<ZwrotDoParagonu>();
                    while (reader.Read())
                    {
                        lista.Add(new ZwrotDoParagonu
                        {
                            Id = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            DataWystawienia = reader.GetDateTime(2),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(3),
                            PodtypRaw = reader.GetInt32(4),
                            NumerPelny = reader.GetString(5),
                            Wartosc = reader.GetDecimal(6),
                            Wystawil = reader.GetString(7),
                            TransakcjaVatRaw = reader.GetInt32(8),
                            Numer = reader.GetInt32(9),
                            Symbol = reader.GetString(10)
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private static IQueryable<Paragon> PobierzParagony(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = UtworzPolecenie(conn, _sqlWersja11))
            {
                conn.Open();
                int verPo11 = (int)cmd.ExecuteScalar();
                cmd.CommandText = _sqlDokumentyPA[verPo11];
                using (var reader = cmd.ExecuteReader())
                {
                    List<Paragon> lista = new List<Paragon>();
                    while (reader.Read())
                    {
                        lista.Add(new Paragon
                        {
                            Id = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            StatusFiskalny = (StatusFiskalny)reader.GetInt32(2),
                            DataWystawienia = reader.GetDateTime(3),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(4),
                            PodtypRaw = reader.GetInt32(5),
                            NumerPelny = reader.GetString(6),
                            Wartosc = reader.GetDecimal(7),
                            Wystawil = reader.GetString(8),
                            TransakcjaVatRaw = reader.GetInt32(9),
                            Numer = reader.GetInt32(10),
                            Symbol = reader.GetString(11)
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private static IQueryable<KorektaDokumentuSprzedazy> PobierzKorektySprzedazy(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = UtworzPolecenie(conn, _sqlWersja11))
            {
                conn.Open();
                int verPo11 = (int)cmd.ExecuteScalar();
                cmd.CommandText = _sqlDokumentyKFS[verPo11];
                using (var reader = cmd.ExecuteReader())
                {
                    List<KorektaDokumentuSprzedazy> lista = new List<KorektaDokumentuSprzedazy>();
                    while (reader.Read())
                    {
                        lista.Add(new KorektaDokumentuSprzedazy
                        {
                            Id = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            DataWystawienia = reader.GetDateTime(2),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(3),
                            PodtypRaw = reader.GetInt32(4),
                            NumerPelny = reader.GetString(5),
                            NumerKorygowanego = reader.GetString(6),
                            Kontrahent = !reader.IsDBNull(7) ? reader.GetString(7) : null,
                            Wartosc = reader.GetDecimal(8),
                            TransakcjaVatRaw = reader.GetInt32(9),
                            Numer = reader.GetInt32(10),
                            Symbol = reader.GetString(11)
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private static IQueryable<DokumentZakupu> PobierzDokumentyZakupu(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = UtworzPolecenie(conn, _sqlWersja11))
            {
                conn.Open();
                int verPo11 = (int)cmd.ExecuteScalar();
                cmd.CommandText = _sqlDokumentyFZ[verPo11];
                using (var reader = cmd.ExecuteReader())
                {
                    List<DokumentZakupu> lista = new List<DokumentZakupu>();
                    while (reader.Read())
                    {
                        lista.Add(new DokumentZakupu
                        {
                            Id = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            DataOtrzymania = !reader.IsDBNull(2) ? reader.GetDateTime(2) : (DateTime?)null,
                            DataWystawienia = reader.GetDateTime(3),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(4),
                            PodtypRaw = reader.GetInt32(5),
                            NumerPelny = reader.GetString(6),
                            NumerOryginalu = reader.GetString(7),
                            Kontrahent = !reader.IsDBNull(8) ? reader.GetString(8) : null,
                            Wartosc = reader.GetDecimal(9),
                            TransakcjaVatRaw = reader.GetInt32(10),
                            Numer = reader.GetInt32(11),
                            Symbol = reader.GetString(12)
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private static IQueryable<DokumentSprzedazy> PobierzDokumentySprzedazyWszystkie(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = UtworzPolecenie(conn, _sqlWersja11))
            {
                conn.Open();
                int verPo11 = (int)cmd.ExecuteScalar();
                cmd.CommandText = _sqlDokumentyFS[verPo11];
                using (var reader = cmd.ExecuteReader())
                {
                    List<DokumentSprzedazy> lista = new List<DokumentSprzedazy>();
                    while (reader.Read())
                    {
                        lista.Add(new DokumentSprzedazy()
                        {
                            Id = reader.GetInt32(0),
                            DataWystawienia = reader.GetDateTime(1),
                            NumerPelny = reader.GetString(2),
                            Kontrahent = !reader.IsDBNull(3) ? reader.GetString(3) : null,
                            Wartosc = reader.GetDecimal(4),
                            Status = (StatusDokumentu)reader.GetInt32(5),
                            StatusFiskalny = (StatusFiskalny)reader.GetInt32(6),
                            Typ = (TypDokumentu)reader.GetInt32(7),
                            PodtypRaw = reader.GetInt32(8),
                            TransakcjaVatRaw = reader.GetInt32(9),
                            FakturaUproszczona = reader.GetBoolean(10),
                            Numer = reader.GetInt32(11),
                            Symbol = reader.GetString(12)
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private static IQueryable<Kategoria> PobierzKategorie(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlKategorie, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<Kategoria> lista = new List<Kategoria>();
                    while (reader.Read())
                    {
                        lista.Add(new Kategoria()
                        {
                            Id = reader.GetInt32(0),
                            Nazwa = reader.GetString(1),
                            Podtytul = reader.GetString(2),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private static IQueryable<Magazyn> PobierzMagazyny(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlMagazyny, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<Magazyn> lista = new List<Magazyn>();
                    while (reader.Read())
                    {
                        lista.Add(new Magazyn()
                        {
                            Id = reader.GetInt32(0),
                            Nazwa = reader.GetString(1),
                            Symbol = reader.GetString(2),
                            Opis = reader.GetString(3),
                            Glowny = reader.GetInt32(4) != 0,
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private string PobierzHTMLPodgladuObiektu(XElement konfiguracja, TypPodgladu typPodgladu, int idDokumentu)
        {
            string[] sqlNaglowek = null;
            string[] sqlPozycje = null;
            string[] sqlTabelaVat = null;
            string sqlPlatnosci = null;
            bool magazynowy = false;
            switch (typPodgladu.TypDanych)
            {
                case TypDanych.FakturySprzedazy:
                case TypDanych.SprzedazDetaliczna:
                case TypDanych.FakturyZakupu:
                case TypDanych.ZamowieniaOdKlientow:
                case TypDanych.ZamowieniaDoDostawcow:
                    sqlNaglowek = _sqlNaglowekHandlowy;
                    sqlPozycje = _sqlPozycje;
                    sqlTabelaVat = _sqlTabelaVat;
                    sqlPlatnosci = _sqlPlatnosci;
                    break;
                case TypDanych.ZwrotyDetaliczne:
                    sqlNaglowek = _sqlNaglowekHandlowy;
                    sqlPozycje = _sqlPozycjeZwrotu;
                    sqlTabelaVat = _sqlTabelaVat;
                    sqlPlatnosci = _sqlPlatnosci;
                    break;
                case TypDanych.KorektySprzedazy:
                case TypDanych.KorektyZakupu:
                    sqlNaglowek = _sqlNaglowekHandlowy;
                    sqlPozycje = _sqlPozycjeKorekty;
                    sqlTabelaVat = _sqlTabelaVatKorekty;
                    sqlPlatnosci = _sqlPlatnosci;
                    break;
                case TypDanych.WydaniaMagazynowe:
                case TypDanych.PrzyjeciaMagazynowe:
                    sqlNaglowek = _sqlNaglowekMagazynowy;
                    sqlPozycje = _sqlPozycjeKorekty;
                    sqlTabelaVat = _sqlTabelaVat;
                    sqlPlatnosci = _sqlPlatnosci;
                    magazynowy = true;
                    break;
            }
            return PobierzHTMLPodgladuObiektu(konfiguracja, idDokumentu, sqlNaglowek, sqlPozycje, sqlTabelaVat, sqlPlatnosci, magazynowy);
        }

        private static string PobierzHTMLPodgladuObiektu(XElement konfiguracja, int idDokumentu, string[] sqlNaglowek, string[] sqlPozycje, string[] sqlTabelaVat, string sqlPlatnosci, bool magazynowy)
        {
            try
            {
                using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
                using (SqlCommand cmd = UtworzPolecenie(conn, _sqlWersja11))
                {
                    conn.Open();
                    int verPo11 = (int)cmd.ExecuteScalar();

                    cmd.CommandText = sqlNaglowek[verPo11];
                    cmd.Parameters.AddWithValue("id", idDokumentu);

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds, "Dokument");
                    cmd.CommandText = sqlPozycje[verPo11];
                    adapter.Fill(ds, "Pozycje");
                    cmd.CommandText = sqlTabelaVat[verPo11];
                    adapter.Fill(ds, "Vat");
                    cmd.CommandText = sqlPlatnosci;
                    adapter.Fill(ds, "Platnosci");

                    if (idDokumentu == 0) //do celów testowych - żeby można było uruchomić wszystkie SQL-e i je sprawdzić
                        return string.Empty;
                    PodgladDokumentu podglad = new PodgladDokumentu(ds, magazynowy);
                    return podglad.TransformText();
                }
            }
            catch (Exception ex)
            {
                return Resources.podglad.Replace(_podgladPlaceholder, "<p style=\"font-weight: bold;\">Nie udało się wygenerować podglądu z powodu błędu:</p><div style=\"width: 500; font-family: courier new\">" + ex.Message + "</div>");
            }
        }

        public static string HtmlEncode(string text)
        {
            if (text == null)
                return null;

            StringBuilder sb = new StringBuilder(text.Length);

            int len = text.Length;
            for (int i = 0; i < len; i++)
            {
                switch (text[i])
                {

                    case '<':
                        sb.Append("&lt;");
                        break;
                    case '>':
                        sb.Append("&gt;");
                        break;
                    case '"':
                        sb.Append("&quot;");
                        break;
                    case '&':
                        sb.Append("&amp;");
                        break;
                    default:
                        if (text[i] > 159)
                        {
                            sb.Append("&#");
                            sb.Append(((int)text[i]).ToString(System.Globalization.CultureInfo.InvariantCulture));
                            sb.Append(";");
                        }
                        else
                            sb.Append(text[i]);
                        break;
                }
            }
            return sb.ToString();
        }

        private static XElement PodajWezel(XElement wezelGlowny, string nazwa)
        {
            return wezelGlowny != null ?
                wezelGlowny.Elements().FirstOrDefault(e => e.Name.ToString().Equals(nazwa, StringComparison.OrdinalIgnoreCase)) :
                null;
        }

        private static SqlCommand UtworzPolecenie(SqlConnection conn, string polecenieSql)
        {
            return new SqlCommand(polecenieSql, conn);
        }

        private static SqlConnection UtworzPolaczenie(XElement konfiguracja)
        {
            return UtworzPolaczenie(WczytajKonfiguracje(konfiguracja));
        }

        private static SqlConnection UtworzPolaczenie(IKontekstBazy kontekst)
        {
            SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
            csb.DataSource = kontekst.Serwer;
            csb.InitialCatalog = kontekst.Baza;
            csb.IntegratedSecurity = kontekst.ZaufanePolaczenie;
            if (!kontekst.ZaufanePolaczenie)
            {
                csb.UserID = kontekst.Login;
                csb.Password = kontekst.Haslo;
            }
			csb.TrustServerCertificate = true;
			return new SqlConnection(csb.ToString());
        }

        private static void WykonajZapytanie(string query, IKontekstBazy kontekst, Action<SqlDataReader> akcja)
        {
            using (var conn = UtworzPolaczenie(kontekst))
            {
                conn.Open();

                using (var cmd = new SqlCommand(query, conn))
                {
                    using (var dataReader = cmd.ExecuteReader())
                    {
                        akcja(dataReader);
                    }
                }
                conn.Close();
            }
        }

        //private static Tuple<string, string> PobierzTransformatePodgladu(IKontekstBazy kontekst, TypPodgladu typPodgladu)
        //{
        //    string def = null;
        //    string xsl = null;
        //    WykonajZapytanie(string.Format(_sqlTransformacja, typPodgladu.Id), kontekst, reader =>
        //    {
        //        if (reader.Read())
        //        {
        //            using (var stream = new MemoryStream((byte[])reader[0]))
        //            {
        //                using (var sr = new StreamReader(stream))
        //                {
        //                    def = sr.ReadToEnd();
        //                }
        //            }
        //            if (!reader.IsDBNull(1))
        //            {
        //                using (var stream = new MemoryStream((byte[])reader[1]))
        //                {
        //                    using (var sr = new StreamReader(stream))
        //                    {
        //                        xsl = sr.ReadToEnd();
        //                    }
        //                }
        //            }
        //        }
        //    });

        //    return Tuple.Create(def, xsl);
        //}

        private static IKontekstBazy WczytajKonfiguracje(XElement wezelKonf)
        {
            KontekstBazy kontekst = new KontekstBazy();
            try
            {
                foreach (var wezel in wezelKonf.Elements())
                {
                    var nazwa = wezel.Name.ToString();
                    if (nazwa.Equals(_nazwaWezlaSerwera, StringComparison.OrdinalIgnoreCase))
                        kontekst.Serwer = wezel.Value;
                    else if (nazwa.Equals(_nazwaWezlaBazy, StringComparison.OrdinalIgnoreCase))
                        kontekst.Baza = wezel.Value;
                    else if (nazwa.Equals(_nazwaWezlaLoginu, StringComparison.OrdinalIgnoreCase))
                    {
                        var dane = wezel.Value.Split('/');
                        if (dane.Any())
                            kontekst.Login = dane[0];
                        if (dane.Count() > 1)
                            kontekst.Haslo = dane[1];
                    }
                    else if (nazwa.Equals(_nazwaWezlaAutentykacji, StringComparison.OrdinalIgnoreCase))
                        kontekst.ZaufanePolaczenie =
                            wezel.Value.Equals(_autentykacjaWindows, StringComparison.OrdinalIgnoreCase) ? true : false;
                }
            }
            catch { }
            return kontekst;
        }

        #endregion

        #region IFunkcjaPobieraniaDanychArchiwalnych

        private bool BazaInsertNexo(XElement konfiguracja)
        {
            bool ret = false;

            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = UtworzPolecenie(conn, _sqlPodmiotNexo))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.GetString(0) == "true")
                            ret = true;
                    }
                }
            }

            return ret;
        }

        public XElement UtworzKonfiguracje(string serwer, string baza)
        {
            return UtworzKonfiguracje(serwer, baza, null, null);
        }

        public XElement UtworzKonfiguracje(string serwer, string baza, string login, string haslo)
        {
            return new XElement(_nazwaWezlaKonfiguracji,
                        new XElement(_nazwaWezlaSerwera, serwer),
                        new XElement(_nazwaWezlaAutentykacji, !string.IsNullOrEmpty(login) && haslo != null ? _autentykacjaMixed : _autentykacjaWindows),
                        new XElement(_nazwaWezlaLoginu, !string.IsNullOrEmpty(login) && haslo != null ? string.Format("{0}/{1}", login, haslo) : ""),
                        new XElement(_nazwaWezlaBazy, baza));
        }

        #endregion

    }


}
