using System;
using System.Collections.Generic;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.CennikiICeny;
using InsERT.Moria.Rozszerzanie;

namespace NexoPlugins
{
    public abstract class FunkcjaWyliczaniaCenyBazowejNa5 : IFunkcjaWyliczaniaCenyBazowej
    {
        public PrzeznaczenieFunkcjiWyliczaniaCenyBazowej PrzeznaczenieFunkcji { get; set; }

        public RodzajFunkcjiWyliczaniaCenyBazowej RodzajFunkcji { get { return RodzajFunkcjiWyliczaniaCenyBazowej.Cennikowa; } }

        public Guid Identyfikator { get; set; }

        public string Nazwa
        {
            get { return "Cena równa 5,00."; }
        }

        public string Opis
        {
            get { return "Ustawia cenę na 5,00 w walucie ceny ewidencyjnej."; }
        }

        public IDostawcaPluginow Dostawca
        {
            get { return new DostawcaPluginow(); }
        }

        public InsERT.Moria.Finanse.Kwota WyliczCeneBazowa(Asortyment asortyment, IKontekstWyliczeniaCeny kontekst)
        {
            return new InsERT.Moria.Finanse.Kwota(5m, asortyment.WalutaCenyEwidencyjnej);
        }

        public RabatDomyslny WyliczRabatDomyslny(Asortyment asortyment, IKontekstWyliczeniaCeny kontekst)
        {
            return null;
        }
    }

    public class FunkcjaWyliczaniaCenyBazowejNa5DlaUslug : FunkcjaWyliczaniaCenyBazowejNa5
    {
        public FunkcjaWyliczaniaCenyBazowejNa5DlaUslug()
        {
            PrzeznaczenieFunkcji = PrzeznaczenieFunkcjiWyliczaniaCenyBazowej.DlaPozycjiAsortymentuBezStanowMagazynowych;
            Identyfikator = new Guid("{2AA32BF8-26AB-45C0-B913-D668BD157F7E}");
        }
    }

    public class FunkcjaWyliczaniaCenyBazowejNa5DlaTowarow : FunkcjaWyliczaniaCenyBazowejNa5
    {
        public FunkcjaWyliczaniaCenyBazowejNa5DlaTowarow()
        {
            PrzeznaczenieFunkcji = PrzeznaczenieFunkcjiWyliczaniaCenyBazowej.DlaPozycjiAsortymentuZeStanamiMagazynowymi;
            Identyfikator = new Guid("{0A3C7661-82A4-4512-B9E5-4A4A35D8F137}");
        }
    }

    class DostawcaPluginow : IDostawcaPluginow
    {
        public string Adres { get { return "ul. Jerzmanowska 2, 54-519 Wrocław"; } }
        public string AdresWWW { get { return "www.insert.com.pl"; } }
        public IEnumerable<string> Kontakty { get { yield return "tel. +48 71 78 76 100"; yield return "email: office@insert.com.pl"; } }
        public string KRS { get { return "0000306888"; } }
        public string Nazwa { get { return "InsERT S.A."; } }
        public string NIP { get { return "898-19-45-134"; } }
        public string REGON { get { return "932283479"; } }
    }
}
