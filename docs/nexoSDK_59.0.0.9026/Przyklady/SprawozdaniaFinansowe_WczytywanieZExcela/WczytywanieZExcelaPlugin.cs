using InsERT.Moria.SprawozdaniaFinansowe.Nowe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Wspomaganie;
using InsERT.Mox.Validation;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace InsERT.Moria.SprawozdaniaFinansowe.WczytywanieZExcela
{
    public class WczytywanieZExcelaPlugin : IFunkcjaSkladnikaPolaSprawozdaniaFinansowego
    {
        private IDostawcaPluginow _dostawca = new DostawcaPluginowInsERT();

        public bool KontoKsiegoweJestWymagane { get { return false; } }

        public bool MozeWskazywacNaKonto { get { return false; } }

        public IEnumerable<DefinicjaParametruFunkcjiSkladnikaPolaSprawozdaniaFinansowego> DefinicjeParametrow { get { return null; } }

        public int Kolejnosc { get { return int.MaxValue; } }

        public Guid Identyfikator { get { return new Guid("C7B900D7-AE0E-4B20-A50C-C2155C982DE1"); } }

        public string Nazwa { get { return "Wartość z pliku c:\\sprawozdanie.xlsx"; } }

        public string Opis { get { return "Wczytuje wartość pola sprawozdania finansowego z odpowiedniej kolumny i wiersza z pliku c:\\sprawozdanie.xlsx"; } }

        public IDostawcaPluginow Dostawca { get { return _dostawca; } }

        public bool MozeLuznoWskazywacNaStruktureSprawozdaniaFinansowego { get; set; }

		public bool MozeOkreslicZnaczniki { get; set; }

		public string GenerujPostacTekstowa(Nowe.ISprawozdanieFinansowe sprawozdanieFinansowe, SkladnikPolaSprawozdaniaFinansowego2 skladnik)
        {
            return "Wartość z c:\\sprawozdanie.xlsx";
        }

        public List<IWartoscParametruFunkcjiSkladnikaPolaSprawozdaniaFinansowego> InicjalizujWartosciParametrow(Nowe.ISprawozdanieFinansowe sprawozdanieFinansowe)
        {
            return null;
        }

        public bool KontoJestPoprawne(DefinicjaKontaKsiegowego konto, ref DataErrorBase blad)
        {
            return true;
        }

        public void Waliduj(SkladnikPolaSprawozdaniaFinansowego2 skladnik, string nazwaWlasciwosci)
        {
        }

        public void Waliduj(SkladnikPolaSprawozdaniaFinansowego2 skladnik, ElementKartotekiSprawozdaniaFinansowego2 elementKartoteki, string nazwaWlasciwosci)
        {
        }

        public void WyczyscBledyWalidacji(SkladnikPolaSprawozdaniaFinansowego2 skladnikpolasprawozdaniafinansowego2)
        {
        }

        public decimal WyliczWartoscNaDzien(
            IKontekstWyliczaniaSprawozdaniaFinansowego kontekst, 
            OkresObrachunkowy okresObrachunkowy, 
            SkladnikPolaSprawozdaniaFinansowego2 skladnik, 
            IFunkcjaKolumnyDefinicjiSprawozdaniaFinansowego funkcjaKolumny, 
            WyliczonaWartoscPolaSprawozdaniaFinansowego2 wyliczonePole, 
            DateTime dzien, 
            List<string> ostrzezenia, 
            out string log)
        {
            return WyliczWartoscZaOkres(
                kontekst,
                okresObrachunkowy,
                skladnik,
                funkcjaKolumny,
                wyliczonePole,
                dzien,
                dzien,
                ostrzezenia,
                out log);
        }

        public decimal WyliczWartoscZaOkres(
            IKontekstWyliczaniaSprawozdaniaFinansowego kontekst, 
            OkresObrachunkowy okresObrachunkowy, 
            SkladnikPolaSprawozdaniaFinansowego2 skladnik, 
            IFunkcjaKolumnyDefinicjiSprawozdaniaFinansowego funkcjaKolumny, 
            WyliczonaWartoscPolaSprawozdaniaFinansowego2 wyliczonePole, 
            DateTime dataOd, 
            DateTime dataDo, 
            List<string> ostrzezenia, 
            out string log)
        {
            log = string.Empty;
            
            var indeksWiersza = 1 + kontekst.UporzadkowanePola.Value.Select(p => p.Pole.Id).ToList().IndexOf(wyliczonePole.Pole.Id);
            if (indeksWiersza > 0)
            {
                using (var spreadsheetDocument = SpreadsheetDocument.Open(@"c:\\sprawozdanie.xlsx", false))
                {
                    var workbookPart = spreadsheetDocument.WorkbookPart;
                    var worksheetPart = workbookPart.WorksheetParts.First();
                    var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();
                    var pierwszyWiersz = sheetData.Elements<Row>().First();

                    var tresciKomorekPierwszegoWiersza = pierwszyWiersz.Elements<Cell>().Select(c => GetString(workbookPart, c)).Select(t => (t ?? string.Empty).Trim().ToLower()).ToList();
                    var indeksKolumny = tresciKomorekPierwszegoWiersza.IndexOf(funkcjaKolumny.Nazwa.ToLower());

                    if (indeksKolumny >= 0)
                    {
                        var wiersz = sheetData.Elements<Row>().ElementAt(indeksWiersza);
                        if (wiersz != null)
                        {
                            var komorka = wiersz.Elements<Cell>().ElementAt(indeksKolumny);
                            if (komorka != null)
                            {
                                decimal wartosc;
                                if (decimal.TryParse(komorka.CellValue.Text, out wartosc))
                                    return wartosc;
                            }
                        }
                    }
                }
            }

            return 0m;
        }

        private static string GetString(WorkbookPart workbookPart, Cell cell)
        {
            if (cell.DataType != null)
            {
                if (cell.DataType == CellValues.SharedString)
                {
                    int id = -1;

                    if (Int32.TryParse(cell.InnerText, out id))
                    {
                        SharedStringItem item = GetSharedStringItemById(workbookPart, id);

                        if (item.Text != null)
                        {
                            //code to take the string value  
                            return item.Text.Text;
                        }
                        else if (item.InnerText != null)
                        {
                            return item.InnerText;
                        }
                        else if (item.InnerXml != null)
                        {
                            return item.InnerXml;
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static SharedStringItem GetSharedStringItemById(WorkbookPart workbookPart, int id)
        {
            return workbookPart.SharedStringTablePart.SharedStringTable.Elements<SharedStringItem>().ElementAt(id);
        }

        public bool KontoJestPoprawne(Nowe.ISprawozdanieFinansowe sprawozdanie, DefinicjaKontaKsiegowego konto, ref DataErrorBase blad)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<int> EkstrahujLuzneWskazaniaNaStruktureSprawozdaniaFinansowego(SkladnikPolaSprawozdaniaFinansowego2 skladnik)
        {
            throw new NotImplementedException();
        }
    }
}
