using System;
using System.Collections.Generic;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.CennikiICeny;
using InsERT.Moria.Rozszerzanie;
using System.IO;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Finanse;

namespace NexoPlugins
{
    public abstract class FunkcjaWyliczaniaCenyZPliku : IFunkcjaWyliczaniaCenyBazowej
    {
        public PrzeznaczenieFunkcjiWyliczaniaCenyBazowej PrzeznaczenieFunkcji { get; protected set; }

        public RodzajFunkcjiWyliczaniaCenyBazowej RodzajFunkcji { get; protected set; }

        private string _filename = @"c:\temp\towary.csv";
        private Dictionary<string, decimal> _lista = new Dictionary<string, decimal>();

        public Kwota WyliczCeneBazowa(Asortyment asortyment, IKontekstWyliczeniaCeny kontekst)
        {
            if (_lista.Count == 0)
                WczytajPlik(_filename);
            decimal value;
            if (!string.IsNullOrEmpty(asortyment.Symbol) && _lista.TryGetValue(asortyment.Symbol, out value))
                return new Kwota(value, asortyment.WalutaCenyEwidencyjnej);
            else
                return new Kwota(0m, asortyment.WalutaCenyEwidencyjnej);
        }

        public RabatDomyslny WyliczRabatDomyslny(Asortyment asortyment, IKontekstWyliczeniaCeny kontekst)
        {
            return null;
        }

        private void WczytajPlik(string filename)
        {
            try
            {
                var reader = File.OpenText(filename);
                string line = null;
                while (!reader.EndOfStream)
                {
                    if (!string.IsNullOrWhiteSpace(line) && !string.IsNullOrEmpty(line))
                    {
                        var fields = line.Split(';');
                        decimal value;
                        if (decimal.TryParse(fields[1], out value))
                            _lista.Add(fields[0], value);
                    }
                    line = reader.ReadLine();
                }
            }
            catch (Exception ex)
            {
            }
        }

        public Guid Identyfikator { get; protected set; }

        public string Nazwa
        {
            get { return "Cena z pliku"; }
        }

        public string Opis
        {
            get { return "Ustawia cenę na cenę z pliku w walucie ceny ewidencyjnej."; }
        }

        public IDostawcaPluginow Dostawca
        {
            get { return new DostawcaPluginow(); }
        }
}

    public class FunkcjaWyliczaniaCenyZPlikuDlaUslugCennikowa : FunkcjaWyliczaniaCenyZPliku
    {
        public FunkcjaWyliczaniaCenyZPlikuDlaUslugCennikowa()
        {
            RodzajFunkcji = RodzajFunkcjiWyliczaniaCenyBazowej.Cennikowa;
            PrzeznaczenieFunkcji = PrzeznaczenieFunkcjiWyliczaniaCenyBazowej.DlaPozycjiAsortymentuBezStanowMagazynowych;
            Identyfikator = new Guid("{EAED76D7-D271-4848-B469-640E23FA2F34}");
        }
    }

    public class FunkcjaWyliczaniaCenyZPlikuDlaTowarowCennikowa : FunkcjaWyliczaniaCenyZPliku
    {
        public FunkcjaWyliczaniaCenyZPlikuDlaTowarowCennikowa()
        {
            RodzajFunkcji = RodzajFunkcjiWyliczaniaCenyBazowej.Cennikowa;
            PrzeznaczenieFunkcji = PrzeznaczenieFunkcjiWyliczaniaCenyBazowej.DlaPozycjiAsortymentuZeStanamiMagazynowymi;
            Identyfikator = new Guid("{21A92E7F-7DA4-4E3B-B8A4-95D4E72D315B}");
        }
    }

    public class FunkcjaWyliczaniaCenyZPlikuDlaUslugSprzedazowa : FunkcjaWyliczaniaCenyZPliku
    {
        public FunkcjaWyliczaniaCenyZPlikuDlaUslugSprzedazowa()
        {
            RodzajFunkcji = RodzajFunkcjiWyliczaniaCenyBazowej.DlaDokumentowSprzedazy;
            PrzeznaczenieFunkcji = PrzeznaczenieFunkcjiWyliczaniaCenyBazowej.DlaPozycjiAsortymentuBezStanowMagazynowych;
            Identyfikator = new Guid("{FD1C5D82-1CD3-4FD1-936F-25D153ACEDD6}");
        }
    }

    public class FunkcjaWyliczaniaCenyZPlikuDlaTowarowSprzedazowa : FunkcjaWyliczaniaCenyZPliku
    {
        public FunkcjaWyliczaniaCenyZPlikuDlaTowarowSprzedazowa()
        {
            RodzajFunkcji = RodzajFunkcjiWyliczaniaCenyBazowej.DlaDokumentowSprzedazy;
            PrzeznaczenieFunkcji = PrzeznaczenieFunkcjiWyliczaniaCenyBazowej.DlaPozycjiAsortymentuZeStanamiMagazynowymi;
            Identyfikator = new Guid("{C616F55A-1528-4201-9B46-0656CEA4307F}");
        }
    }
}
