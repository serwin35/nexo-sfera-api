using System;
using System.Collections.Generic;
using System.Data;

namespace DaneArchiwalne
{
    public partial class PodgladDokumentu
    {
        private DataSet _dane;
        private DataRow _naglowek;
        private DataRowCollection _pozycje;
        private DataRowCollection _pozycjeVat;
        private DataRowCollection _platnosci;
        private List<DataRow> _platnosciZaplacono;
        private List<DataRow> _platnosciPozostalo;
        private int _precyzjaWaluty;
        private bool _korekta;
        private bool _zwrot;
        private bool _nabywca, _nabywcaAdres;
        private string _nabywcaNazwa;
        private bool _zakupowy;
        private string _firmaEtykieta;
        private string _dataEtykieta;
        private bool _magazynowy;
        public PodgladDokumentu(DataSet dane, bool magazynowy)
        {
            _dane = dane;
            _naglowek = dane.Tables["Dokument"].Rows[0];
            _pozycje = dane.Tables["Pozycje"].Rows;
            _pozycjeVat = dane.Tables["Vat"].Rows;
            _platnosci = dane.Tables["Platnosci"].Rows;
            _precyzjaWaluty = (int)(byte)_naglowek["PrecyzjaWaluty"];
            _korekta = (bool)_naglowek["Korekta"];
            _zwrot = _naglowek.Table.Columns.Contains("Zwrot") && (bool)_naglowek["Zwrot"];

            _platnosciPozostalo = new List<DataRow>();
            _platnosciZaplacono = new List<DataRow>();
            foreach (DataRow pl in _platnosci)
                ((bool)pl["zaplacone"] ? _platnosciZaplacono : _platnosciPozostalo).Add(pl); 

            _nabywca = _naglowek.Table.Columns.Contains("Nabywca_PelnaNazwa");
            if (_nabywca)
            {
                object nazwa = _naglowek["Nabywca_PelnaNazwa"];
                if (!(nazwa is System.DBNull))
                    _nabywcaNazwa = (string)nazwa;
                if (string.IsNullOrWhiteSpace(_nabywcaNazwa))
                {
                    nazwa = _naglowek["Nabywca_Nazwa"];
                    if (!(nazwa is System.DBNull))
                        _nabywcaNazwa = (string)nazwa;
                }
                _nabywca = !string.IsNullOrWhiteSpace(_nabywcaNazwa);
            }
            _nabywcaAdres = _naglowek.Table.Columns.Contains("Nabywca_Adres");
            if (_nabywcaAdres)
                _nabywcaAdres = !string.IsNullOrEmpty((string)_naglowek["Nabywca_Adres"]);

            if (_korekta)
            {
                foreach (DataRow row in _pozycjeVat)
                {
                    _naglowek["NettoPrzedKorekta"] = (decimal)_naglowek["NettoPrzedKorekta"] + (decimal)row["NettoPrzedKorekta"];
                    _naglowek["BruttoPrzedKorekta"] = (decimal)_naglowek["BruttoPrzedKorekta"] + (decimal)row["BruttoPrzedKorekta"];
                    _naglowek["VatPrzedKorekta"] = (decimal)_naglowek["VatPrzedKorekta"] + (decimal)row["VatPrzedKorekta"];
                    _naglowek["NettoPoKorekcie"] = (decimal)_naglowek["NettoPoKorekcie"] + (decimal)row["NettoPoKorekcie"];
                    _naglowek["BruttoPoKorekcie"] = (decimal)_naglowek["BruttoPoKorekcie"] + (decimal)row["BruttoPoKorekcie"];
                    _naglowek["VatPoKorekcie"] = (decimal)_naglowek["VatPoKorekcie"] + (decimal)row["VatPoKorekcie"];
                }
            }

            _zakupowy = (bool)_naglowek["Zakupowy"];
            _magazynowy = magazynowy;
            _firmaEtykieta = _magazynowy ? (_zakupowy ? "Dostawca" : "Odbiorca") : (_zakupowy ? "Sprzedawca" : "Nabywca");
            _dataEtykieta = _magazynowy ? (_zakupowy ? "Data przyjęcia" : "Data wydania") : "Data wystawienia";
        }

        private string Formatuj(object wartosc)
        {
            if (wartosc is decimal)
                return Formatuj((decimal)wartosc, _precyzjaWaluty);
            else if (wartosc is DateTime)
                return Formatuj((DateTime)wartosc);
            else
                return wartosc.ToString();
        }
        private string Formatuj(decimal wartosc, int precyzja)
        {
            return wartosc.ToString("###,###,###,###,##0." + new string('0', precyzja));
        }
        private string Formatuj(object wartosc, object precyzja)
        {
            //if (precyzja == null)
            //    precyzja = 2;
            if (precyzja is byte)
                return Formatuj((decimal)wartosc, (byte)precyzja);
            else if (wartosc is System.DBNull)
                return Formatuj(0m, 2);
            else
                return Formatuj((decimal)wartosc, (int)precyzja);
        }
        private string Formatuj(DateTime data)
        {
            return data.ToString("yyyy-MM-dd");
        }
        private string FormatujRabat(object wartosc)
        {
            if (wartosc is System.DBNull)
                wartosc = 0m;
            System.Diagnostics.Debug.Assert(wartosc is decimal);
            return (wartosc is decimal) ? ((decimal)wartosc).ToString("0.#") : wartosc.ToString();
        }
    }
}