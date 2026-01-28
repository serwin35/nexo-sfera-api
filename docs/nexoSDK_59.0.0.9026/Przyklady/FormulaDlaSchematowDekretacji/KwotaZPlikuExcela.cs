using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using InsERT.Moria.Dokumenty.Logistyka.DokumentyDoKsiegowania;
using InsERT.Moria.DokumentyDoKsiegowania;
using InsERT.Moria.Finanse;
using InsERT.Moria.ImportKsiegowy;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Waluty;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FormulaDlaSchematowDekretacji
{
    public class KwotaZPlikuExcela :
        IFunkcjaFormulySchematuImportu,
        IFunkcjaDotyczacaDokumentuDoKsiegowaniaFormulySchematuImportu
    {
        private readonly string sciezkaDoPlikuExcela = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "DokumentyIKwoty.xlsx");

        public static readonly Guid Guid = new Guid("95307381-E1E8-4C64-B2A8-264A2E561C66");

        private readonly Func<IWaluty> _waluty;
        private readonly Lazy<Waluta> _walutaSystemowa;

        public KwotaZPlikuExcela(
            Func<IParametryWalut> parametryWalut,
            Func<IWaluty> waluty)
        {
            if (parametryWalut == null)
                throw new ArgumentNullException(nameof(parametryWalut));

            _walutaSystemowa = new Lazy<Waluta>(() => parametryWalut().WalutaSystemowa);
            _waluty = waluty ?? throw new ArgumentNullException(nameof(waluty));
        }

        Type IFunkcjaFormulySchematuImportu.Typ { get => typeof(Kwoty); set => throw new InvalidOperationException(); }

        int IFunkcjaFormulySchematuImportu.MinimalnaLiczbaParametrow => 0;

        int? IFunkcjaFormulySchematuImportu.MaksymalnaLiczbaParametrow => 0;

        string IFunkcjaFormulySchematuImportu.NazwaDoWyswietlenia => "Kwota z pliku Excel'a wg numeru dokumentu";

        Guid IFunkcja.Identyfikator => Guid;

        string IFunkcja.Nazwa => "Kwota z pliku Excel'a wg numeru dokumentu";

        string IFunkcja.Opis => "Odczytuje kwotę z pliku excel wg numeru dokumentu";

        IDostawcaPluginow IFunkcja.Dostawca => new DostawcaPluginow();

        private IPozycjaTresciDokumentuDoKsiegowania _trescDokumentuDoKsiegowania;
        IPozycjaTresciDokumentuDoKsiegowania IFunkcjaDotyczacaDokumentuDoKsiegowaniaFormulySchematuImportu.TrescDokumentuDoKsiegowania { set => _trescDokumentuDoKsiegowania = value; }

        Enum IFunkcjaDotyczacaDokumentuDoKsiegowaniaFormulySchematuImportu.KontekstUzycia => ElementTresciDokumentuHandlowego.Naglowek;

        bool IFunkcjaFormulySchematuImportu.CzyMoznaUzycDlaSchematu(SchematImportu schemat) => true;

        bool IFunkcjaDotyczacaDokumentuDoKsiegowaniaFormulySchematuImportu.CzyMoznaUzycDlaTypu(TypDokumentuDoKsiegowania typ)
        {
            return typ.GrupaTypowDokumentuDoKsiegowania == (byte)DziedzinaDokumentuDoKsiegowania.DokumentyHandlowe;
        }

        Type IFunkcjaFormulySchematuImportu.OkreslTypParametru(int nrParametru, Type typFunkcji, Type[] typyParametrow) => null;

        Type IFunkcjaFormulySchematuImportu.OkreslTypZwracany(params Type[] typyParametrow) => ((IFunkcjaFormulySchematuImportu)this).Typ;

        string IFunkcjaFormulySchematuImportu.OpisParametru(int nrParametru) => string.Empty;

        IEnumerable<FormulaSchematuImportu> IFunkcjaFormulySchematuImportu.PobierzParametryMogaceStanowicWartosc(FormulaSchematuImportu formula) => Enumerable.Empty<FormulaSchematuImportu>();

        string IFunkcjaFormulySchematuImportu.PobierzReprezentacjeTekstowa(params string[] parametry)
            => "Kwota z pliku Excel'a wg numeru dokumentu";

        object IFunkcjaFormulySchematuImportu.Wykonaj(params Func<bool, object>[] parametry)
        {
            if (_trescDokumentuDoKsiegowania == null)
                return new Kwoty(0m, _walutaSystemowa.Value);

            var numerDokumentu = _trescDokumentuDoKsiegowania.Wartosc<string>(ElementTresciDokumentuHandlowego.NaglowekNumer)?.Trim();

            if (string.IsNullOrEmpty(numerDokumentu) || !File.Exists(sciezkaDoPlikuExcela))
                return new Kwoty(0m, _walutaSystemowa.Value);

            using (var spreadsheetDocument = SpreadsheetDocument.Open(sciezkaDoPlikuExcela, false))
            {
                var workbookPart = spreadsheetDocument.WorkbookPart;
                var worksheetPart = workbookPart.WorksheetParts.First();
                var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();
                var wiersze = sheetData.Elements<Row>();

                foreach (var wiersz in wiersze)
                {
                    var tresciKomorek =
                        wiersz.Elements<Cell>().Take(3).Select(c => GetString(workbookPart, c)).Select(t => (t ?? string.Empty).Trim()).ToList();

                    if (tresciKomorek.Count >= 2 && tresciKomorek.Count <= 3)
                    {
                        var numerDokumentuExcel = tresciKomorek[0];
                        if (numerDokumentu.Equals(numerDokumentuExcel, StringComparison.CurrentCultureIgnoreCase))
                        {
                            var kwotaString = tresciKomorek[1];

                            decimal kwota;
                            if (
                                string.IsNullOrEmpty(kwotaString) ||
                                (
                                    !decimal.TryParse(kwotaString, NumberStyles.Any, CultureInfo.InvariantCulture, out kwota) &&
                                    !decimal.TryParse(kwotaString, NumberStyles.Any, CultureInfo.CurrentCulture, out kwota)
                                )
                            )
                                return new Kwoty(0m, _walutaSystemowa.Value);

                            Waluta waluta;
                            var symbolWaluty = tresciKomorek[2];
                            if (string.IsNullOrEmpty(symbolWaluty))
                                waluta = _walutaSystemowa.Value;
                            else
                                waluta = _waluty().Dane.Wszystkie().FirstOrDefault(w => w.Symbol == symbolWaluty);

                            return new Kwoty(kwota, waluta ?? _walutaSystemowa.Value);

                        }
                    }
                }
            }

            return new Kwoty(0m, _walutaSystemowa.Value);
        }

        private static string GetString(WorkbookPart workbookPart, Cell cell)
        {
            if (cell.DataType != null)
            {
                if (cell.DataType == CellValues.SharedString)
                {
                    if (int.TryParse(cell.InnerText, out var id))
                    {
                        SharedStringItem item = GetSharedStringItemById(workbookPart, id);

                        if (item.Text != null)
                        {
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

            return cell.InnerText ?? string.Empty;
        }

        private static SharedStringItem GetSharedStringItemById(WorkbookPart workbookPart, int id)
        {
            return workbookPart.SharedStringTablePart.SharedStringTable.Elements<SharedStringItem>().ElementAt(id);
        }
    }
}
