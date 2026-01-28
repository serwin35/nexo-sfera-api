using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Linq;

namespace PrzykladowaFunkcjaRozdzielajacaZaliczke
{
    public class RozdzielajZaliczkeNaPozycjeNajpierwTowaryOdNajmniejszejStawki : IFunkcjaRozdzielajacaZaliczkeNaPozycje
    {
        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("744a44fd-9263-42c3-9820-4a5f61b6abe9");

        public string Nazwa => "Najpierw towary od najniższej stawki";

        public string Opis => "Funkcja wpisuje zaliczkę najpierw na towary i komplety od najniższej stawki, a później na usługi.";

        public void RozdzielZaliczke(DokumentHandlowy dokument)
        {
            decimal wartoscZaliczki = dokument.AspektZaliczki.Zaliczka;

            var pozycjePosortowane = dokument.Pozycje
                .Where(p => !p.RodzajAsortymentu.Gratis && !p.RodzajAsortymentu.Material) // gratisy i materiały nie są nigdy zaliczkowane
                .OrderByDescending(p => p.RodzajAsortymentu.StanyMagazynowe) // najpierw asortyment ze stanem magazynowym (towary/komplety/opakowania)
                .ThenBy(p => p.StawkaVat.Stawka) // wg stawki rosnąco
                .ThenBy(p => p.LP); // ostatecznie wg LP pozycji

            foreach (var pozycja in pozycjePosortowane)
            {
                // unikamy ustawiania ujemnej zaliczki na pozycji:
                decimal pozostalaZaliczkaPozycji = Math.Max(0m, pozycja.WyliczPozostalaZaliczkePozycji(false));
                // nie chcemy przekroczyć wartości zaliczki dla całego dokumentu:
                decimal zaliczkaNaPozycje = Math.Min(pozostalaZaliczkaPozycji, wartoscZaliczki);
                pozycja.AspektZaliczkiPozycji.Zaliczka.Brutto = zaliczkaNaPozycje;
                wartoscZaliczki -= zaliczkaNaPozycje;
            }
        }
    }
}