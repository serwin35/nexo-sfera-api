using InsERT.Moria.Archiwa;
using InsERT.Moria.Asortymenty;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Klienci;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;

namespace DaneArchiwalne
{
    public class DaneArchiwalneInsertGtBazowa
    {
        protected static readonly List<TypDanych> _obslugiwaneTypy = new List<TypDanych>
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
            TypDanych.ZwrotyDetaliczne
        };

        protected enum TypObiektuPodgladu
        {
            Nieznany, Dokument
        }

        protected static string _podgladPlaceholder = "###PODGLAD###";

        #region Stałe - konfiguracja

        protected const string _nazwaWezlaKonfiguracja = "Konfiguracja";
        protected const string _nazwaWezlaSerwer = "sql_server";
        protected const string _nazwaWezlaBaza = "database";
        protected const string _nazwaWezlaLogin = "sql_login";
        protected const string _nazwaWezlaAutentykacja = "auth_mode";

        protected const string _autentykacjaWindows = "WINDOWS";
        protected const string _autentykacjaMixed = "MIXED";

        #endregion

        #region Stałe - SQL

        protected static readonly string _sqlPodmiotGt = @"SELECT value FROM sys.extended_properties WHERE name = 'inspdm';";

        protected static readonly string _sqlDokumentyFS = "SELECT dok_Id,  dok_DataWyst, dok_NrPelny, adrh_Nazwa, dok_KwWartosc, dok_Status, dok_StatusFiskal, dok_Typ, dok_Podtyp, dok_RodzajOperacjiVat, dok_FakturaUproszczona, dok_Nr, RTRIM(LEFT(dok_NrPelny, CHARINDEX(' ', dok_NrPelny, 0))), dok_MagId, mag_Symbol, kat_Id, kat_Nazwa FROM [dbo].[dok__Dokument] LEFT JOIN [dbo].[adr_Historia] ON adrh_Id = dok_PlatnikAdreshId LEFT JOIN sl_Magazyn ON dok_MagId=mag_Id LEFT JOIN sl_Kategoria ON dok_KatId=kat_Id WHERE dok_Typ IN (2, 4, 62);";
        protected static readonly string _sqlDokumentyFZ = "SELECT dok_Id, dok_Status, dok_DataOtrzym, dok_DataWyst, dok_Typ, dok_Podtyp, dok_NrPelny, dok_NrPelnyOryg, adrh_Nazwa, dok_WartBrutto, dok_RodzajOperacjiVat, dok_Nr, RTRIM(LEFT(dok_NrPelny, CHARINDEX(' ', dok_NrPelny, 0))), dok_MagId, mag_Symbol, kat_Id, kat_Nazwa FROM [dbo].[dok__Dokument] LEFT JOIN [dbo].[adr_Historia] ON adrh_Id = dok_PlatnikAdreshId LEFT JOIN sl_Magazyn ON dok_MagId=mag_Id LEFT JOIN sl_Kategoria ON dok_KatId=kat_Id WHERE dok_Typ IN (1, 3, 34);";
        protected static readonly string _sqlDokumentyKFS = "SELECT dok_Id, dok_Status, dok_DataWyst, dok_Typ, dok_Podtyp, dok_NrPelny, dok_DoDokNrPelny, adrh_Nazwa, dok_KwWartosc, dok_RodzajOperacjiVat, dok_Nr, RTRIM(LEFT(dok_NrPelny, CHARINDEX(' ', dok_NrPelny, 0))), dok_MagId, mag_Symbol, kat_Id, kat_Nazwa FROM [dbo].[dok__Dokument] LEFT JOIN [dbo].[adr_Historia] ON adrh_Id = dok_PlatnikAdreshId LEFT JOIN sl_Magazyn ON dok_MagId=mag_Id LEFT JOIN sl_Kategoria ON dok_KatId=kat_Id WHERE dok_Typ = 6 OR dok_Typ = 67;";
        protected static readonly string _sqlDokumentyPA = "SELECT dok_Id, dok_Status, dok_StatusFiskal,  dok_DataWyst, dok_Typ, dok_Podtyp, dok_NrPelny, dok_KwWartosc, dok_Wystawil, dok_RodzajOperacjiVat, dok_Nr, RTRIM(LEFT(dok_NrPelny, CHARINDEX(' ', dok_NrPelny, 0))), dok_MagId, mag_Symbol, kat_Id, kat_Nazwa FROM [dbo].[dok__Dokument] LEFT JOIN sl_Magazyn ON dok_MagId=mag_Id LEFT JOIN sl_Kategoria ON dok_KatId=kat_Id WHERE dok_Typ = 21;";
        protected static readonly string _sqlDokumentyZW = "SELECT dok_Id, dok_Status, dok_DataWyst, dok_Typ, dok_Podtyp, dok_NrPelny, dok_KwWartosc, dok_Wystawil, dok_RodzajOperacjiVat, dok_Nr, RTRIM(LEFT(dok_NrPelny, CHARINDEX(' ', dok_NrPelny, 0))), dok_MagId, mag_Symbol, kat_Id, kat_Nazwa FROM [dbo].[dok__Dokument] LEFT JOIN sl_Magazyn ON dok_MagId=mag_Id LEFT JOIN sl_Kategoria ON dok_KatId=kat_Id WHERE dok_Typ = 14;";
        protected static readonly string _sqlDokumentyKFZ = "SELECT dok_Id, dok_Status, dok_DataOtrzym, dok_DataWyst, dok_Typ, dok_Podtyp, dok_NrPelny, dok_NrPelnyOryg, adrh_Nazwa, dok_WartBrutto, dok_RodzajOperacjiVat, dok_Nr, RTRIM(LEFT(dok_NrPelny, CHARINDEX(' ', dok_NrPelny, 0))), dok_MagId, mag_Symbol, kat_Id, kat_Nazwa FROM [dbo].[dok__Dokument] LEFT JOIN [dbo].[adr_Historia] ON adrh_Id = dok_PlatnikAdreshId LEFT JOIN sl_Magazyn ON dok_MagId=mag_Id LEFT JOIN sl_Kategoria ON dok_KatId=kat_Id WHERE dok_Typ = 5;";
        protected static readonly string _sqlDokumentyZK = "SELECT dok_Id, statusreal, statusrez, dok_DataWyst, dok_Typ, dok_Podtyp, dok_NrPelny, dok_NrPelnyOryg, adrh_Nazwa, dok_KwWartosc, zre_KwWartosc, dok_TerminRealizacji, pow_DataWyst, dok_RodzajOperacjiVat, ss_PrzetworzonoZKwZD, dok_Nr, RTRIM(LEFT(dok_NrPelny, CHARINDEX(' ', dok_NrPelny, 0))), dok_MagId, mag_Symbol, kat_Id, kat_Nazwa FROM [dbo].[vwDok4ZamGrid] LEFT JOIN [dbo].[adr_Historia] ON adrh_Id = dok_PlatnikAdreshId LEFT JOIN sl_Magazyn ON dok_MagId=mag_Id LEFT JOIN sl_Kategoria ON dok_KatId=kat_Id WHERE dok_Typ = 16;";
        protected static readonly string _sqlDokumentyZD = "SELECT dok_Id, statusreal, dok_DataWyst, dok_Typ, dok_Podtyp, dok_NrPelny, adrh_Nazwa, dok_KwWartosc, zre_KwWartosc, dok_TerminRealizacji, pow_DataWyst, dok_RodzajOperacjiVat, dok_Nr, RTRIM(LEFT(dok_NrPelny, CHARINDEX(' ', dok_NrPelny, 0))), dok_MagId, mag_Symbol, kat_Id, kat_Nazwa FROM [dbo].[vwDok4ZamGrid] LEFT JOIN [dbo].[adr_Historia] ON adrh_Id = dok_PlatnikAdreshId LEFT JOIN sl_Magazyn ON dok_MagId=mag_Id LEFT JOIN sl_Kategoria ON dok_KatId=kat_Id WHERE dok_Typ = 15;";
        protected static readonly string _sqlDokumentyWZ = "SELECT dok_Id, dok_Status, dok_DataWyst, dok_Typ, dok_Podtyp, dok_NrPelny, adrh_Nazwa, dok_WartNetto, dok_WartMag, dok_DoDokNrPelny, dok_Nr, RTRIM(LEFT(dok_NrPelny, CHARINDEX(' ', dok_NrPelny, 0))), dok_MagId, mag_Symbol, kat_Id, kat_Nazwa FROM [dbo].[dok__Dokument] LEFT JOIN [dbo].[adr_Historia] ON adrh_Id = dok_OdbiorcaAdreshId LEFT JOIN sl_Magazyn ON dok_MagId=mag_Id LEFT JOIN sl_Kategoria ON dok_KatId=kat_Id WHERE dok_Typ IN (11, 13, 25, 9);";
        protected static readonly string _sqlDokumentyPZ = "SELECT dok_Id, dok_Status, dok_DataWyst, dok_Typ, dok_Podtyp, dok_NrPelny, dok_NrPelnyOryg, adrh_Nazwa, dok_WartNetto, dok_WartMag, dok_DoDokNrPelny, dok_Nr, RTRIM(LEFT(dok_NrPelny, CHARINDEX(' ', dok_NrPelny, 0))), dok_MagId, mag_Symbol, kat_Id, kat_Nazwa FROM [dbo].[dok__Dokument] LEFT JOIN [dbo].[adr_Historia] ON adrh_Id = dok_OdbiorcaAdreshId LEFT JOIN sl_Magazyn ON dok_MagId=mag_Id LEFT JOIN sl_Kategoria ON dok_KatId=kat_Id WHERE dok_Typ IN (10, 12, 26, 9);";

        protected static readonly string _sqlMozliwePodglady = "SELECT gtt_Id, gtt_Nazwa, gtt_DefinicjaId FROM gt_Transformacja WHERE gtt_ObiektId = @id;";
        protected static readonly string _sqlTransformacja = "SELECT gtd_Definicja, gtt_Transformacja FROM gt_Definicja INNER JOIN gt_Transformacja ON gtd_Id=gtt_DefinicjaId WHERE gtt_Id=@id;";

        protected static readonly string _sqlMagazyny = "SELECT mag_Id, mag_Symbol, mag_Nazwa, mag_Opis, mag_Glowny FROM sl_Magazyn;";
        protected static readonly string _sqlKategorie = "SELECT kat_Id, kat_Nazwa, kat_Podtytul FROM sl_Kategoria WHERE kat_Typ=2;";

        #endregion

        #region Metody pomocnicze

        protected static TypObiektuPodgladu PodajTypPodgladu(TypDanych typDanych)
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

        protected static SqlCommand UtworzPolecenie(SqlConnection conn, string polecenieSql)
        {
            return new SqlCommand(polecenieSql, conn);
        }

        protected static SqlConnection UtworzPolaczenie(IKontekstBazy kontekst)
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

        protected static SqlConnection UtworzPolaczenie(XElement konfiguracja)
        {
            return UtworzPolaczenie(WczytajKonfiguracje(konfiguracja));
        }

        protected static IKontekstBazy WczytajKonfiguracje(XElement wezelKonf)
        {
            KontekstBazy kontekst = new KontekstBazy();
            foreach (var wezel in wezelKonf.Elements())
            {
                var nazwa = wezel.Name.ToString();
                if (nazwa.Equals(_nazwaWezlaSerwer, StringComparison.OrdinalIgnoreCase))
                    kontekst.Serwer = wezel.Value;
                else if (nazwa.Equals(_nazwaWezlaBaza, StringComparison.OrdinalIgnoreCase))
                    kontekst.Baza = wezel.Value;
                else if (nazwa.Equals(_nazwaWezlaLogin, StringComparison.OrdinalIgnoreCase))
                {
                    var dane = wezel.Value.Split('/');
                    if (dane.Any())
                        kontekst.Login = dane[0];
                    if (dane.Count() > 1)
                        kontekst.Haslo = dane[1];
                }
                else if (nazwa.Equals(_nazwaWezlaAutentykacja, StringComparison.OrdinalIgnoreCase))
                    kontekst.ZaufanePolaczenie =
                        wezel.Value.Equals(_autentykacjaWindows, StringComparison.OrdinalIgnoreCase) ? true : false;
            }
            return kontekst;
        }

        protected static void WykonajZapytanie(string query, IKontekstBazy kontekst, Action<SqlDataReader> akcja)
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

        protected bool BazaInsertGt(XElement konfiguracja)
        {
            bool ret = false;

            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = UtworzPolecenie(conn, _sqlPodmiotGt))
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

        protected static Tuple<string, string> PobierzTransformatePodgladu(IKontekstBazy kontekst, TypPodgladu typPodgladu)
        {
            string def = null;
            string xsl = null;
            WykonajZapytanie(string.Format(_sqlTransformacja, typPodgladu.Id), kontekst, reader =>
            {
                if (reader.Read())
                {
                    using (var stream = new MemoryStream((byte[])reader[0]))
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            def = sr.ReadToEnd();
                        }
                    }
                    if (!reader.IsDBNull(1))
                    {
                        using (var stream = new MemoryStream((byte[])reader[1]))
                        {
                            using (var sr = new StreamReader(stream))
                            {
                                xsl = sr.ReadToEnd();
                            }
                        }
                    }
                }
            });

            return Tuple.Create(def, xsl);
        }

        protected static string PobierzHTMLPodgladuObiektu(IKontekstBazy kontekst, TypPodgladu typPodgladu, int idDokumentu)
        {
            try
            {
                ADODB._Connection conn = new ADODB.Connection();
                if (kontekst.ZaufanePolaczenie)
                    conn.ConnectionString = string.Format("PROVIDER=SQLOLEDB;SERVER={0};DATABASE={1};TRUSTED_CONNECTION=YES;",
                        kontekst.Serwer, kontekst.Baza);
                else
                    conn.ConnectionString = string.Format("PROVIDER=SQLOLEDB;SERVER={0};DATABASE={1};USER ID={2};PASSWORD={3};",
                        kontekst.Serwer, kontekst.Baza, kontekst.Login, kontekst.Haslo);
                conn.Open();

                int id = idDokumentu;

                WspolneLib.CoExtractor extractor = new WspolneLib.CoExtractor();
                extractor.ActiveConnection = conn;

                System.Xml.Xsl.XslCompiledTransform xsl = null;

                using (SqlConnection sqlConn = UtworzPolaczenie(kontekst))
                using (SqlCommand cmd = new SqlCommand(_sqlTransformacja, sqlConn))
                {
                    sqlConn.Open();
                    cmd.Parameters.AddWithValue("id", typPodgladu.Id);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string sDef = System.Text.Encoding.UTF8.GetString((byte[])reader[0]);
                            extractor.XMLDefinition = sDef;
                            string sXsl = reader[1] is System.DBNull ? null : System.Text.Encoding.UTF8.GetString((byte[])reader[1]);
                            if (!string.IsNullOrWhiteSpace(sXsl))
                            {
                                xsl = new System.Xml.Xsl.XslCompiledTransform();
                                using (StringReader sReader = new StringReader(sXsl))
                                using (XmlReader xmlReader = XmlReader.Create(sReader))
                                    xsl.Load(xmlReader);
                            }
                        }
                    }
                }

                XmlDocument xmlDoc = new XmlDocument();
                extractor.AddParam("id", id);
                string xml = extractor.GetData(WspolneLib.WsResultType.wrtString);
                xmlDoc.LoadXml(xml);
                conn.Close();
                System.Runtime.InteropServices.Marshal.ReleaseComObject(conn);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(extractor);

                string html = string.Empty;
                using (MemoryStream outStream = new MemoryStream())
                {
                    using (StreamWriter outWriter = new StreamWriter(outStream))
                    using (StreamReader reader = new StreamReader(outStream))
                    {
                        if (xsl != null)
                            xsl.Transform(xmlDoc, null, outWriter);
                        else
                        {
                            outWriter.Write("<pre>");
                            outWriter.Write(HtmlEncode(xml));
                            outWriter.Write("</pre>");
                        }
                        outWriter.Flush();
                        outStream.Seek(0, SeekOrigin.Begin);
                        html = Resources.podglad.Replace(_podgladPlaceholder, reader.ReadToEnd());
                    }
                }
                return html;
            }
            catch (Exception ex)
            {
                return Resources.podglad.Replace(_podgladPlaceholder, "<p style=\"font-weight: bold;\">Nie udało się wygenerować podglądu z powodu błędu:</p><div style=\"width: 500; font-family: courier new\">" + ex.Message + "</div>");
            }
        }

        #endregion

    }
}