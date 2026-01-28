using InsERT.Moria.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Mox.DatabaseAccess;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace RozszerzeniaKontroliSkarbowej
{
    public class Rozszerzenia_JPK_MAG : RozszerzeniaBase
    {
        /// <summary>
        /// Funkcja pobiera numer wewnętrzny nagłówka dokumentu magazynowego do pola NumerPZ w sekcji PZWartosc pliku JPK_MAG.
        /// </summary>
        /// <param name="obiekt">Nagłówek dokumentu magazynowego.</param>
        /// <returns>Numer wewnętrzny dokumentu magazynowego.</returns>
        public string NumerWewnetrznyDokumentuMagazynowego_NumerPZ(DokumentMagazynowy obiekt)
        {
            return obiekt.NumerWewnetrzny.PelnaSygnatura;
        }

        /// <summary>
        /// Funkcja pobiera numer wewnętrzny dokumentu magazynowego do pola Numer2PZ w sekcji PZWiersz pliku JPK_MAG.
        /// </summary>
        /// <param name="obiekt">Pozycja dokumentu magazynowego JPK.</param>
        /// <returns>Numer wewnętrzny dokumentu magazynowego.</returns>
        public string NumerWewnetrznyDokumentuMagazynowegoPozycji_Numer2PZ(PozycjaDokumentuMagazynowegoJPK obiekt)
        {
            Dictionary<int, string> cache = PobierzLubDodajZmiennaUzytkownika("Cache", () => new Dictionary<int, string>());
            string numer;
            if (!cache.TryGetValue(obiekt.IdDokumentu, out numer))
            {
                using (DbConnection dbConnection = FabrykaPolaczenia.CreateConnection(DbConnectionFlags.NoPooling | DbConnectionFlags.NoEnlist))
                {
                    dbConnection.Open();
                    try
                    {
                        using (DbCommand command = dbConnection.CreateCommand())
                        {
                            DbParameter dokumentId = command.CreateParameter();
                            dokumentId.ParameterName = "@DokumentId";
                            dokumentId.Value = obiekt.IdDokumentu;
                            dokumentId.DbType = System.Data.DbType.Int32;
                            command.Parameters.Add(dokumentId);
                            command.CommandText = "SELECT NumerWewnetrzny_PelnaSygnatura FROM ModelDanychContainer.Dokumenty where Id = @DokumentId;";
                            numer = command.ExecuteScalar() as string;
                            cache.Add(obiekt.IdDokumentu, numer ?? string.Empty);
                        }
                    }
                    finally
                    {
                        dbConnection.Close();
                    }
                }
            }
            return numer;
        }

        /// <summary>
        /// Funkcja pobiera symbol magazynu źródłowego na dokumencie wydania lub przyjęcia międzymagazynowego do pola SkadMM w sekcji MMWartosc pliku JPK_MAG.
        /// </summary>
        /// <param name="obiekt">Nagłówek dokumentu magazynowego.</param>
        /// <returns>Symbol magazynu źródłowego.</returns>
        public string SymbolMagazynuZrodlowego_SkadMM(DokumentMagazynowy obiekt)
        {
            int idMagazynu;
            if (obiekt is DokumentMMP mmp)
                idMagazynu = mmp.DokumentMMW.MagazynId.GetValueOrDefault(0);
            else if (obiekt is DokumentMMW mmw)
                idMagazynu = mmw.MagazynId.GetValueOrDefault(0);
            else
                return string.Empty;
            Dictionary<int, string> cache = PobierzLubDodajZmiennaUzytkownika("Cache", () => new Dictionary<int, string>());
            string symbol;
            InsERT.Moria.ModelOrganizacyjny.IMagazynyDane magazynyDane = PodajDaneTypu<InsERT.Moria.ModelOrganizacyjny.IMagazynyDane>();
            if (!cache.TryGetValue(idMagazynu, out symbol))
            {
                symbol = magazynyDane.Wszystkie().Where(m => m.Id == idMagazynu).Select(m => m.Symbol).FirstOrDefault() ?? string.Empty;
                cache.Add(idMagazynu, symbol);
            }
            return symbol;
        }

        /// <summary>
        /// Funkcja pobiera symbol magazynu docelowego na dokumencie wydania lub przyjęcia międzymagazynowego do pola DokadMM w sekcji MMWartosc pliku JPK_MAG.
        /// </summary>
        /// <param name="obiekt">Nagłówek dokumentu magazynowego.</param>
        /// <returns>Symbol magazynu docelowego.</returns>
        public string SymbolMagazynuDocelowego_DokadMM(DokumentMagazynowy obiekt)
        {
            if (obiekt is DokumentMMP mmp)
                return mmp.Magazyn.Symbol;
            else if (obiekt is DokumentMMW mmw)
                return mmw.MagazynDocelowy.Symbol;
            return string.Empty;
        }

        /// <summary>
        /// Funkcja pobiera numer dokumentu magazynowego z pola własnego typu tekst o nazwie 'Numer własny', który można wstawić np. do pola NumerRW sekcji RWWartosc pliku JPK_MAG.
        /// </summary>
        /// <param name="obiekt">Nagłówek dokumentu magazynowego.</param>
        /// <returns>Numer własny dokumentu magazynowego.</returns>
        public string WlasnyNumerRW_NumerRW(DokumentMagazynowy obiekt)
        {
            IPolaWlasneAdv2Accessor accessor = PolaWlasneAccessorFactory.Utworz(obiekt);
            string numerWlasny = accessor.PobierzWartoscTypuTekst("Numer własny");
            if (string.IsNullOrEmpty(numerWlasny))
                return obiekt.NumerWewnetrzny.PelnaSygnatura;
            return numerWlasny;
        }

        /// <summary>
        /// Funkcja pobiera numer dokumentu magazynowego z pola własnego dokumentu typu tekst o nazwie 'Numer własny', który można wstawić np. do pola Numer2RW sekcji RWWiersz pliku JPK_MAG.
        /// </summary>
        /// <param name="obiekt">Pozycja dokumentu magazynowego.</param>
        /// <returns>Numer własny dokumentu magazynowego.</returns>
        public string WlasnyNumerRWPozycji_Numer2RW(PozycjaDokumentuMagazynowegoJPK obiekt)
        {
            Dictionary<int, string> cache = PobierzLubDodajZmiennaUzytkownika("Cache", () => new Dictionary<int, string>());
            string numerWlasny;
            if (!cache.TryGetValue(obiekt.IdDokumentu, out numerWlasny))
            {
                InsERT.Moria.Dokumenty.Logistyka.IRozchodyWewnetrzneDane dane = PodajDaneTypu<InsERT.Moria.Dokumenty.Logistyka.IRozchodyWewnetrzneDane>();
                numerWlasny = dane.Wszystkie()
                    .Where(d => d.Id == obiekt.IdDokumentu)
                    .Select(d => d.PolaWlasneAdv2.Get<string>("Numer własny"))
                    .ResolveExtensionProperties()
                    .FirstOrDefault() ?? string.Empty;
                cache.Add(obiekt.IdDokumentu, numerWlasny);
            }
            if (string.IsNullOrEmpty(numerWlasny))
                return obiekt.NumerDokumentu;
            return numerWlasny;
        }

        /// <summary>
        /// Funkcja pobiera wartość netto dokumentu magazynowego, którą można wstawić np. do pola WartoscWZ sekcji WZWartosc pliku JPK_MAG.
        /// </summary>
        /// <param name="obiekt">Nagłówek dokumentu magazynowego.</param>
        /// <returns>Wartość netto dokumentu magazynowego.</returns>
        public decimal WartoscWZ(DokumentMagazynowy obiekt)
        {
            return obiekt.Wartosc.NettoPoRabacie;
        }

        /// <summary>
        /// Funkcja pobiera cenę netto dokumentu magazynowego, którą można wstawić np. do pola CenaJednWZ sekcji WZWiersz pliku JPK_MAG.
        /// </summary>
        /// <param name="obiekt">Pozycja dokumentu magazynowego.</param>
        /// <returns>Cena netto pozycji dokumentu magazynowego.</returns>
        public decimal CenaJednostkowaWZ(PozycjaDokumentuMagazynowegoJPK obiekt)
        {
            Dictionary<int, decimal> cache = PobierzLubDodajZmiennaUzytkownika("Cache", () => new Dictionary<int, decimal>());
            decimal cena;
            if (!cache.TryGetValue(obiekt.Id, out cena))
            {
                InsERT.Moria.Dokumenty.Logistyka.IDokumentyDane dokumenty = PodajDaneTypu<InsERT.Moria.Dokumenty.Logistyka.IDokumentyDane>();
                List<(int id, decimal cena)> danePozycji = dokumenty.Wszystkie().Where(d => d.Id == obiekt.IdDokumentu)
                    .SelectMany(d => d.Pozycje)
                    .Select(p => new { p.Id, p.Cena.NettoPoRabacie })
                    .ToArray()
                    .Select(d => (d.Id, d.NettoPoRabacie))
                    .ToList();
                foreach (var dane in danePozycji)
                {
                    if (!cache.ContainsKey(dane.id))
                        cache.Add(dane.id, dane.cena);
                }
                if (!cache.TryGetValue(obiekt.Id, out cena))
                    cache.Add(obiekt.Id, cena = 0m);
            }
            return cena;
        }

        /// <summary>
        /// Funkcja pobiera wartość netto dokumentu magazynowego, którą można wstawić np. do pola WartoscPozycjiWZ sekcji WZWiersz pliku JPK_MAG.
        /// </summary>
        /// <param name="obiekt">Pozycja dokumentu magazynowego.</param>
        /// <returns>Wartość netto pozycji dokumentu magazynowego.</returns>
        public decimal WartoscPozycjiWZ(PozycjaDokumentuMagazynowegoJPK obiekt)
        {
            Dictionary<int, decimal> cache = PobierzLubDodajZmiennaUzytkownika("Cache", () => new Dictionary<int, decimal>());
            decimal wartosc;
            if (!cache.TryGetValue(obiekt.Id, out wartosc))
            {
                InsERT.Moria.Dokumenty.Logistyka.IDokumentyDane dokumenty = PodajDaneTypu<InsERT.Moria.Dokumenty.Logistyka.IDokumentyDane>();
                List<(int id, decimal wartosc)> danePozycji = dokumenty.Wszystkie().Where(d => d.Id == obiekt.IdDokumentu)
                    .SelectMany(d => d.Pozycje)
                    .Select(p => new { p.Id, p.Wartosc.NettoPoRabacie })
                    .ToArray()
                    .Select(d => (d.Id, d.NettoPoRabacie))
                    .ToList();
                foreach (var dane in danePozycji)
                {
                    if (!cache.ContainsKey(dane.id))
                        cache.Add(dane.id, dane.wartosc);
                }
                if (!cache.TryGetValue(obiekt.Id, out wartosc))
                    cache.Add(obiekt.Id, wartosc = 0m);
            }
            return wartosc;
        }
    }
}
